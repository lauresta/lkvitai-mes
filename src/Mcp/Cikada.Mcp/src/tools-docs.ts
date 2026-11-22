// The manuals tools: docs_search and docs_get answer from the user manuals (Lithuanian, chunked at image build time), docs_image returns a screenshot a
// chunk shows, with frames drawn on it when asked. They need no backend API call and no role: any signed-in user may read the manuals.

import { ApiError } from './api.js';
import { loadDocsSearch, tokenize, type DocsSearch } from './docs-search.js';
import type { DocChunk } from './docs-chunker.js';
import { ArgumentError } from './validate.js';
import { MARK_COLOURS, defaultImageDir, imageFile, loadImage, type Mark } from './docs-images.js';
import { READ, asNumber, asString, compact, int, num, obj, str, type ToolContext, type ToolDef } from './tool.js';

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
    images: chunk.images?.length ? chunk.images : undefined,
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
    'Read the full text of one manual chunk by id (as docs_search returns it), or all chunks of one page in order by page (the path without .md, e.g. "procesai/P-01-priemimas-inbound"). Returns the text with module, page title, heading path, status ("laikina" = stub, "dalinai" = partly working), routes, roles, url and images (the screenshots the chunk shows: pass a path to docs_image to see one). Give exactly one of id or page. Read the chunk before answering from a search hit, and tell the user when the status is "laikina".',
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

const docsImage: ToolDef = {
  name: 'docs_image',
  title: 'Show a manual screenshot',
  description:
    'Return one screenshot from the user manuals as an image: the screen a manual chunk describes. Take the path from the images of a docs_get / docs_search result (e.g. "img/warehouse/inbound-create.png"). Call it without marks first to look at the screen; to point something out to the user, call it again with marks: up to 6 frames given in PERCENT of the image width and height from the top left corner (x, y = top left of the frame; width, height = its size). Each frame gets its own colour, in order: red, blue, green, orange, purple, cyan; refer to them by colour in the answer ("press the button in the red frame"). The screenshots show the test stand with sample data, so describe the layout, not the numbers.',
  inputSchema: obj(
    {
      image: str('Screenshot path from a manual chunk, e.g. "img/warehouse/inbound-create.png".', { minLength: 5, maxLength: 200, pattern: '^img/[\\w-]+/[\\w.-]+\\.png$' }),
      marks: {
        type: 'array',
        description: 'Frames to draw, in percent of the image size.',
        maxItems: MARK_COLOURS.length,
        items: obj(
          {
            x: num('Left edge, percent of the width (0-100).', { minimum: 0, maximum: 100 }),
            y: num('Top edge, percent of the height (0-100).', { minimum: 0, maximum: 100 }),
            width: num('Width, percent of the image width.', { minimum: 0.5, maximum: 100 }),
            height: num('Height, percent of the image height.', { minimum: 0.5, maximum: 100 }),
          },
          ['x', 'y', 'width', 'height'],
        ),
      },
    },
    ['image'],
  ),
  annotations: READ,
  handler: (args, ctx) => {
    const path = asString(args.image) ?? '';
    const dir = ctx.docsImageDir ?? defaultImageDir();
    if (!dir) throw new ApiError('The manual screenshots are not available on this server (they are copied in when the image is built).', 503, 'DOCS_IMAGES_UNAVAILABLE');
    const file = imageFile(dir, path);
    if (!file) throw new ApiError(`There is no manual screenshot "${path}". Use a path from the images of a docs_get result.`, 404, 'NOT_FOUND');
    const marks = (Array.isArray(args.marks) ? (args.marks as Mark[]) : []).map((m) => ({ x: m.x, y: m.y, width: Math.min(m.width, 100 - m.x), height: Math.min(m.height, 100 - m.y) }));
    const image = loadImage(file, marks);
    const legend = marks.map((_mark, index) => `${index + 1} ${(MARK_COLOURS[index] as { name: string }).name}`);
    const url = ctx.manualsBaseUrl ? `${ctx.manualsBaseUrl}/${path}` : undefined;
    return Promise.resolve({
      text: `Screenshot ${path} (${image.width}x${image.height})${legend.length ? `, frames: ${legend.join(', ')}` : ''}.${url ? ` ${url}` : ''}`,
      data: compact({ image: path, width: image.width, height: image.height, url, marks: marks.length ? marks.map((mark, index) => ({ ...mark, colour: (MARK_COLOURS[index] as { name: string }).name })) : undefined }),
      images: [{ data: image.data.toString('base64'), mimeType: 'image/png' }],
    });
  },
};

export const DOCS_TOOLS: readonly ToolDef[] = [docsSearch, docsGet, docsImage];
