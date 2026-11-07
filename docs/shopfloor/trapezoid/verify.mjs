import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { calculate, legacyProfile, lookup } from './reference-calculator.mjs';
const tables = JSON.parse(await fs.readFile(new URL('./legacy-tables.json', import.meta.url)));
const fixtures = JSON.parse(await fs.readFile(new URL('./fixtures.json', import.meta.url)));
const close = (actual, expected, label) => assert.ok(Math.abs(actual - expected) < 1e-8, `${label}: ${actual} != ${expected}`);
for (const fixture of fixtures.cases) {
  const result = calculate(fixture.input, tables, {...legacyProfile,...fixture.profileOverride});
  for (const [key, expected] of Object.entries(fixture.expected)) {
    if (key === 'cutsMm') expected.forEach((value, i) => close(result.slats[i].cutMm, value, `${fixture.name} lamella ${i+1}`));
    else close(result[key], expected, `${fixture.name} ${key}`);
  }
}
// Independent checks across every compressed band, not just the two examples.
for (const key of ['89','127','extender']) {
  const table = tables[key];
  for (let i = 0; i < table.bands.length; i++) {
    const [start, value] = table.bands[i];
    assert.equal(lookup(table, start), value);
    const end = i+1 < table.bands.length ? table.bands[i+1][0]-1 : table.maxKey;
    assert.equal(lookup(table, end), value);
  }
}
for (const controlType of ['A', 'B']) for (const slatWidthMm of [89,127]) {
  const input = { controlType, slatWidthMm, widthMm:2000, shortHeightMm:1800, tallHeightMm:1800, overhangMm:0 };
  const r = calculate(input,tables);
  assert.equal(r.incrementMm,0);
  assert.ok(r.slats.every(s=>s.cutMm===r.slats[0].cutMm));
  close(r.fabricM,r.slatCount*(r.slats[0].cutMm+27)/1000,'flat total');
}
const base=fixtures.cases[0].input;
const a0=calculate(base,tables), a10=calculate({...base,overhangMm:10},tables);
close(a0.fixedOffsetMm-a10.fixedOffsetMm,10/Math.cos(a0.angle),'overhang geometry');
assert.ok(a10.pitchMm>a0.pitchMm);
const b0=calculate({...base,controlType:'B'},tables), b10=calculate({...base,controlType:'B',overhangMm:10},tables);
close(b0.fabricM,b10.fabricM,'B ignores overhang');
assert.throws(()=>calculate({...base,widthMm:0},tables),/InvalidDimensions/);
assert.throws(()=>calculate({...base,shortHeightMm:null},tables),/InvalidDimensions/);
assert.throws(()=>calculate({...base,widthMm:200},tables),/InvalidCarrierSpan/);
assert.throws(()=>calculate({...base,widthMm:8000},tables),/OutsideReferenceTable/);
assert.throws(()=>calculate({...base,shortHeightMm:10,tallHeightMm:10},tables),/NonPositiveResult/);
const extra=calculate(base,tables,{...legacyProfile,maxSpacerCutMm:50});
assert.equal(extra.extraCount,19);
assert.equal(extra.slatCount,64); // Native disposable Calc scenario, see excel-analysis.md.
console.log(`Verified 2 original-cache fixtures (40 individual cuts), 4 independent LibreOffice scenarios (160 further cuts), all lookup boundaries, flat A/B × 89/127, overhang, invalid dimensions and legacy extra-count behavior.`);
