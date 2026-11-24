#!/usr/bin/env node
// El pipeline de los PDF del proyecto: markdown â†’ HTML â†’ PDF.
//
//   node scripts/pdf/md-to-pdf.mjs --in <archivo.md> --out <archivo.pdf> [opciones]
//
// - El HTML se diseña (la maqueta: scripts/pdf/maqueta.css) y después se
//   convierte: nunca se escribe el PDF directo.
// - La conversión la hace el Chrome que ya está en la máquina, por CDP
//   (Page.printToPDF): sale con el pie de la página y su número.
// - No depende de nada instalado: node y el Chrome.
//
// Opciones:
//   --in <md>        el markdown de origen (obligatorio)
//   --out <pdf>      el PDF de salida (obligatorio)
//   --title <texto>  el título de la portada
//   --subtitle <txt> el subtítulo de la portada
//   --repo <texto>   de qué repositorio sale (con el commit, en la portada)
//   --date <texto>   la fecha de la portada
//   --css <archivo>  la maqueta (por defecto, la de al lado)
//   --no-index       sin la hoja del índice
//   --no-numbers     sin el pie con el número de página (el manual del
//                    usuario se sigue paso a paso, no se consulta por página)
//   --keep-html      deja el HTML intermedio (por defecto, lo deja)

import { readFileSync, writeFileSync, existsSync, mkdirSync, rmSync, readdirSync } from 'node:fs';
import { spawn } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);

// ---------------------------------------------------------------------------
// La línea de comandos
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (!token.startsWith('--')) continue;
    const key = token.slice(2);
    const next = argv[i + 1];
    const value = next === undefined || next.startsWith('--') ? true : next;
    if (next !== undefined && !next.startsWith('--')) i++;

    // Un flag que se repite se acumula (--metric "32|secciones" --metric ...).
    if (key in args) {
      args[key] = [].concat(args[key], value);
    } else {
      args[key] = value;
    }
  }
  return args;
}

const args = parseArgs(process.argv.slice(2));

if (!args.in || !args.out) {
  console.error('Uso: node scripts/pdf/md-to-pdf.mjs --in <archivo.md> --out <archivo.pdf>');
  process.exit(2);
}

const sourcePath = resolve(String(args.in));
const outputPath = resolve(String(args.out));
const maquetaPath = resolve(String(args.css ?? join(dirname(new URL(import.meta.url).pathname.replace(/^\//, '')), 'maqueta.css')));
const htmlPath = outputPath.replace(/\.pdf$/i, '') + '.html';

// ---------------------------------------------------------------------------
// El markdown
// ---------------------------------------------------------------------------

const escapeHtml = (text) =>
  text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function slug(text) {
  return text
    .toLowerCase()
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 60) || 'seccion';
}

function splitRow(line) {
  return line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(cell => cell.trim());
}

function isTableLine(line) {
  return /^\s*\|.*\|\s*$/.test(line);
}

function isTableSeparator(line) {
  return /^\s*\|[\s:|-]+\|\s*$/.test(line) && line.includes('-');
}

function parseMarkdown(markdown) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  const outline = [];
  let section = 0;
  let part = 0;
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    const fence = line.match(/^```([A-Za-z0-9+#._-]*)\s*$/);
    if (fence) {
      const lang = fence[1].toLowerCase();
      const body = [];
      i++;
      while (i < lines.length && !/^```\s*$/.test(lines[i])) { body.push(lines[i]); i++; }
      i++;
      blocks.push({ type: 'code', lang, code: body.join('\n') });
      continue;
    }

    const heading = line.match(/^(#{1,6})\s+(.*)$/);
    if (heading) {
      const level = heading[1].length;
      const text = heading[2].replace(/\s+#+\s*$/, '').trim();
      const id = slug(text);

      if (level <= 2) {
        // El indice lleva la misma numeracion que el documento: las partes
        // (h1) y las secciones (h2) cuelgan de la parte que las contiene.
        if (level === 1) {
          // El primer # es el titulo del documento: no numera partes.
          const isTheTitle = blocks.length === 0;
          if (!isTheTitle) part += 1;
          section = 0;
          outline.push({ level: 1, text, id, number: isTheTitle ? '' : String(part) });
        } else {
          section += 1;
          outline.push({
            level: 2,
            text,
            id,
            number: part > 0 ? `${part}.${section}` : String(section)
          });
        }
      }

      blocks.push({ type: 'heading', level, text, id, number: outline[outline.length - 1]?.number ?? '' });
      i++;
      continue;
    }

    if (isTableLine(line) && i + 1 < lines.length && isTableSeparator(lines[i + 1])) {
      const header = splitRow(line);
      i += 2;
      const rows = [];
      while (i < lines.length && isTableLine(lines[i])) { rows.push(splitRow(lines[i])); i++; }
      blocks.push({ type: 'table', header, rows });
      continue;
    }

    if (/^\s*>\s?/.test(line)) {
      const body = [];
      while (i < lines.length && /^\s*>\s?/.test(lines[i])) {
        body.push(lines[i].replace(/^\s*>\s?/, ''));
        i++;
      }
      blocks.push({ type: 'quote', text: body.join('\n') });
      continue;
    }

    if (/^\s*(-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      blocks.push({ type: 'hr' });
      i++;
      continue;
    }

    if (/^\s*([-*+]|\d+[.)])\s+/.test(line)) {
      const items = [];
      while (i < lines.length) {
        const match = lines[i].match(/^(\s*)([-*+]|\d+[.)])\s+(.*)$/);
        if (match) {
          items.push({
            indent: match[1].replace(/\t/g, '    ').length,
            ordered: /\d/.test(match[2]),
            text: match[3]
          });
          i++;
          continue;
        }
        // La continuación de un ítem: la línea indented que no abre otra cosa.
        if (items.length && /^\s{2,}\S/.test(lines[i]) &&
            !/^\s*([-*+]|\d+[.)])\s+/.test(lines[i]) &&
            !/^\s*>\s?/.test(lines[i]) &&
            !/^```/.test(lines[i]) &&
            !isTableLine(lines[i])) {
          items[items.length - 1].text += ' ' + lines[i].trim();
          i++;
          continue;
        }
        break;
      }
      // Una lista numerada que viene detras de un texto que termina en ":" son
      // los pasos de un procedimiento: salen como los pasos de la maqueta.
      const isProcedure = items.length > 0 && items[0].ordered &&
        blocks.length > 0 &&
        blocks[blocks.length - 1].type === 'paragraph' &&
        blocks[blocks.length - 1].text.trim().endsWith(':');
      blocks.push({ type: 'list', items, isProcedure });
      continue;
    }

    if (line.trim() === '') { i++; continue; }

    const body = [];
    while (i < lines.length && lines[i].trim() !== '' &&
           !/^#{1,6}\s/.test(lines[i]) &&
           !/^```/.test(lines[i]) &&
           !/^\s*>\s?/.test(lines[i]) &&
           !/^\s*([-*+]|\d+[.)])\s+/.test(lines[i]) &&
           !isTableLine(lines[i])) {
      body.push(lines[i]);
      i++;
    }
    if (body.length) blocks.push({ type: 'paragraph', text: body.join('\n') });
    else i++;
  }

  return { blocks, outline };
}

// ---------------------------------------------------------------------------
// Lo que va dentro de una línea
// ---------------------------------------------------------------------------

function inline(text) {
  let html = escapeHtml(text);
  const codes = [];

  html = html.replace(/`([^`]+)`/g, (_, code) => {
    codes.push(code);
    return `\u0000${codes.length - 1}\u0000`;
  });

  // Las imagenes del markdown: la captura de una pantalla. Si el archivo no
  // esta todavia (la captura se toma con el sistema arriba), se deja el
  // recuadro del hueco en vez de una imagen rota.
  html = html.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, (_, alt, src) => {
    const file = src.split('/').pop();
    const exists = existsSync(join(dirname(outputPath), src));
    if (!exists) {
      return `<figure class="captura pendiente"><div class="hueco">`
        + `<span class="hueco-titulo">Captura de pantalla pendiente</span>`
        + `<span class="hueco-nombre">${escapeHtml(file ?? alt)}</span>`
        + `</div><figcaption>${escapeHtml(alt)}</figcaption></figure>`;
    }
    return `<figure class="captura"><img src="${src}" alt="${alt}"><figcaption>${escapeHtml(alt)}</figcaption></figure>`;
  });

  html = html.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, '<a href="$2">$1</a>');
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/(^|[\s(>])\*([^*\n]+)\*/g, '$1<em>$2</em>');
  html = html.replace(/~~([^~]+)~~/g, '<del>$1</del>');

  return html.replace(/\u0000(\d+)\u0000/g, (_, index) => `<code>${codes[Number(index)]}</code>`);
}

// ---------------------------------------------------------------------------
// El resaltado del código
// ---------------------------------------------------------------------------

const KEYWORDS = {
  csharp: 'abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach get goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref required return sbyte sealed set short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile when where while with yield'.split(' '),
  typescript: 'abstract any as asserts async await boolean break case catch class const continue declare default delete do else enum export extends false finally for from function get if implements import in infer instanceof interface keyof let namespace never new null number of private protected public readonly require return satisfies set static string super switch this throw true try type typeof undefined unique unknown var void while yield'.split(' '),
  sql: 'add all alter and as asc begin between by case check column commit constraint create cross database default delete desc distinct drop else end exists foreign from full group having in index inner insert into is join key left like limit not null on or order outer primary references replace right rollback select set table then top transaction trigger truncate union unique update values view when where with'.split(' '),
  bash: 'case cd chmod cp curl cut date do done echo else elif esac exit export fi for grep head if in local mkdir mv printf pwd read rm sed set source tail then touch tr uniq unset until while xargs'.split(' '),
  json: 'true false null'.split(' '),
  yaml: 'true false null yes no'.split(' '),
  css: 'important media import keyframes font face'.split(' ')
};

const ALIASES = {
  ts: 'typescript', tsx: 'typescript', javascript: 'typescript', js: 'typescript',
  jsx: 'typescript', cs: 'csharp', 'c#': 'csharp', sh: 'bash', shell: 'bash',
  zsh: 'bash', console: 'bash', yml: 'yaml', html: 'markup', xml: 'markup',
  vue: 'markup', md: 'markup', markdown: 'markup'
};

const TYPES = /^(?:[A-Z][A-Za-z0-9_]*|[A-Z]{2,})$/;

function highlight(code, lang) {
  const language = ALIASES[lang] ?? lang;
  const keywords = KEYWORDS[language] ?? KEYWORDS.csharp;
  const keywordsByLower = new Set(keywords.map(word => word.toLowerCase()));

  const lineComment = language === 'bash' || language === 'yaml' ? '#' : '//';
  const blockComment = language === 'csharp' || language === 'typescript' ? ['/*', '*/'] : null;

  let out = '';
  let i = 0;

  while (i < code.length) {
    const rest = code.slice(i);

    // comentario de línea
    if (rest.startsWith(lineComment)) {
      const end = code.indexOf('\n', i);
      const stop = end === -1 ? code.length : end;
      out += `<span class="tok-com">${escapeHtml(code.slice(i, stop))}</span>`;
      i = stop;
      continue;
    }

    // comentario de bloque
    if (blockComment && rest.startsWith(blockComment[0])) {
      const end = code.indexOf(blockComment[1], i);
      const stop = end === -1 ? code.length : end + blockComment[1].length;
      out += `<span class="tok-com">${escapeHtml(code.slice(i, stop))}</span>`;
      i = stop;
      continue;
    }

    // cadena
    const quote = /["'`]/.exec(rest);
    if (quote && quote.index === 0) {
      const mark = quote[0];
      let j = 1;
      while (j < rest.length) {
        if (rest[j] === '\\') { j += 2; continue; }
        if (rest[j] === mark) { j++; break; }
        j++;
      }
      out += `<span class="tok-str">${escapeHtml(rest.slice(0, j))}</span>`;
      i += j;
      continue;
    }

    // número
    const number = /^\d[\d_.]*/.exec(rest);
    if (number) {
      out += `<span class="tok-num">${escapeHtml(number[0])}</span>`;
      i += number[0].length;
      continue;
    }

    // palabra
    const word = /^[A-Za-z_][A-Za-z0-9_]*/.exec(rest);
    if (word) {
      const text = word[0];
      const after = rest.slice(text.length);
      let cls = null;

      if (keywordsByLower.has(text.toLowerCase())) cls = 'tok-key';
      else if (TYPES.test(text)) cls = 'tok-type';
      else if (/^\s*\(/.test(after)) cls = 'tok-fn';

      out += cls ? `<span class="${cls}">${escapeHtml(text)}</span>` : escapeHtml(text);
      i += text.length;
      continue;
    }

    // el resto, carácter a carácter
    const ch = rest[0];
    out += /[{}()[\];,.:<>=+\-*/%!&|?]/.test(ch)
      ? `<span class="tok-punc">${escapeHtml(ch)}</span>`
      : escapeHtml(ch);
    i += 1;
  }

  return out;
}

// ---------------------------------------------------------------------------
// El HTML
// ---------------------------------------------------------------------------

function renderList(items) {
  const levels = [];
  for (const item of items) {
    const level = item.indent >= 2 ? 1 : 0;
    if (levels.length === 0) levels.push(level);
    else if (level !== levels[levels.length - 1]) levels.push(level);
  }

  let html = '';
  let open = 0;
  let currentOrdered = null;

  const closeTo = (level) => {
    while (open > level) {
      html += currentOrdered ? '</ol>' : '</ul>';
      open--;
      currentOrdered = null;
    }
  };

  for (const item of items) {
    const level = item.indent >= 2 ? 1 : 0;
    const ordered = item.ordered;

    if (open === 0) {
      html += ordered ? '<ol>' : '<ul>';
      open = 1;
      currentOrdered = ordered;
    } else if (ordered !== currentOrdered) {
      html += currentOrdered ? '</ol>' : '</ul>';
      html += ordered ? '<ol>' : '<ul>';
      currentOrdered = ordered;
    } else if (level === 1) {
      html += '</li><li>';
      html += inline(item.text);
      continue;
    }

    html += `<li>${inline(item.text)}`;
  }

  while (open > 0) {
    html += currentOrdered ? '</ol>' : '</ul>';
    open--;
  }

  return html;
}

function renderBlock(block) {
  switch (block.type) {
    case 'heading': {
      const tag = `h${Math.min(block.level, 4)}`;
      // El encabezado corrido no va aqui: lo lleva la hoja, que sabe en que
      // seccion esta. El titulo es solo el titulo.
      return `<${tag} id="${block.id}">${inline(block.text)}</${tag}>`;
    }
    case 'code':
      // El resaltado solo cuando el bloque declara su lenguaje: un arbol de
      // carpetas o un diagrama no son C# y pintarlos de colores los hace
      // ilegibles.
      return `<pre><code>${block.lang ? highlight(block.code, block.lang) : escapeHtml(block.code)}</code></pre>`;
    case 'table': {
      const head = block.header.map(cell => `<th>${inline(cell)}</th>`).join('');
      const body = block.rows
        .map(row => `<tr>${row.map(cell => `<td>${inline(cell)}</td>`).join('')}</tr>`)
        .join('');
      return `<table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>`;
    }
    case 'quote': {
      // Una cita que empieza con una palabra en negrita es el resumen de la
      // seccion: sale como el recuadro de la maqueta.
      const lead = /^(\*\*[^*]+\*\*)\s*([\s\S]*)$/.exec(block.text.trim());
      if (lead) {
        return `<div class="exec-summary"><p><strong>${inline(lead[1])}</strong> ${inline(lead[2])}</p></div>`;
      }
      return `<blockquote><p>${inline(block.text)}</p></blockquote>`;
    }
    case 'hr':
      return '<hr>';
    case 'list': {
      if (block.isProcedure) {
        const steps = block.items.map((item, index) =>
          `<div class="fs"><span class="n">${index + 1}</span><p>${inline(item.text)}</p></div>`).join('');
        return `<div class="flow">${steps}</div>`;
      }
      return renderList(block.items);
    }
    case 'paragraph': {
      const text = block.text.trim();
      if (/^\[PRINCIPIO/i.test(text)) {
        const rest = text.replace(/^\[PRINCIPIO[:\]]*\s*/i, '');
        return `<div class="callout"><span class="tag">Principio</span><p>${inline(rest)}</p></div>`;
      }
      if (/^\[STACK/i.test(text)) {
        const rest = text.replace(/^\[STACK[:\]]*\s*/i, '');
        return `<div class="callout callout-stack"><span class="tag">Stack</span><p>${inline(rest)}</p></div>`;
      }
      return `<p>${inline(text).replace(/\n/g, '<br>')}</p>`;
    }
    default:
      return '';
  }
}

// El script que reparte el documento en hojas. Va dentro del HTML generado
// (el navegador es el que mide): cada hoja mide 210 x 297 mm y lleva su
// encabezado corrido y su pie con el numero de pagina.
const COMPOSE_JS = `
(function () {
  var brand = document.getElementById('brand').textContent;
  var footText = document.getElementById('foot-text').textContent;
  var withNumbers = !!document.getElementById('numbers');
  var outline = JSON.parse(document.getElementById('outline-data').textContent);
  var content = document.getElementById('content');
  var indexHost = document.getElementById('index-blocks');

  var stage = document.createElement('div');
  stage.id = 'stage';
  stage.style.cssText = 'position:absolute;left:-30000px;top:0;width:210mm;';
  document.body.appendChild(stage);

  function newSheet(descriptor) {
    var sheet = document.createElement('div');
    sheet.className = 'page';
    sheet.innerHTML = '<div class="page-header"><span class="brand"></span><span class="descriptor"></span></div>'
      + '<div class="page-body"></div>'
      + '<div class="page-foot"><span class="repo"></span><span class="pn"></span></div>';
    sheet.querySelector('.brand').textContent = brand;
    sheet.querySelector('.descriptor').textContent = descriptor;
    sheet.querySelector('.repo').textContent = footText;
    stage.appendChild(sheet);
    return sheet;
  }

  function fits(body) { return body.scrollHeight <= body.clientHeight + 1; }

  // Las piezas de texto de un bloque de codigo: para partirlo por lineas sin
  // perder el resaltado de sintaxis.
  function piecesOf(code) {
    var list = [];
    (function walk(node) {
      for (var i = 0; i < node.childNodes.length; i++) {
        var child = node.childNodes[i];
        if (child.nodeType === 3) list.push({ parent: node, node: child });
        else walk(child);
      }
    })(code);
    return list;
  }

  function countLines(pieces) {
    var n = 0;
    for (var i = 0; i < pieces.length; i++) {
      var text = pieces[i].node.textContent;
      for (var c = 0; c < text.length; c++) if (text.charAt(c) === '\\n') n++;
    }
    return n;
  }

  // Deja solo las primeras k lineas (las piezas siguientes quedan vacias).
  function truncateAt(pieces, k) {
    var seen = 0;
    for (var i = 0; i < pieces.length; i++) {
      var piece = pieces[i];
      var text = piece.node.textContent;
      var nl = 0;
      for (var c = 0; c < text.length; c++) if (text.charAt(c) === '\\n') nl++;
      if (seen + nl <= k) {
        seen += nl;
        if (seen === k) {
          for (var j = i + 1; j < pieces.length; j++) pieces[j].node.textContent = '';
          return { index: i + 1, tail: '' };
        }
        continue;
      }
      var cut = -1, cnt = 0;
      for (var c2 = 0; c2 < text.length; c2++) {
        if (text.charAt(c2) === '\\n') {
          cnt++;
          if (seen + cnt === k) { cut = c2 + 1; break; }
        }
      }
      if (cut < 0) cut = text.length;
      var tail = text.slice(cut);
      piece.node.textContent = text.slice(0, cut);
      for (var j2 = i + 1; j2 < pieces.length; j2++) pieces[j2].node.textContent = '';
      return { index: i, tail: tail };
    }
    return { index: pieces.length, tail: '' };
  }

  // Un bloque de codigo que no entra ni en una hoja vacia se parte por
  // lineas: la mitad entra en esta hoja y el resto sigue en la siguiente.
  // El bloque tiene que estar en la hoja mientras se mide.
  function splitCode(block, body) {
    var pre = block.querySelector('pre');
    if (!pre) return null;
    var code = pre.querySelector('code') || pre;
    var pieces = piecesOf(code);
    var total = countLines(pieces);
    if (total < 4) return null;

    var saved = pieces.map(function (piece) { return piece.node.textContent; });
    var restore = function () {
      pieces.forEach(function (piece, i) { piece.node.textContent = saved[i]; });
    };

    var lo = 1, hi = total - 1, best = 0;
    while (lo <= hi) {
      var mid = (lo + hi) >> 1;
      restore();
      truncateAt(pieces, mid);
      if (fits(body)) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
    }
    restore();
    if (!best) return null;

    var clone = pre.cloneNode(true);
    var cloneCode = clone.querySelector('code') || clone;
    var clonePieces = piecesOf(cloneCode);
    var cut = truncateAt(pieces, best);
    for (var i = 0; i < cut.index && clonePieces[i]; i++) {
      if (clonePieces[i].parent) clonePieces[i].parent.removeChild(clonePieces[i].node);
    }
    if (cut.tail && clonePieces[cut.index]) clonePieces[cut.index].node.textContent = cut.tail;

    var rest = block.cloneNode(true);
    var restPre = rest.querySelector('pre');
    restPre.parentNode.replaceChild(clone, restPre);

    // La prueba de que al partir no se pierde ni una coma: lo que queda en la
    // hoja mas lo que sigue tiene que ser el bloque entero.
    var headText = pieces.map(function (piece) { return piece.node.textContent; }).join('');
    var tailText = clonePieces.slice(cut.index).map(function (piece) { return piece.node.textContent; }).join('');
    if (headText + tailText !== saved.join('')) {
      console.log('VOLTERP: ALERTA - un bloque partido no conserva el texto');
    }
    return { tail: rest };
  }

  // Una tabla que no entra se parte por filas (el encabezado se repite).
  function splitTable(block, body) {
    var table = block.querySelector('table');
    var tbody = table && table.querySelector('tbody');
    if (!tbody) return null;
    var rows = Array.prototype.slice.call(tbody.children);
    if (rows.length < 4) return null;

    var setRows = function (n) {
      while (tbody.firstChild) tbody.removeChild(tbody.firstChild);
      for (var i = 0; i < n; i++) tbody.appendChild(rows[i]);
    };

    var lo = 2, hi = rows.length - 1, best = 0;
    while (lo <= hi) {
      var mid = (lo + hi) >> 1;
      setRows(mid);
      if (fits(body)) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
    }
    setRows(rows.length);
    if (!best) return null;

    setRows(best);
    var rest = block.cloneNode(true);
    var restTbody = rest.querySelector('tbody');
    while (restTbody.firstChild) restTbody.removeChild(restTbody.firstChild);
    for (var i = best; i < rows.length; i++) restTbody.appendChild(rows[i]);
    return { tail: rest };
  }

  // Un bloque con varias partes (una lista, una secuencia de parrafos) se
  // parte por partes: la mitad entra y el resto sigue en la hoja siguiente.
  function splitParts(block, body, depth) {
    depth = depth || 0;
    var kids = Array.from(block.childNodes);
    if (kids.length < 1) return null;

    if (kids.length === 1 && kids[0].nodeType === 1 && depth < 4) {
      // El bloque tiene una sola parte: se parte esa parte (una lista, un
      // recuadro con muchos parrafos).
      var sub = splitParts(kids[0], body, depth + 1);
      if (!sub) return null;
      var rest = block.cloneNode(false);
      rest.appendChild(sub.tail);
      return { tail: rest };
    }

    if (kids.length < 2) return null;

    var setKids = function (n) {
      while (block.firstChild) block.removeChild(block.firstChild);
      for (var i = 0; i < n; i++) block.appendChild(kids[i]);
    };

    var lo = 1, hi = kids.length - 1, best = 0;
    while (lo <= hi) {
      var mid = (lo + hi) >> 1;
      setKids(mid);
      if (fits(body)) { best = mid; lo = mid + 1; } else { hi = mid - 1; }
    }
    setKids(kids.length);
    if (!best) return null;

    setKids(best);
    var rest2 = block.cloneNode(false);
    for (var i = best; i < kids.length; i++) rest2.appendChild(kids[i]);
    return { tail: rest2 };
  }

  // Reparte los bloques en hojas: el que no entra abre la hoja siguiente. Un
  // bloque que no entra ni en una hoja vacia se parte (codigo, tabla o
  // bloque de partes) o se deja desbordar (lo demas: no se pierde contenido).
  function compose(nodes, descriptorFor, label) {
    var sheets = [];
    var i = 0;
    var spilled = 0;
    var split = 0;
    while (i < nodes.length) {
      var sheet = newSheet(descriptorFor(nodes[i]));
      var body = sheet.querySelector('.page-body');
      while (i < nodes.length) {
        var node = nodes[i];

        // Un titulo de seccion abre hoja nueva (como en la casa).
        if (node.dataset.opens === '1' && body.children.length) break;

        body.appendChild(node);
        if (fits(body)) { i++; continue; }

        // No entra: se parte para que entre.
        var parts = splitCode(node, body) || splitTable(node, body) || splitParts(node, body);
        if (parts) {
          nodes.splice(i + 1, 0, parts.tail);
          i++;
          split++;
          continue;
        }

        body.removeChild(node);
        if (body.children.length === 0) {
          body.appendChild(node);
          body.style.overflow = 'visible';
          sheet.style.height = 'auto';
          sheet.style.minHeight = '297mm';
          spilled++;
          console.log('VOLTERP: desborda <' + node.tagName.toLowerCase() + '> '
            + (node.textContent || '').trim().slice(0, 70).replace(/\\s+/g, ' '));
          i++;
        }
        break;
      }
      sheets.push(sheet);
    }
    if (split) console.log('VOLTERP: ' + split + ' bloque(s) partidos (' + label + ')');
    if (spilled) console.log('VOLTERP: ' + spilled + ' bloque(s) desbordan la hoja (' + label + ')');
    return sheets;
  }

  var contentNodes = Array.prototype.slice.call(content.children);
  var indexNodes = indexHost ? Array.prototype.slice.call(indexHost.children) : [];

  // 1. El contenido, hoja por hoja.
  var contentSheets = compose(contentNodes, function (node) { return node.dataset.sec || ''; }, 'contenido');

  // En que hoja cae cada seccion: de ahi sale el numero de pagina del indice.
  var sheetOf = {};
  contentSheets.forEach(function (sheet, sheetIndex) {
    Array.prototype.slice.call(sheet.querySelector('.page-body').children).forEach(function (node) {
      if (node.dataset.outline !== undefined) sheetOf[node.dataset.outline] = sheetIndex;
    });
  });

  // 2. El indice, hoja por hoja.
  var indexSheets = indexNodes.length
    ? compose(indexNodes, function () { return 'Tabla de contenidos'; }, 'indice')
    : [];

  // 3. Los numeros: la portada es la 1, el indice va detras y el contenido al
  //    final. El total de hojas lo define el contenido.
  var offset = 1 + indexSheets.length;
  var total = offset + contentSheets.length;

  function pad(n) { return (n < 10 ? '0' : '') + n; }

  indexSheets.forEach(function (sheet, i) {
    if (withNumbers) sheet.querySelector('.pn').textContent = pad(2 + i);
  });
  contentSheets.forEach(function (sheet, i) {
    if (withNumbers) sheet.querySelector('.pn').textContent = pad(offset + 1 + i);
  });
  indexNodes.forEach(function (node) {
    var slot = node.querySelector('.toc-p');
    if (!slot) return;
    var at = sheetOf[node.dataset.outline];
    slot.textContent = at === undefined ? '' : 'p. ' + pad(offset + 1 + at);
  });

  // 4. El orden final: portada, indice, contenido.
  indexSheets.concat(contentSheets).forEach(function (sheet) {
    document.body.appendChild(sheet);
  });
  stage.remove();
  content.remove();
  if (indexHost) indexHost.remove();

  // 5. La portada cuenta las paginas ella misma: el dato no se escribe a
  //    mano (un numero viejo en la portada es peor que no ponerlo).
  var totalPages = 1 + indexSheets.length + contentSheets.length;
  var stats = document.querySelectorAll('.cover-stat');
  for (var s = 0; s < stats.length; s++) {
    var label = (stats[s].dataset.metric || '').toLowerCase();
    if (label.charAt(0) === 'p' && label.indexOf('gina') >= 0) {
      var num = stats[s].querySelector('.num');
      if (num) num.textContent = String(totalPages);
    }
  }

  console.log('VOLTERP: hojas ' + totalPages + ' de ' + total);
  document.body.dataset.composed = '1';
})();
`;

function renderDocument({ blocks, outline }, meta, css) {
  // El título del documento (el primer # del markdown) es el de la portada:
  // si se queda en el cuerpo, el PDF repite el título dos veces.
  const body = blocks[0]?.type === 'heading' && blocks[0].level === 1 ? blocks.slice(1) : blocks;

  // El título con la parte en degradado (la de la casa).
  const titleParts = meta.titleAccent
    ? (() => {
        const at = meta.title.indexOf(meta.titleAccent);
        if (at < 0) return escapeHtml(meta.title);
        return escapeHtml(meta.title.slice(0, at))
          + `<span class="grad">${escapeHtml(meta.titleAccent)}</span>`
          + escapeHtml(meta.title.slice(at + meta.titleAccent.length));
      })()
    : escapeHtml(meta.title);

  const stats = (meta.metrics ?? []).map(metric => `
      <div class="cover-stat" data-metric="${escapeHtml(metric.label)}">
        <div class="num">${escapeHtml(metric.value)}</div>
        <div class="lbl">${escapeHtml(metric.label)}</div>
      </div>`).join('');
  const cover = `
  <div class="page cover">
    <div class="cover-tag">${escapeHtml(meta.kicker ?? 'Volterp')}</div>
    <div class="cover-title">${titleParts}</div>
    ${meta.subtitle ? `<div class="cover-sub">${escapeHtml(meta.subtitle)}</div>` : ''}
    ${stats ? `<div class="cover-stats">${stats}</div>` : ''}
    <div class="cover-foot">
      <span>${escapeHtml(meta.footer ?? '')}</span>
      <span>${escapeHtml(meta.kind ?? 'Documento técnico')} · ${escapeHtml(meta.dateLabel ?? meta.date ?? '')}</span>
    </div>
  </div>`;

  // El indice: cada entrada es un bloque que el generador reparte en hojas y
  // al que le agrega el numero de pagina donde cae la seccion.
  const indexBlocks = outline.length && meta.index !== false
    ? `<div class="blk"><h2 class="index-title">Índice</h2></div>\n`
      + outline.map((entry, index) => `
      <div class="blk toc-entry" data-outline="${index}">
        <div class="${entry.level === 1 ? 'toc-parte' : 'toc-seccion lvl-' + entry.level}">
          ${entry.number ? `<span class="n">${escapeHtml(entry.number)}</span>` : ''}
          <a href="#${entry.id}">${inline(entry.text)}</a>
          <span class="toc-p"></span>
        </div>
      </div>`).join('')
    : '';

  // El contenido: cada bloque lleva la seccion a la que pertenece (para el
  // encabezado corrido de su hoja) y, si es un titulo, la entrada del indice
  // que le corresponde (para saber en que pagina cae).
  let section = '';
  const contentBlocks = body.map(block => {
    if (block.type === 'heading' && block.level <= 2) section = block.text;
    const at = block.type === 'heading' ? outline.findIndex(entry => entry.id === block.id) : -1;
    // Los titulos de seccion abren hoja nueva (como en la casa).
    const opens = block.type === 'heading' && block.level <= 2 ? ' data-opens="1"' : '';
    return `<div class="blk" data-sec="${escapeHtml(section)}"`
      + (at >= 0 ? ` data-outline="${at}"` : '')
      + opens
      + `>${renderBlock(block)}</div>`;
  }).join('\n');

  // El encabezado corrido lleva la marca del documento a la izquierda.
  const brand = meta.title ?? '';

  return `<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="UTF-8">
<title>${escapeHtml(meta.title ?? 'Documento')}</title>
<style>
${css}
</style>
</head>
<body>
${cover}
<div id="index-blocks"${indexBlocks ? '' : ' hidden'}>${indexBlocks}</div>
<div id="content">
${contentBlocks}
</div>
<span id="brand" hidden>${escapeHtml(brand)}</span>
<span id="foot-text" hidden>${escapeHtml(meta.footer ?? '')}</span>
${meta.pageNumbers === false ? '' : '<span id="numbers" hidden></span>'}
<script type="application/json" id="outline-data">${JSON.stringify(outline)}</script>
<script>
${COMPOSE_JS}
</script>
</body>
</html>
`;
}

// ---------------------------------------------------------------------------
// La conversión: Chrome por CDP
// ---------------------------------------------------------------------------

const CHROME_CANDIDATES = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe'
];

function findChrome() {
  if (process.env.CHROME_PATH) return process.env.CHROME_PATH;
  for (const candidate of CHROME_CANDIDATES) if (existsSync(candidate)) return candidate;
  throw new Error('No se encontro Chrome ni Edge. Se puede indicar con CHROME_PATH.');
}

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

// Playwright primero (es la via que recomienda la skill styled-pdf); si no esta
// en la maquina, el conversor propio habla con el Chrome por CDP y no depende
// de nada instalado.
//
// El harness pisa USERPROFILE, HOME y LOCALAPPDATA, asi que la instalacion se
// busca por donde esta de verdad: el cache de npx de los perfiles de C:.
async function resolvePlaywright() {
  if (process.env.PLAYWRIGHT_PATH && existsSync(process.env.PLAYWRIGHT_PATH))
    return { module: process.env.PLAYWRIGHT_PATH, browsers: null };

  const local = join(process.cwd(), 'node_modules', 'playwright');
  if (existsSync(local)) return { module: local, browsers: null };

  const roots = [];
  for (const variable of [process.env.USERPROFILE, process.env.LOCALAPPDATA, process.env.HOME]) {
    if (!variable) continue;
    roots.push(join(variable, 'AppData', 'Local', 'npm-cache', '_npx'));
  }
  if (existsSync('C:/Users')) {
    for (const user of readdirSync('C:/Users')) {
      roots.push(join('C:/Users', user, 'AppData', 'Local', 'npm-cache', '_npx'));
      roots.push(join('C:/Users', user, 'AppData', 'Local', 'ms-playwright'));
    }
  }

  let browsers = null;
  for (const root of roots) {
    if (!existsSync(root)) continue;
    if (root.endsWith('ms-playwright')) { browsers ??= root; continue; }
    const candidates = readdirSync(root)
      .map(name => join(root, name, 'node_modules', 'playwright'))
      .filter(path => existsSync(path))
      .sort();
    if (candidates.length) return { module: candidates[candidates.length - 1], browsers };
  }

  return browsers ? { module: null, browsers } : null;
}

async function printWithPlaywright(playwrightPath, htmlPath, pdfPath, meta) {
  const { chromium } = require(playwrightPath);

  // El navegador que trae la libreria puede no estar en la maquina (la
  // version del cache pide otra revision del chromium). En vez de descargar
  // nada, Playwright conduce el Chrome que ya esta instalado.
  const executablePath = CHROME_CANDIDATES.find(candidate => existsSync(candidate));

  const browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {})
  });
  try {
    const page = await browser.newPage();
    // El generador de hojas avisa por consola (bloques partidos, desbordes).
    page.on('console', message => console.log(`  ${message.text()}`));
    page.on('pageerror', error => console.error(`  ERROR en la pagina: ${error.message}`));
    await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle' });

    // Si el documento no quedo compuesto en hojas, el PDF no sirve: se para
    // aca en vez de dejar un documento a medio hacer.
    const composed = await page.evaluate(() => document.body.dataset.composed === '1');
    if (!composed) throw new Error('las hojas no se compusieron (revisa el error del navegador).');
    await page.pdf({
      path: pdfPath,
      printBackground: true,
      // El tamano lo dice el `@page` de la maqueta (A4 exacto). Chrome no
      // acepta `paperWidth`: las medidas en pulgadas de la pagina ficam en
      // Letter y el PDF sale descuadrado.
      preferCSSPageSize: true,
      // El encabezado y el pie los lleva cada hoja (van dentro, no en el
      // margen del papel): asi no pisan el texto ni salen en la portada.
      displayHeaderFooter: false
    });
  } finally {
    await browser.close();
  }
  return 'playwright';
}

async function printWithChrome(htmlPath, pdfPath, meta) {
  const userDataDir = join(tmpdir(), `volterp-pdf-${Date.now()}`);
  mkdirSync(userDataDir, { recursive: true });

  const chrome = spawn(findChrome(), [
    '--headless=new',
    '--disable-gpu',
    '--no-sandbox',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-extensions',
    '--hide-scrollbars',
    '--force-device-scale-factor=1',
    `--user-data-dir=${userDataDir}`,
    '--remote-debugging-port=0',
    pathToFileURL(htmlPath).href
  ], { stdio: 'ignore' });

  try {
    const portFile = join(userDataDir, 'DevToolsActivePort');
    let port = null;
    for (let attempt = 0; attempt < 150 && !port; attempt++) {
      await sleep(100);
      if (!existsSync(portFile)) continue;
      const [value] = readFileSync(portFile, 'utf8').split('\n');
      if (value && value.trim()) port = value.trim();
    }
    if (!port) throw new Error('Chrome no abrio el puerto de depuracion.');

    let target = null;
    for (let attempt = 0; attempt < 100 && !target; attempt++) {
      const response = await fetch(`http://127.0.0.1:${port}/json/list`);
      const targets = await response.json();
      target = targets.find(entry => entry.type === 'page');
      if (!target) await sleep(100);
    }
    if (!target) throw new Error('Chrome no abrio la pagina del documento.');

    const socket = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
      socket.addEventListener('open', resolve, { once: true });
      socket.addEventListener('error', reject, { once: true });
    });

    let nextId = 0;
    const pending = new Map();
    socket.addEventListener('message', event => {
      const message = JSON.parse(event.data);
      if (message.id && pending.has(message.id)) {
        const { resolve, reject } = pending.get(message.id);
        pending.delete(message.id);
        message.error ? reject(new Error(message.error.message)) : resolve(message.result);
      }
    });

    const send = (method, params = {}) => new Promise((resolve, reject) => {
      const id = ++nextId;
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });

    await send('Page.enable');
    await sleep(600); // las fuentes y el layout se asientan

    const { data } = await send('Page.printToPDF', {
      printBackground: true,
      // El tamano y los margenes van en el `@page` de la maqueta (A4 exacto y
      // sin margenes: el aire lo lleva cada hoja). El encabezado y el pie van
      // dentro de la hoja, asi que Chrome no dibuja nada encima.
      preferCSSPageSize: true,
      displayHeaderFooter: false,
      marginTop: 0,
      marginBottom: 0,
      marginLeft: 0,
      marginRight: 0
    });

    writeFileSync(pdfPath, Buffer.from(data, 'base64'));
    socket.close();
  } finally {
    chrome.kill();
    await sleep(200);
    rmSync(userDataDir, { recursive: true, force: true });
  }
}

// ---------------------------------------------------------------------------
// El main
// ---------------------------------------------------------------------------

const markdown = readFileSync(sourcePath, 'utf8');
const parsed = parseMarkdown(markdown);
const css = readFileSync(maquetaPath, 'utf8');
const markdownTitle = parsed.blocks[0]?.type === 'heading' && parsed.blocks[0].level === 1
  ? parsed.blocks[0].text
  : '';

const metrics = (Array.isArray(args.metric) ? args.metric : args.metric ? [args.metric] : [])
  .map(entry => {
    const [value, ...rest] = String(entry).split('|');
    return { value: value.trim(), label: rest.join('|').trim() };
  });

const MONTHS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

// La fecha en el pie de la portada: "Sept 2026".
function dateLabel(iso) {
  const parts = /^(\d{4})-(\d{2})/.exec(iso ?? '');
  if (!parts) return iso ?? '';
  const month = MONTHS[Number(parts[2]) - 1];
  return month ? `${month} ${parts[1]}` : iso;
}

const meta = {
  kicker: args.kicker ?? 'Volterp',
  title: args.title ?? markdownTitle ?? sourcePath.split(/[\\/]/).pop().replace(/\.md$/i, ''),
  titleAccent: args.accent ?? '',
  subtitle: args.subtitle ?? '',
  repo: args.repo ?? '',
  commit: args.commit ?? '',
  date: args.date ?? new Date().toISOString().slice(0, 10),
  dateLabel: dateLabel(args.date ?? new Date().toISOString().slice(0, 10)),
  kind: args.kind ?? 'Documento técnico',
  metrics,
  footer: args.footer ?? (args.repo ? `Volterp · ${args.repo}` : 'Volterp'),
  pageNumbers: args.numbers !== false && !args['no-numbers'],
  index: args.index !== false && !args['no-index']
};

const html = renderDocument(parsed, meta, css);
mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(htmlPath, html, 'utf8');

async function printToPdf(htmlPath, pdfPath, meta) {
  const found = await resolvePlaywright();

  if (found?.module) {
    try {
      if (found.browsers && !process.env.PLAYWRIGHT_BROWSERS_PATH)
        process.env.PLAYWRIGHT_BROWSERS_PATH = found.browsers;
      await printWithPlaywright(found.module, htmlPath, pdfPath, meta);
      console.log('Convertidor  playwright');
      return;
    } catch (error) {
      console.warn(`Playwright no pudo convertir (${error.message}); se usa el Chrome por CDP.`);
    }
  }

  await printWithChrome(htmlPath, pdfPath, meta);
  console.log('Convertidor  chrome por CDP');
}

await printToPdf(htmlPath, outputPath, meta);
const size = readFileSync(outputPath).length;
console.log(`PDF   ${outputPath}`);
console.log(`HTML  ${htmlPath}`);
console.log(`Secciones ${parsed.outline.filter(entry => entry.level <= 2).length} · bloques ${parsed.blocks.length} · ${Math.round(size / 1024)} KB`);
