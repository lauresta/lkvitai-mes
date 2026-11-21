// The manuals tools: docs_search and docs_get answer from the user manuals (Lithuanian, chunked at image build time). They need no backend API call and no
// role: any signed-in user may read the manuals.

import { ApiError } from './api.js';
import { loadDocsSearch, tokenize, type DocsSearch } from './docs-search.js';
import type { DocChunk } from './docs-chunker.js';
import { ArgumentError } from './validate.js';
import { READ, asNumber, asString, compact, int, obj, str, type ToolContext, type ToolDef } from './tool.js';

const available = (ctx: ToolContext): DocsSearch => {
  const search = ctx.docs ?? loadDocsSearch();
  if (!search) throw new ApiError('The manuals index is not available on this server (it is generated when the image is built).', 503, 'DOCS_UNAVAILABLE');
  return search;
};

const urlOf = (chunk: DocChunk, base: string | undefined): string => (base ? `${base}/${chunk.url_path}` : `/${chunk.url_path}`);

const meta = (chunk: DocChunk, base: string | undefined) =>
  compact({
    id: chunk.id,
    module: chunk.module,
    page: chunk.page,
    page_title: chunk.page_title,
    heading_path: chunk.heading_path.length ? chunk.heading_path : undefined,
    status: chunk.status,
    routes: chunk.routes.length ? chunk.routes : undefined,
    roles: chunk.roles.length ? chunk.roles : undefined,
    url: urlOf(chunk, base),
  });

const docsSearch: ToolDef = {
  name: 'docs_search',
  title: 'Search the user manuals',
  description:
    'Search the Cikada.MES user manuals (how to use each screen and process: warehouse, sales, shopfloor, frontline, finance, scanning, portal). The manuals are written in LITHUANIAN, so translate the user\'s question first and search with Lithuanian keywords (e.g. "priėmimas", "išsiuntimas", "atsargų koregavimas"; diacritics are optional). Try 2-3 different queries (synonyms, the screen name, the button label) before concluding that something is not documented. Each hit is a short snippet only: read the full chunk with docs_get (by id) before answering the user. If the hit or chunk status is "laikina" (stub: the screen shows sample data or does nothing yet), say so in the answer; "dalinai" means only part of the screen works. Returns id, module, page_title, heading_path, status, url and snippet per hit, and the total number of matching chunks.',
  inputSchema: obj(
    {
      query: str('Lithuanian keywords, e.g. "prekių priėmimas Create Inbound".', { minLength: 1, maxLength: 300 }),
      module: str('Only one module: warehouse, sales, shopfloor, frontline, finance, scanning, portal, mcp.', { maxLength: 40 }),
      limit: int('How many hits, default 6, at most 15.', { minimum: 1, maximum: 15 }),
    },
    ['query'],
  ),
  annotations: READ,
  handler: async (args, ctx) => {
    const query = asString(args.query) ?? '';
    if (tokenize(query).length === 0) throw new ArgumentError('query must contain at least one word');
    const search = available(ctx);
    const module = asString(args.module);
    const limit = asNumber(args.limit) ?? 6;
    const { hits, total } = search.search(query, { ...(module ? { module } : {}), limit });
    const results = hits.map((hit) => ({ ...meta(hit.chunk, ctx.manualsBaseUrl), snippet: hit.snippet }));
    const lines = results.map((r, i) => `${i + 1}. ${r.page_title}${r.heading_path ? ` > ${(r.heading_path as string[]).join(' > ')}` : ''} [${r.module}${r.status ? `, ${r.status}` : ''}] ${r.id}`);
    return {
      text: `${results.length} of ${total} matching chunks for "${query}".${lines.length ? `\n${lines.join('\n')}` : ` Try other Lithuanian keywords. Modules: ${search.modules().join(', ')}.`}`,
      data: { total, results, ...(total === 0 ? { modules: search.modules() } : {}) },
    };
  },
};

const docsGet: ToolDef = {
  name: 'docs_get',
  title: 'Read a manual chunk or page',
  description:
    'Read the full text of one manual chunk by id (as docs_search returns it), or all chunks of one page in order by page (the path without .md, e.g. "procesai/P-01-priemimas-inbound"). Returns the text with module, page title, heading path, status ("laikina" = stub, "dalinai" = partly working), routes, roles and url. Give exactly one of id or page. Read the chunk before answering from a search hit, and tell the user when the status is "laikina".',
  inputSchema: obj({
    id: str('Chunk id from docs_search, e.g. "procesai/P-01-priemimas-inbound#zingsniai".', { maxLength: 300 }),
    page: str('Page path without .md, e.g. "procesai/P-01-priemimas-inbound".', { maxLength: 300 }),
  }),
  annotations: READ,
  handler: async (args, ctx) => {
    const search = available(ctx);
    const id = asString(args.id);
    const page = asString(args.page);
    if ((id === undefined) === (page === undefined)) throw new ArgumentError('give exactly one of id or page');
    const chunks = id !== undefined ? [search.getChunk(id)].filter((c): c is DocChunk => c !== undefined) : search.getPage(page as string);
    if (!chunks.length) throw new ApiError(id !== undefined ? `There is no manual chunk with id "${id}". Use an id from docs_search.` : `There is no manual page "${page}". Use a page from docs_search.`, 404, 'NOT_FOUND');
    const items = chunks.map((chunk) => ({ ...meta(chunk, ctx.manualsBaseUrl), text: chunk.text }));
    return {
      text: items.map((item) => `# ${item.page_title}${item.heading_path ? ` > ${(item.heading_path as string[]).join(' > ')}` : ''} [${item.status || 'no status'}]\n${item.text}`).join('\n\n'),
      data: { chunks: items },
    };
  },
};

export const DOCS_TOOLS: readonly ToolDef[] = [docsSearch, docsGet];
