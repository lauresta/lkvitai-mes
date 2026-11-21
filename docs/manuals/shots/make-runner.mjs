// Builds a Playwright snippet that takes every screenshot listed in shots/*.json.
// Usage: node docs/manuals/shots/make-runner.mjs [module ...] > runner.js
// The snippet is an `async (page) => {...}` function: run it in a browser that is
// already signed in to the test stand (for example through the Playwright MCP).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const docsDir = path.resolve(here, '..', 'docs');
const only = process.argv.slice(2);
const shots = [];
for (const f of fs.readdirSync(here).filter((x) => x.endsWith('.json')).sort()) {
  const mod = f.replace(/\.json$/, '');
  if (only.length && !only.includes(mod)) continue;
  for (const s of JSON.parse(fs.readFileSync(path.join(here, f), 'utf8'))) {
    shots.push({ ...s, lang: ['sales', 'shopfloor', 'finance'].includes(mod) ? 'lt' : 'en', out: path.join(docsDir, s.file).replace(/\\/g, '/') });
  }
}
for (const s of shots) fs.mkdirSync(path.dirname(s.out), { recursive: true });

const body = `async (page) => {
  const shots = ${JSON.stringify(shots)};
  const results = [];
  let curLang = null;
  await page.setViewportSize({ width: 1440, height: 900 });
  for (const s of shots) {
    try {
      const resp = await page.goto(s.url, { waitUntil: 'domcontentloaded', timeout: 30000 });
      await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
      await page.waitForTimeout(1500);
      if (/sso\\.lauresta\\.com/.test(page.url())) { results.push(s.file + ' -> NOT SIGNED IN'); break; }
      if (s.lang !== curLang) {
        curLang = s.lang;
        await page.locator('form[action*="culture/set"] button', { hasText: s.lang === 'lt' ? /^(LT|Lietuvi)/ : /^(EN|English)/ }).first().click({ timeout: 5000 }).catch(() => {});
        await page.waitForTimeout(1500);
        await page.goto(s.url, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
        await page.waitForTimeout(1500);
      }
      const status = resp ? resp.status() : 0;
      let note = '';
      for (const st of s.steps || []) {
        try {
          if (st.click) await page.locator(st.click).first().click({ timeout: 5000 });
          else if (st.fill) await page.locator(st.fill[0]).first().fill(st.fill[1], { timeout: 5000 });
          else if (st.hover) await page.locator(st.hover).first().hover({ timeout: 5000 });
          else if (st.wait) await page.waitForTimeout(st.wait);
          await page.waitForTimeout(600);
        } catch (e) { note += ' step-failed:' + JSON.stringify(st).slice(0, 60); }
      }
      const text = (await page.locator('body').innerText().catch(() => '')).slice(0, 4000);
      if (/Server error|404 page not found|Access denied|An unhandled error/i.test(text)) note += ' page-shows-error';
      if (s.blurColumns) {
        await page.evaluate((names) => {
          for (const table of document.querySelectorAll('table')) {
            const heads = [...table.querySelectorAll('thead th')];
            heads.forEach((th, i) => {
              if (names.some((n) => th.innerText.trim().toLowerCase().startsWith(n.toLowerCase()))) {
                for (const td of table.querySelectorAll('tbody tr td:nth-child(' + (i + 1) + ')')) td.style.filter = 'blur(5px)';
              }
            });
          }
        }, s.blurColumns);
      }
      if (s.blur) {
        await page.evaluate((sels) => { for (const sel of sels) for (const el of document.querySelectorAll(sel)) el.style.filter = 'blur(5px)'; }, s.blur);
      }
      await page.screenshot({ path: s.out, fullPage: !!s.fullPage });
      results.push(s.file + ' -> ' + status + note);
    } catch (e) {
      results.push(s.file + ' -> FAILED ' + String(e).slice(0, 120));
    }
  }
  return results.join('\\n');
}`;
process.stdout.write(body + '\n');
