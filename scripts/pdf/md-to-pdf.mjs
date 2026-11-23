#!/usr/bin/env node
// El pipeline de los PDF del proyecto: markdown → HTML → PDF.
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

import { readFileSync, writeFileSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { spawn } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { pathToFileURL } from 'node:url';

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
    if (next === undefined || next.startsWith('--')) args[key] = true;
    else { args[key] = next; i++; }
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
      if (level === 2) {
        section += 1;
        outline.push({ level, text, id, number: section });
      } else if (level === 3) {
        outline.push({ level, text, id });
      }
      blocks.push({ type: 'heading', level, text, id });
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
      blocks.push({ type: 'list', items });
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
      return `<${tag} id="${block.id}">${inline(block.text)}</${tag}>`;
    }
    case 'code':
      return `<pre><code>${highlight(block.code, block.lang)}</code></pre>`;
    case 'table': {
      const head = block.header.map(cell => `<th>${inline(cell)}</th>`).join('');
      const body = block.rows
        .map(row => `<tr>${row.map(cell => `<td>${inline(cell)}</td>`).join('')}</tr>`)
        .join('');
      return `<table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>`;
    }
    case 'quote':
      return `<blockquote><p>${inline(block.text)}</p></blockquote>`;
    case 'hr':
      return '<hr>';
    case 'list':
      return renderList(block.items);
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

function renderDocument({ blocks, outline }, meta, css) {
  // El título del documento (el primer # del markdown) es el de la portada:
  // si se queda en el cuerpo, el PDF repite el título dos veces.
  const body = blocks[0]?.type === 'heading' && blocks[0].level === 1 ? blocks.slice(1) : blocks;
  const cover = `
  <div class="cover">
    <div class="kicker">${escapeHtml(meta.kicker ?? 'Volterp')}</div>
    <div class="rule"></div>
    <h1>${escapeHtml(meta.title ?? '')}</h1>
    ${meta.subtitle ? `<div class="subtitle">${escapeHtml(meta.subtitle)}</div>` : ''}
    <div class="meta">
      ${meta.repo ? `<span><strong>Origen</strong> ${escapeHtml(meta.repo)}</span>` : ''}
      ${meta.commit ? `<span><strong>Commit</strong> ${escapeHtml(meta.commit)}</span>` : ''}
      ${meta.date ? `<span><strong>Fecha</strong> ${escapeHtml(meta.date)}</span>` : ''}
    </div>
  </div>`;

  const toc = outline.length && meta.index !== false ? `
  <div class="index">
    <h2 id="indice" style="break-before:auto">Índice</h2>
    <div class="toc">
      <ol>
        ${outline
          .filter(entry => entry.level === 2)
          .map(entry => `<li><a href="#${entry.id}">${inline(entry.text)}</a></li>`)
          .join('\n        ')}
      </ol>
    </div>
  </div>` : '';

  const content = body.map(renderBlock).join('\n');

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
${toc}
${content}
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

async function printToPdf(htmlPath, pdfPath, meta) {
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

    const footer = `
      <div style="font-size:7pt;width:100%;padding:0 15mm 5mm;color:#94a3b8;
                  display:flex;justify-content:space-between;font-family:Inter,sans-serif;">
        <span>${escapeHtml(meta.title ?? '')}</span>
        <span>Página <span class="pageNumber"></span> de <span class="totalPages"></span></span>
      </div>`;

    const { data } = await send('Page.printToPDF', {
      printBackground: true,
      preferCSSPageSize: true,
      displayHeaderFooter: meta.pageNumbers !== false,
      headerTemplate: '<div></div>',
      footerTemplate: meta.pageNumbers === false ? '<div></div>' : footer,
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

const meta = {
  kicker: args.kicker ?? 'Volterp',
  title: args.title ?? markdownTitle ?? sourcePath.split(/[\\/]/).pop().replace(/\.md$/i, ''),
  subtitle: args.subtitle ?? '',
  repo: args.repo ?? '',
  commit: args.commit ?? '',
  date: args.date ?? new Date().toISOString().slice(0, 10),
  pageNumbers: args.numbers !== false && !args['no-numbers'],
  index: args.index !== false && !args['no-index']
};

const html = renderDocument(parsed, meta, css);
mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(htmlPath, html, 'utf8');

await printToPdf(htmlPath, outputPath, meta);

const size = readFileSync(outputPath).length;
console.log(`PDF   ${outputPath}`);
console.log(`HTML  ${htmlPath}`);
console.log(`Secciones ${parsed.outline.filter(entry => entry.level === 2).length} · bloques ${parsed.blocks.length} · ${Math.round(size / 1024)} KB`);
