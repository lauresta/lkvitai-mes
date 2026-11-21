// Build step (image build / npm run docs:index): chunks the manuals into data/docs-index.json plus data/docs-manifest.json.
//   node dist/src/build-docs-index.js [--docs <dir>] [--out <dir>]
// Defaults: docs = <repo>/docs/manuals/docs, out = <package>/data. The git sha comes from GIT_SHA or `git rev-parse` when available.

import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildIndex } from './docs-chunker.js';

const packageDir = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

function arg(name: string): string | undefined {
  const at = process.argv.indexOf(`--${name}`);
  return at >= 0 ? process.argv[at + 1] : undefined;
}

function gitSha(): string {
  if (process.env.GIT_SHA?.trim()) return process.env.GIT_SHA.trim();
  try {
    return execFileSync('git', ['rev-parse', '--short=12', 'HEAD'], { cwd: packageDir, stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
  } catch {
    return '';
  }
}

const docsDir = resolve(arg('docs') ?? join(packageDir, '..', '..', '..', 'docs', 'manuals', 'docs'));
const outDir = resolve(arg('out') ?? join(packageDir, 'data'));
if (!existsSync(docsDir)) {
  console.error(`manuals folder not found: ${docsDir}`);
  process.exit(1);
}
const index = buildIndex(docsDir, { gitSha: gitSha() });
mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, 'docs-index.json'), JSON.stringify(index));
const { chunks: _chunks, ...manifest } = index;
writeFileSync(join(outDir, 'docs-manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`docs index: ${index.page_count} pages, ${index.chunk_count} chunks -> ${join(outDir, 'docs-index.json')}`);
