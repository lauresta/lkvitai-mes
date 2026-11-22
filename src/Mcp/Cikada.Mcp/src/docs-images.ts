// The manual screenshots behind docs_image: finds the PNG that a manual chunk shows and, when asked, draws coloured frames on it so the assistant can
// point at a button or a field. The screenshots are copied into the image at build time (data/docs-img/<module>/<name>.png) and never leave it otherwise.

import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PNG } from 'pngjs';

/** A frame on a screenshot, in percent of its width and height, measured from the top left corner. */
export interface Mark {
  x: number;
  y: number;
  width: number;
  height: number;
}

export const MARK_COLOURS: readonly { name: string; rgb: readonly [number, number, number] }[] = [
  { name: 'red', rgb: [220, 38, 38] },
  { name: 'blue', rgb: [37, 99, 235] },
  { name: 'green', rgb: [22, 163, 74] },
  { name: 'orange', rgb: [234, 88, 12] },
  { name: 'purple', rgb: [147, 51, 234] },
  { name: 'cyan', rgb: [8, 145, 178] },
];

const IMAGE_PATH = /^img\/([\w-]+)\/([\w.-]+\.png)$/i;

/** DOCS_IMAGE_DIR, else <package>/data/docs-img (the image), else the manuals folder of a checkout. */
export function defaultImageDir(): string | undefined {
  const fromEnv = process.env.DOCS_IMAGE_DIR?.trim();
  if (fromEnv) return fromEnv;
  const packageDir = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
  const candidates = [join(packageDir, 'data', 'docs-img'), join(packageDir, '..', '..', '..', 'docs', 'manuals', 'docs', 'img')];
  return candidates.find((dir) => existsSync(dir));
}

/** The file of "img/<module>/<name>.png" inside the screenshots folder, or undefined when the path is not of that shape or the file is missing. */
export function imageFile(dir: string, path: string): string | undefined {
  const match = IMAGE_PATH.exec(path);
  if (!match || path.includes('..')) return undefined;
  const file = join(dir, match[1] as string, match[2] as string);
  return existsSync(file) ? file : undefined;
}

export interface LoadedImage {
  data: Buffer;
  width: number;
  height: number;
}

function paint(png: PNG, x: number, y: number, rgb: readonly [number, number, number]): void {
  if (x < 0 || y < 0 || x >= png.width || y >= png.height) return;
  const at = (png.width * y + x) * 4;
  png.data[at] = rgb[0];
  png.data[at + 1] = rgb[1];
  png.data[at + 2] = rgb[2];
  png.data[at + 3] = 255;
}

/** A frame `thickness` pixels wide just inside the rectangle, with a one pixel white line around it so it shows on any background. */
function frame(png: PNG, left: number, top: number, right: number, bottom: number, thickness: number, rgb: readonly [number, number, number]): void {
  const white = [255, 255, 255] as const;
  for (let ring = -1; ring < thickness; ring++) {
    const colour = ring < 0 ? white : rgb;
    for (let x = left + ring; x <= right - ring; x++) {
      paint(png, x, top + ring, colour);
      paint(png, x, bottom - ring, colour);
    }
    for (let y = top + ring; y <= bottom - ring; y++) {
      paint(png, left + ring, y, colour);
      paint(png, right - ring, y, colour);
    }
  }
}

/** Reads the PNG and draws the marks (at most as many as there are colours); without marks the file is returned as it is. */
export function loadImage(file: string, marks: readonly Mark[] = []): LoadedImage {
  const source = readFileSync(file);
  const png = PNG.sync.read(source);
  if (!marks.length) return { data: source, width: png.width, height: png.height };
  const thickness = Math.max(3, Math.round(png.width / 360));
  marks.slice(0, MARK_COLOURS.length).forEach((mark, index) => {
    const left = Math.round((mark.x / 100) * png.width);
    const top = Math.round((mark.y / 100) * png.height);
    const right = Math.min(png.width - 1, Math.round(((mark.x + mark.width) / 100) * png.width));
    const bottom = Math.min(png.height - 1, Math.round(((mark.y + mark.height) / 100) * png.height));
    frame(png, left, top, right, bottom, thickness, (MARK_COLOURS[index] as { rgb: readonly [number, number, number] }).rgb);
  });
  return { data: PNG.sync.write(png), width: png.width, height: png.height };
}
