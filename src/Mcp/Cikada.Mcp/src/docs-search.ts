// In-process lexical search over the manual chunks: BM25 on folded tokens, a title / heading boost, and a light prefix match that tolerates Lithuanian
// word endings. No dependencies, no backend call.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { DocChunk, DocsIndex } from './docs-chunker.js';

const K1 = 1.2;
const B = 0.75;
const HEAD_BOOST = 3;
const MIN_PREFIX = 5;
const PREFIX_WEIGHT = 0.4;

/** Lower-case and strip diacritics one character at a time, so the folded text keeps the offsets of the original (ą->a, č->c, ė->e, š->s, ž->z ...). */
export function fold(text: string): string {
  let out = '';
  for (const ch of text) {
    const base = ch.normalize('NFD')[0] ?? ch;
    out += base.toLowerCase()[0] ?? base;
  }
  return out;
}

export const tokenize = (text: string): string[] => fold(text).match(/[a-z0-9]{2,}/g) ?? [];

const commonPrefix = (a: string, b: string): number => {
  let n = 0;
  while (n < a.length && n < b.length && a[n] === b[n]) n++;
  return n;
};

interface Doc {
  chunk: DocChunk;
  body: Map<string, number>;
  head: Map<string, number>;
  length: number;
}

export interface Hit {
  chunk: DocChunk;
  score: number;
  snippet: string;
}

export class DocsSearch {
  readonly docs: Doc[];
  private readonly byId = new Map<string, DocChunk>();
  private readonly df = new Map<string, number>();
  private readonly avgLength: number;

  constructor(readonly index: DocsIndex) {
    this.docs = index.chunks.map((chunk) => {
      this.byId.set(chunk.id, chunk);
      const count = (tokens: string[]): Map<string, number> => {
        const map = new Map<string, number>();
        for (const token of tokens) map.set(token, (map.get(token) ?? 0) + 1);
        return map;
      };
      const bodyTokens = tokenize(chunk.text);
      return { chunk, body: count(bodyTokens), head: count(tokenize([chunk.page_title, ...chunk.heading_path, ...chunk.routes].join(' '))), length: bodyTokens.length };
    });
    for (const doc of this.docs) {
      for (const term of new Set([...doc.body.keys(), ...doc.head.keys()])) this.df.set(term, (this.df.get(term) ?? 0) + 1);
    }
    this.avgLength = this.docs.reduce((sum, doc) => sum + doc.length, 0) / Math.max(1, this.docs.length);
  }

  get chunks(): readonly DocChunk[] {
    return this.index.chunks;
  }

  modules(): string[] {
    return [...new Set(this.index.chunks.map((chunk) => chunk.module))].sort();
  }

  getChunk(id: string): DocChunk | undefined {
    return this.byId.get(id);
  }

  getPage(page: string): DocChunk[] {
    const wanted = page.trim().replace(/\\/g, '/').replace(/\.md$/i, '').replace(/^\/+|\/+$/g, '');
    return this.index.chunks.filter((chunk) => chunk.page === wanted);
  }

  /** Vocabulary terms a query token stands for, with their weight: the token itself, and (5+ characters) terms sharing a 5+ character prefix. */
  private expand(token: string): Map<string, number> {
    const terms = new Map<string, number>();
    if (this.df.has(token)) terms.set(token, 1);
    if (token.length >= MIN_PREFIX) {
      for (const term of this.df.keys()) if (term !== token && term.length >= MIN_PREFIX && commonPrefix(token, term) >= MIN_PREFIX) terms.set(term, PREFIX_WEIGHT);
    }
    return terms;
  }

  search(query: string, options: { module?: string; limit?: number } = {}): { hits: Hit[]; total: number } {
    const tokens = [...new Set(tokenize(query))];
    const module = options.module?.trim().toLowerCase();
    const expanded = tokens.map((token) => this.expand(token));
    const n = this.docs.length;
    const scored: { doc: Doc; score: number }[] = [];
    for (const doc of this.docs) {
      if (module && doc.chunk.module.toLowerCase() !== module) continue;
      let score = 0;
      for (const terms of expanded) {
        for (const [term, weight] of terms) {
          const body = doc.body.get(term) ?? 0;
          const head = doc.head.get(term) ?? 0;
          if (!body && !head) continue;
          const df = this.df.get(term) ?? 1;
          const idf = Math.log(1 + (n - df + 0.5) / (df + 0.5));
          const tf = body / (1 - B + (B * doc.length) / Math.max(1, this.avgLength)) + HEAD_BOOST * head;
          score += weight * idf * ((tf * (K1 + 1)) / (tf + K1));
        }
      }
      if (score > 0) scored.push({ doc, score });
    }
    scored.sort((a, b) => b.score - a.score || a.doc.chunk.id.localeCompare(b.doc.chunk.id));
    const limit = Math.max(1, options.limit ?? 6);
    return { total: scored.length, hits: scored.slice(0, limit).map(({ doc, score }) => ({ chunk: doc.chunk, score, snippet: snippet(doc.chunk.text, tokens) })) };
  }
}

/** About 300 characters around the densest cluster of query words; the start of the text when only the title matched. */
export function snippet(text: string, tokens: string[], size = 300): string {
  const folded = fold(text);
  const matches: number[] = [];
  for (const m of folded.matchAll(/[a-z0-9]{2,}/g)) {
    const word = m[0];
    if (tokens.some((t) => word === t || (t.length >= MIN_PREFIX && word.length >= MIN_PREFIX && commonPrefix(t, word) >= MIN_PREFIX))) matches.push(m.index ?? 0);
  }
  if (!matches.length) return text.length <= size ? text : `${text.slice(0, size).trimEnd()}…`;
  let best = matches[0] as number;
  let bestCount = 0;
  for (const start of matches) {
    const count = matches.filter((p) => p >= start && p < start + size).length;
    if (count > bestCount) {
      bestCount = count;
      best = start;
    }
  }
  let from = Math.max(0, best - 60);
  if (from > 0) {
    const space = text.indexOf(' ', from);
    if (space >= 0 && space < best) from = space + 1;
  }
  const to = Math.min(text.length, from + size);
  return `${from > 0 ? '…' : ''}${text.slice(from, to).trim()}${to < text.length ? '…' : ''}`;
}

/** Where the generated index sits: <package>/data/docs-index.json (dist/src is two levels below the package, in the image and in a checkout). */
export const defaultIndexPath = (): string => process.env.DOCS_INDEX_PATH?.trim() || resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', 'data', 'docs-index.json');

let cached: DocsSearch | null | undefined;

/** The index of the running process, read once; null when the file is missing or unreadable (the tools then say so). */
export function loadDocsSearch(path = defaultIndexPath()): DocsSearch | null {
  if (cached !== undefined) return cached;
  try {
    cached = existsSync(path) ? new DocsSearch(JSON.parse(readFileSync(path, 'utf8')) as DocsIndex) : null;
  } catch {
    cached = null;
  }
  return cached;
}
