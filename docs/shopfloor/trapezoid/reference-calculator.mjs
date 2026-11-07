// Executable specification, not a production service. Units: mm, radians, metres.
// Preserve trapnew.xls, including the shifted 89 mm LOOKUP. See excel-analysis.md.
export const legacyProfile = Object.freeze({
  mountingMm: 5, profileMm: 25, hookMm: 16, controlMm: 15,
  maxSpacerCutMm: 130, spacerAllowanceMm: 6, railAllowanceMm: 0,
  fixedMechanismOffsetMm: 5.5, slatCutAllowanceMm: 45, fabricExtraPerSlatMm: 27
});

export function excelRound(value, digits = 0) {
  const factor = 10 ** digits;
  return Math.sign(value) * Math.floor(Math.abs(value) * factor + 0.5) / factor;
}

export function lookup(table, key) {
  if (!Number.isFinite(key) || key < table.bands[0][0] || key > table.maxKey)
    throw new Error('OutsideReferenceTable');
  let lo = 0, hi = table.bands.length;
  while (lo + 1 < hi) {
    const mid = Math.floor((lo + hi) / 2);
    if (table.bands[mid][0] <= key) lo = mid; else hi = mid;
  }
  return table.bands[lo][1];
}

export function calculate(input, tables, profile = legacyProfile) {
  const { widthMm: w, shortHeightMm: hs, tallHeightMm: ht, slatWidthMm: b,
    controlType: type, overhangMm: o = 0 } = input;
  if (![w, hs, ht, b, o].every(Number.isFinite) || w <= 0 || hs <= 0 || ht < hs || o < 0)
    throw new Error('InvalidDimensions');
  if (!['A', 'B'].includes(type) || ![89, 127].includes(b)) throw new Error('UnsupportedConfiguration');
  const p = profile;
  if (p.maxSpacerCutMm <= p.spacerAllowanceMm) throw new Error('InvalidTechnologyProfile');
  const h1 = p.mountingMm + p.profileMm, hook = p.hookMm;
  const angle = Math.atan2(ht - hs, w), cos = Math.cos(angle), sin = Math.sin(angle), tan = Math.tan(angle);
  const diagonalMm = Math.hypot(w, ht - hs);
  const railBlankMm = diagonalMm - h1 * tan - p.railAllowanceMm;
  const railCutMm = railBlankMm - p.controlMm;
  // Algebraically equivalent to the legacy nested SQRT/TAN formula for ht >= hs.
  const fixedOffsetMm = (b / 2 - (type === 'A' ? o : 0)) / cos + hook * tan;
  const freeOffsetMm = b / (2 * cos) - (type === 'A' ? hook : h1 + hook) * tan;
  const spanMm = railCutMm - fixedOffsetMm - freeOffsetMm;
  const lookupWidthMm = excelRound(w - h1 * sin - (p.controlMm + p.railAllowanceMm) * cos);
  const table = tables[String(b)], baseCount = lookup(table, lookupWidthMm);
  if (baseCount < 2 || spanMm <= 0) throw new Error('InvalidCarrierSpan');
  const basePitchMm = spanMm / (baseCount - 1);
  const extraCount = basePitchMm > p.maxSpacerCutMm - p.spacerAllowanceMm
    ? Math.max(0, Math.ceil(spanMm / (p.maxSpacerCutMm - p.spacerAllowanceMm) + 1) - baseCount) : 0;
  // Excel: LOOKUP(PLOTIS, HBR89 + extra) + extra. Both columns shift.
  const slatCount = b === 89 ? lookup(table, lookupWidthMm - extraCount) + 2 * extraCount : baseCount + extraCount;
  if (slatCount < 2) throw new Error('InvalidSlatCount');
  if (slatCount > 78) throw new Error('BeyondLegacyPrintCapacity');
  const pitchMm = spanMm / (slatCount - 1);
  const desiredExtenderMm = b / 2 * tan - hook / cos;
  const extenderMm = desiredExtenderMm < 0 ? 0 : lookup(tables.extender, excelRound(desiredExtenderMm));
  if (typeof extenderMm !== 'number') throw new Error('UnsupportedExtender');
  const firstFinishedMm = type === 'A'
    ? hs - h1 * cos + p.controlMm * sin + b / 2 * tan - hook / cos - extenderMm
    : hs - h1 / cos + b / 2 * tan - hook / cos - extenderMm;
  const incrementMm = pitchMm * sin;
  const slats = Array.from({ length: slatCount }, (_, i) => {
    const finishedMm = firstFinishedMm + i * incrementMm;
    const cutMm = finishedMm + p.slatCutAllowanceMm;
    return { number: i + 1, finishedMm, cutMm, displayedCutMm: excelRound(cutMm),
      alongRailMm: i * pitchMm, horizontalMm: i * pitchMm * cos };
  });
  if (slats.some(s => s.finishedMm <= 0) || railCutMm <= 0) throw new Error('NonPositiveResult');
  const cutTotalMm = slats.reduce((total, s) => total + s.cutMm, 0);
  const fabricM = (cutTotalMm + slatCount * p.fabricExtraPerSlatMm) / 1000;
  return { angle, diagonalMm, railBlankMm, railCutMm, fixedOffsetMm, freeOffsetMm,
    fixedToMechanismMm: fixedOffsetMm - p.fixedMechanismOffsetMm,
    spanMm, lookupWidthMm, baseCount, extraCount, slatCount, pitchMm,
    spacerCutMm: pitchMm + p.spacerAllowanceMm, extenderMm, firstFinishedMm,
    incrementMm, cutTotalMm, fabricM, slats,
    notes: [...(type === 'B' && o !== 0 ? ['OverhangUnusedByLegacyB'] : []),
      ...(b === 89 && extraCount > 0 ? ['Legacy89ShiftedLookup'] : [])] };
}
