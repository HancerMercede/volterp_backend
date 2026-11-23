#!/usr/bin/env node
// Las capturas de las pantallas, para el manual del usuario.
//
//   node scripts/pdf/capturas.mjs --base http://localhost:5173 --out <carpeta>
//                                 [--user demo.admin1] [--pass Demo123!]
//
// Se loguea con un usuario de prueba, recorre las pantallas de la app y deja
// una imagen de cada una. El manual las usa como imágenes: son la foto de una
// fecha, y el script es lo que las regenera.
//
// El front tiene que estar corriendo (npm run dev) y el API tambien: la app
// pide los datos de verdad.

import { existsSync, mkdirSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    const token = argv[i];
    if (!token.startsWith('--')) continue;
    const next = argv[i + 1];
    if (next === undefined || next.startsWith('--')) args[token.slice(2)] = true;
    else { args[token.slice(2)] = next; i++; }
  }
  return args;
}

const args = parseArgs(process.argv.slice(2));

const base = String(args.base ?? 'http://localhost:5173').replace(/\/$/, '');
const outputDir = resolve(String(args.out ?? join(process.cwd(), 'docs', 'manuales', 'capturas')));
const user = String(args.user ?? 'demo.admin1');
const password = String(args.pass ?? 'Demo123!');

// Las pantallas: el nombre del archivo, la ruta y el titulo que lleva el
// manual. El orden es el del menu de la app.
const screens = [
  { slug: '01-dashboard', ruta: '/', titulo: 'Dashboard' },
  { slug: '02-ventas', ruta: '/ventas', titulo: 'Ventas' },
  { slug: '03-caja-nueva-venta', ruta: '/nueva-venta', titulo: 'La caja: nueva venta' },
  { slug: '04-compras', ruta: '/compras', titulo: 'Compras' },
  { slug: '05-inventario', ruta: '/inventario', titulo: 'Inventario' },
  { slug: '06-clientes', ruta: '/clientes', titulo: 'Clientes' },
  { slug: '07-proveedores', ruta: '/proveedores', titulo: 'Proveedores' },
  { slug: '08-contabilidad', ruta: '/contabilidad', titulo: 'Contabilidad' },
  { slug: '09-cuentas', ruta: '/contabilidad/cuentas', titulo: 'Cuentas contables' },
  { slug: '10-cuentas-por-cobrar', ruta: '/contabilidad/cxc', titulo: 'Cuentas por cobrar' },
  { slug: '11-cuentas-por-pagar', ruta: '/contabilidad/cxp', titulo: 'Cuentas por pagar' },
  { slug: '12-rrhh', ruta: '/rrhh', titulo: 'Recursos humanos' },
  { slug: '13-nomina', ruta: '/rrhh/nomina', titulo: 'Nómina' },
  { slug: '14-asistencia', ruta: '/rrhh/asistencia', titulo: 'Asistencia' },
  { slug: '15-proyectos', ruta: '/proyectos', titulo: 'Proyectos' },
  { slug: '16-reportes', ruta: '/reportes', titulo: 'Reportes' },
  { slug: '17-facturacion', ruta: '/facturacion', titulo: 'Facturación electrónica' },
  { slug: '18-emisores', ruta: '/facturacion/emisores', titulo: 'Emisores' },
  { slug: '19-rangos', ruta: '/facturacion/rangos', titulo: 'Rangos de e-NCF' },
  { slug: '20-certificados', ruta: '/facturacion/certificados', titulo: 'Certificados' },
  { slug: '21-comprobantes', ruta: '/facturacion/comprobantes', titulo: 'Comprobantes' },
  { slug: '22-recepcion', ruta: '/facturacion/recepcion', titulo: 'Recepción' },
  { slug: '23-configuracion-fiscal', ruta: '/facturacion/configuracion', titulo: 'Configuración fiscal' },
  { slug: '24-configuracion', ruta: '/configuracion', titulo: 'Configuración' },
  { slug: '25-soporte', ruta: '/soporte', titulo: 'Soporte' }
];

// El harness pisa USERPROFILE, HOME y LOCALAPPDATA, asi que la instalacion se
// busca por donde esta de verdad: el cache de npx de los perfiles de C:.
function resolvePlaywright() {
  if (process.env.PLAYWRIGHT_PATH && existsSync(process.env.PLAYWRIGHT_PATH))
    return process.env.PLAYWRIGHT_PATH;

  const local = join(process.cwd(), 'node_modules', 'playwright');
  if (existsSync(local)) return local;

  const roots = [];
  if (existsSync('C:/Users')) {
    for (const user of readdirSync('C:/Users'))
      roots.push(join('C:/Users', user, 'AppData', 'Local', 'npm-cache', '_npx'));
  }

  for (const root of roots) {
    if (!existsSync(root)) continue;
    const candidates = readdirSync(root)
      .map(name => join(root, name, 'node_modules', 'playwright'))
      .filter(path => existsSync(path))
      .sort();
    if (candidates.length) return candidates[candidates.length - 1];
  }

  return null;
}

const CHROME = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
];

const playwrightPath = resolvePlaywright();
if (!playwrightPath) {
  console.error('No se encontro Playwright. Se puede indicar con PLAYWRIGHT_PATH.');
  process.exit(2);
}

const { chromium } = require(playwrightPath);
const executablePath = CHROME.find(candidate => existsSync(candidate));

mkdirSync(outputDir, { recursive: true });

const browser = await chromium.launch({
  headless: true,
  ...(executablePath ? { executablePath } : {})
});

const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  locale: 'es-DO'
});

const page = await context.newPage();

console.log(`Entro a ${base}`);
await page.goto(`${base}/login`, { waitUntil: 'networkidle' });

// El login: el campo del usuario y el de la clave.
const userField = page.locator('input[name="email"], input[name="username"], input[type="email"], input[type="text"]').first();
const passField = page.locator('input[type="password"]').first();

await userField.waitFor({ state: 'visible', timeout: 30000 });
await userField.fill(user);
await passField.fill(password);
await page.locator('button[type="submit"], form button').first().click();

await page.waitForURL(url => !url.pathname.includes('/login'), { timeout: 30000 });
console.log(`Sesion de ${user} iniciada`);

for (const screen of screens) {
  const path = join(outputDir, `${screen.slug}.png`);

  try {
    await page.goto(`${base}${screen.ruta}`, { waitUntil: 'networkidle', timeout: 45000 });
    // La app carga los datos despues de pintar: un respiro antes de la foto.
    await page.waitForTimeout(2200);
    await page.screenshot({ path, fullPage: false });
    console.log(`OK   ${screen.slug}  ${screen.titulo}`);
  } catch (error) {
    console.log(`FALLA ${screen.slug}  ${screen.titulo}: ${error.message.split('\n')[0]}`);
  }
}

await browser.close();
console.log(`Capturas en ${outputDir}`);
