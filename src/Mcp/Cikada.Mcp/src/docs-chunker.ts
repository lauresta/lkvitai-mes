// Splits the user manuals (MkDocs markdown, Lithuanian) into chunks for the docs_search / docs_get tools. Pure functions plus one directory walker; the
// index is built at image build time by build-docs-index.ts and never committed.

import { readdirSync, readFileSync } from 'node:fs';
import { join, posix, relative, sep } from 'node:path';

export interface DocChunk {
  /** Stable: "<relative path without .md>#<slug of the heading path>". */
  id: string;
  module: string;
  page: string;
  page_title: string;
  heading_path: string[];
  status: string;
  routes: string[];
  roles: string[];
  /** Path of the page on the MkDocs site plus #anchor. */
  url_path: string;
  text: string;
  /** Screenshots the section shows, as paths under the docs folder ("img/<module>/<name>.png"); docs_image returns them. */
  images?: DocImage[];
}

export interface DocImage {
  path: string;
  alt: string;
}

export interface FrontMatter {
  module?: string;
  title?: string;
  routes: string[];
  roles: string[];
  status?: string;
}

/** A `##` section longer than this is split further at `###`. */
export const MAX_SECTION_CHARS = 1800;

/** The folders of the first manual all belong to the warehouse module. */
const FOLDER_MODULES: Record<string, string> = { procesai: 'warehouse', roles: 'warehouse', 'trikciu-salinimas': 'warehouse' };

function scalar(raw: string): string {
  let value = raw.trim();
  const quote = value[0];
  if (quote === '"' || quote === "'") {
    const end = value.indexOf(quote, 1);
    return end > 0 ? value.slice(1, end) : value.slice(1);
  }
  const hash = value.search(/\s#/);
  if (hash >= 0) value = value.slice(0, hash).trim();
  return value;
}

function list(raw: string): string[] {
  let value = raw.trim();
  const hash = value.lastIndexOf(']');
  if (value.startsWith('[') && hash >= 0) value = value.slice(1, hash);
  else value = scalar(value);
  return value
    .split(',')
    .map((item) => scalar(item))
    .filter((item) => item !== '');
}

/** Reads the optional `---` front matter (a flat subset of YAML: scalars and inline lists) and returns it with the markdown that follows. */
export function parseFrontMatter(source: string): { meta: FrontMatter; body: string } {
  const text = source.replace(/^﻿/, '').replace(/\r\n?/g, '\n');
  const meta: FrontMatter = { routes: [], roles: [] };
  const open = /^---[ \t]*\n/.exec(text);
  if (!open) return { meta, body: text };
  const rest = text.slice(open[0].length);
  const close = /^---[ \t]*$/m.exec(rest);
  if (!close) return { meta, body: text };
  for (const line of rest.slice(0, close.index).split('\n')) {
    const match = /^([A-Za-z_][\w-]*)\s*:\s*(.*)$/.exec(line);
    if (!match) continue;
    const key = match[1] as string;
    const raw = match[2] ?? '';
    if (key === 'routes' || key === 'roles') meta[key] = list(raw);
    else if (key === 'module' || key === 'title' || key === 'status') {
      const value = scalar(raw);
      if (value) meta[key] = value;
    }
  }
  return { meta, body: rest.slice(close.index + close[0].length).replace(/^\n+/, '') };
}

/** The anchor MkDocs / python-markdown gives a heading: ASCII-folded, lower-case, words joined with hyphens. */
export function slugify(text: string): string {
  return text
    .normalize('NFKD')
    .replace(/[^\x00-\x7F]/g, '')
    .replace(/[^\w\s-]/g, '')
    .trim()
    .toLowerCase()
    .replace(/[-\s]+/g, '-');
}

/** Images become their alt text, links their label, HTML comments disappear; (UI: ...) labels and everything else stay as written. */
export function cleanText(markdown: string): string {
  return markdown
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, (_all, alt: string) => alt.trim())
    .replace(/\[([^\]]+)\]\((?!UI:)[^)]*\)/g, '$1')
    .replace(/[ \t]+$/gm, '')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

/** The PNG screenshots a piece of markdown shows, resolved against the page's folder; anything outside img/ is left out. */
export function imagesOf(page: string, markdown: string): DocImage[] {
  const found: DocImage[] = [];
  const pattern = /!\[([^\]]*)\]\(\s*([^)\s]+)[^)]*\)/g;
  for (let match = pattern.exec(markdown); match; match = pattern.exec(markdown)) {
    const path = posix.normalize(posix.join(posix.dirname(page), match[2] as string));
    if (/^img\/[\w-]+\/[\w.-]+\.png$/i.test(path) && !found.some((image) => image.path === path)) found.push({ path, alt: (match[1] as string).trim() });
  }
  return found;
}

interface Heading {
  level: number;
  title: string;
  anchor: string;
}

function parseHeading(line: string): Heading | undefined {
  const match = /^(#{1,6})[ \t]+(.+?)[ \t]*#*[ \t]*$/.exec(line);
  if (!match) return undefined;
  let title = (match[2] as string).trim();
  let anchor = '';
  const attr = /\s*\{#([\w-]+)[^}]*\}\s*$/.exec(title);
  if (attr) {
    anchor = attr[1] as string;
    title = title.slice(0, attr.index).trim();
  }
  title = title.replace(/\[([^\]]+)\]\([^)]*\)/g, '$1').replace(/[*_`]/g, '').trim();
  return { level: (match[1] as string).length, title, anchor: anchor || slugify(title) };
}

interface Section {
  path: Heading[];
  lines: string[];
}

/** Lines to sections at `##` (and `###` for a long `##`); headings inside code fences do not count. */
function sections(lines: string[]): { h1?: Heading; intro: string[]; parts: Section[] } {
  let h1: Heading | undefined;
  const intro: string[] = [];
  const h2s: { heading: Heading; lines: string[] }[] = [];
  let fence = false;
  for (const line of lines) {
    if (/^\s*(```|~~~)/.test(line)) fence = !fence;
    const heading = fence ? undefined : parseHeading(line);
    if (heading && heading.level === 2) h2s.push({ heading, lines: [] });
    else if (heading && heading.level === 1 && !h1 && h2s.length === 0) h1 = heading;
    else (h2s.length ? (h2s[h2s.length - 1] as { lines: string[] }).lines : intro).push(line);
  }
  const parts: Section[] = [];
  for (const h2 of h2s) {
    const body = h2.lines.join('\n');
    if (cleanText(body).length <= MAX_SECTION_CHARS) {
      parts.push({ path: [h2.heading], lines: h2.lines });
      continue;
    }
    let current: Section = { path: [h2.heading], lines: [] };
    const split: Section[] = [current];
    let inFence = false;
    for (const line of h2.lines) {
      if (/^\s*(```|~~~)/.test(line)) inFence = !inFence;
      const heading = inFence ? undefined : parseHeading(line);
      if (heading && heading.level === 3) {
        current = { path: [h2.heading, heading], lines: [] };
        split.push(current);
      } else current.lines.push(line);
    }
    parts.push(...split);
  }
  return { ...(h1 ? { h1 } : {}), intro, parts };
}

const STATUS_LINE = /^\*\*B[uū]sena:?\*\*:?\s*(.+)$/im;

function statusOf(meta: FrontMatter, body: string): string {
  const fromLine = STATUS_LINE.exec(body)?.[1]?.trim().toLowerCase().match(/^[a-ząčęėįšųūž]+/)?.[0];
  return (meta.status ?? fromLine ?? '').toLowerCase();
}

/** MkDocs default (use_directory_urls): "a/b.md" -> "a/b/", "a/index.md" -> "a/", "index.md" -> "". */
export function pageUrlPath(page: string): string {
  const parts = page.split('/');
  if (parts[parts.length - 1] === 'index') parts.pop();
  return parts.length ? `${parts.join('/')}/` : '';
}

export function moduleOf(page: string, meta: FrontMatter): string {
  if (meta.module) return meta.module.toLowerCase();
  const folder = page.includes('/') ? (page.split('/')[0] as string) : '';
  return FOLDER_MODULES[folder] ?? (folder || 'warehouse');
}

/** One page (path relative to the docs folder, with or without ".md") to its chunks. */
export function chunkPage(pagePath: string, source: string): DocChunk[] {
  const page = pagePath.replace(/\\/g, '/').replace(/\.md$/i, '');
  const { meta, body } = parseFrontMatter(source);
  const { h1, intro, parts } = sections(body.split('\n'));
  const pageTitle = meta.title ?? h1?.title ?? (page.split('/').pop() as string);
  const status = statusOf(meta, body);
  const base = { module: moduleOf(page, meta), page, page_title: pageTitle, status, routes: meta.routes, roles: meta.roles };
  const pageUrl = pageUrlPath(page);
  const used = new Set<string>();
  const unique = (id: string): string => {
    let candidate = id;
    for (let n = 2; used.has(candidate); n++) candidate = `${id}-${n}`;
    used.add(candidate);
    return candidate;
  };

  const chunks: DocChunk[] = [];
  const introText = cleanText(intro.filter((line) => !STATUS_LINE.test(line)).join('\n'));
  const statusLine = STATUS_LINE.exec(intro.join('\n'))?.[0]?.trim();
  if (introText || statusLine) {
    const introImages = imagesOf(page, intro.join('\n'));
    chunks.push({ id: unique(`${page}#intro`), ...base, heading_path: [], url_path: pageUrl, text: cleanText([statusLine, introText].filter(Boolean).join('\n\n')), ...(introImages.length ? { images: introImages } : {}) });
  }
  for (const part of parts) {
    const text = cleanText(part.lines.join('\n'));
    if (!text) continue;
    const last = part.path[part.path.length - 1] as Heading;
    const images = imagesOf(page, part.lines.join('\n'));
    chunks.push({
      id: unique(`${page}#${part.path.map((h) => h.anchor || slugify(h.title)).join('--')}`),
      ...base,
      heading_path: part.path.map((h) => h.title),
      url_path: `${pageUrl}#${last.anchor}`,
      text,
      ...(images.length ? { images } : {}),
    });
  }
  return chunks;
}

function markdownFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) found.push(...markdownFiles(full));
    else if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) found.push(full);
  }
  return found;
}

export interface DocsIndex {
  generated_at: string;
  git_sha: string;
  page_count: number;
  chunk_count: number;
  chunks: DocChunk[];
}

/** Walks the docs folder (sorted, so the index is reproducible) and chunks every page. */
export function buildIndex(docsDir: string, meta: { generatedAt?: string; gitSha?: string } = {}): DocsIndex {
  const files = markdownFiles(docsDir)
    .map((file) => relative(docsDir, file).split(sep).join('/'))
    .sort();
  const chunks = files.flatMap((file) => chunkPage(file, readFileSync(join(docsDir, file), 'utf8')));
  return { generated_at: meta.generatedAt ?? new Date().toISOString(), git_sha: meta.gitSha ?? '', page_count: files.length, chunk_count: chunks.length, chunks };
}
