// Checks internal links and pictures of the manuals. Exit code 1 when something is broken.
// Usage: node docs/manuals/tools/check-links.mjs [--images]   (--images also fails on missing picture files)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const docs = path.join(root, 'docs');
const needImages = process.argv.includes('--images');
const norm = (p) => path.normalize(p).split(path.sep).join('/');
const images = new Set();
let broken = 0;
let missingImages = 0;

function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith('.md')) check(p);
  }
}
function check(file) {
  const text = fs.readFileSync(file, 'utf8');
  for (const m of text.matchAll(/\]\(([^)#\s]+)(#[^)]*)?\)/g)) {
    const link = m[1];
    if (/^(https?:|mailto:)/.test(link)) continue;
    const target = path.join(path.dirname(file), link);
    if (/\.(png|jpe?g|gif|svg)$/i.test(link)) {
      images.add(norm(target));
      if (!fs.existsSync(target)) { missingImages++; if (needImages) console.log('MISSING IMAGE', norm(path.relative(docs, file)), '->', link); }
    } else if (!fs.existsSync(target)) { broken++; console.log('BROKEN LINK', norm(path.relative(docs, file)), '->', link); }
  }
}
walk(docs);

const listed = new Set();
const shotsDir = path.join(root, 'shots');
for (const f of fs.readdirSync(shotsDir).filter((x) => x.endsWith('.json'))) {
  for (const s of JSON.parse(fs.readFileSync(path.join(shotsDir, f), 'utf8'))) listed.add(norm(path.join(docs, s.file)));
}
const notListed = [...images].filter((i) => !listed.has(i));
const notUsed = [...listed].filter((i) => !images.has(i));
for (const i of notListed) console.log('PICTURE WITHOUT A SHOT ENTRY', norm(path.relative(docs, i)));
for (const i of notUsed) console.log('SHOT ENTRY NOT USED', norm(path.relative(docs, i)));
console.log(`broken links: ${broken}, pictures referenced: ${images.size}, missing files: ${missingImages}, without shot entry: ${notListed.length}, unused shot entries: ${notUsed.length}`);
process.exit(broken || notListed.length || (needImages && missingImages) ? 1 : 0);
