import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const M = require('./combat-model.js');

/* ---- build presets : one "geared level-N hero" budget, split three ways ---- */
// Equal defensive budget across all three (health / armor / regen identical) so the
// matchup differences come from damage SHAPE — single-target Strike vs AoE Cast —
// not from raw effective-HP. Engagement differs (each build's natural stance).
const DEF = { health: 442, healthRegen: 3, armor: 27 };
const BUILDS = {
  physical: {
    ...DEF,
    physicalDamage: 46, attackSpeed: 1.6,
    magicalDamage: 5, resourceMax: 45, resourceRegen: 6,
    castThreshold: 0.3, engagement: 2,
  },
  magical: {
    ...DEF,
    physicalDamage: 8, attackSpeed: 1.0,
    magicalDamage: 23, resourceMax: 120, resourceRegen: 20,
    castThreshold: 0.4, engagement: 4,
  },
  // hybrid: midpoint damage, but spends the leftover budget on a thicker HP pool
  // (the "bruiser" — trades peak throughput for a floor under its bad matchup).
  hybrid: {
    health: 476, healthRegen: 4, armor: 30,
    physicalDamage: 28, attackSpeed: 1.35,
    magicalDamage: 14, resourceMax: 86, resourceRegen: 14,
    castThreshold: 0.35, engagement: 3,
  },
};
// engagement-neutralised copies — for isolating damage SHAPE from the kite/dive stance
const BUILDS_EN3 = Object.fromEntries(Object.entries(BUILDS).map(([k, v]) => [k, { ...v, engagement: 3 }]));

/* ---- locations : S level + which archetype is Packed ---- */
const loc = (sourceLevel, packed, over = {}) => M.deepMerge({
  sourceLevel, packed,
  roster: { brute: [8, 8], skirmisher: [6, 6] },
  packBatch: [2, 3], packedSpawnWeight: 0.6,
  initialSpawn: 2, spawnInterval: 2.6, spawnJitter: 0.7,
}, over);

const LOCATIONS = {
  // isolation grid @ S5: brute-heavy / skirm-heavy / mixed — same total roster (~17)
  's5-brutepack':  loc(5, 'brute',      { roster: { brute: [12, 12], skirmisher: [5, 5] } }),
  's5-skirmpack':  loc(5, 'skirmisher', { roster: { brute: [5, 5], skirmisher: [12, 12] } }),
  's5-mixed':      loc(5, 'brute',      { roster: { brute: [8, 8], skirmisher: [9, 9] }, packBatch: [2, 2], packedSpawnWeight: 0.5 }),
  // low-S counterparts (are builds separable when the content is trivial?)
  's2-brutepack':  loc(2, 'brute',      { roster: { brute: [12, 12], skirmisher: [5, 5] } }),
  's2-skirmpack':  loc(2, 'skirmisher', { roster: { brute: [5, 5], skirmisher: [12, 12] } }),
  // canonical pair (handoff): Location 1 packs Brutes @ low S, Location 2 flips @ high S
  thornwood: loc(2, 'brute',      { roster: { brute: [9, 9], skirmisher: [5, 5] } }),
  ashfall:   loc(5, 'skirmisher', { roster: { brute: [4, 4], skirmisher: [13, 13] }, packBatch: [3, 5], packedSpawnWeight: 0.7, spawnInterval: 2.3 }),
};

const cfg = (b, l, over = {}, builds = BUILDS) => M.deepMerge({ hero: builds[b], location: LOCATIONS[l] }, over);
const raw = { hero: { retreatHealthFraction: 0 }, loot: { keepChancePerKill: 0 } }; // out-sustain test

function agg(config, n = 200) {
  const rows = [];
  for (let i = 0; i < n; i++) rows.push(M.simulate(config, 1000 + i * 7919));
  const s = arr => arr.slice().sort((a, b) => a - b);
  const med = arr => s(arr)[Math.floor(arr.length / 2)] ?? 0;
  const mean = arr => +(arr.reduce((x, y) => x + y, 0) / (arr.length || 1)).toFixed(1);
  const died = rows.filter(r => r.outcome === 'Died');
  const floors = rows.map(r => r.settledHpFloor).filter(x => x != null);
  return {
    deathRate: +(died.length / rows.length).toFixed(2),
    encMed: med(rows.map(r => r.encountersCleared)),
    killsB: mean(rows.map(r => r.killsByType.brute)),
    killsS: mean(rows.map(r => r.killsByType.skirmisher)),
    ttkB: mean(rows.map(r => r.ttkBrute)),
    ttkS: mean(rows.map(r => r.ttkSkirmisher)),
    durMed: med(rows.map(r => r.duration)),
    xpMin: mean(rows.map(r => r.xpPerMin)),
    inB: mean(rows.map(r => r.dmgInByType.brute)),
    inS: mean(rows.map(r => r.dmgInByType.skirmisher)),
    fbXp: mean(rows.map(r => r.xpForfeited)),
    settledFloor: floors.length ? +(mean(floors)).toFixed(2) : '-',
    outc: tally(rows.map(r => r.outcome)),
  };
}
const tally = list => { const m = {}; for (const x of list) m[x] = (m[x] || 0) + 1; return m; };
const show = (label, a) => console.log(
  label.padEnd(22),
  `death ${String(a.deathRate).padStart(4)}`,
  `enc ${String(a.encMed).padStart(2)}`,
  `killB ${String(a.killsB).padStart(5)}`,
  `killS ${String(a.killsS).padStart(5)}`,
  `ttkB ${String(a.ttkB).padStart(5)}s`,
  `ttkS ${String(a.ttkS).padStart(4)}s`,
  `dur ${String(a.durMed).padStart(5)}s`,
  `xp/min ${String(a.xpMin).padStart(5)}`,
  `inB/inS ${String(a.inB).padStart(5)}/${String(a.inS).padStart(5)}`,
  `fbXp ${String(a.fbXp).padStart(4)}`,
  `settledHP ${String(a.settledFloor).padStart(5)}`,
  `| ${Object.entries(a.outc).map(([k, v]) => `${k}:${v}`).join(' ')}`,
);

console.log('\n############ Q-A : build x pack composition @ S5 — which build wins which pack?');
console.log('#  raw (retreat OFF, bag OFF), NATURAL engagement (phys 2 / mag 4 / hyb 3). higher enc = better.');
for (const l of ['s5-brutepack', 's5-mixed', 's5-skirmpack']) {
  console.log(`--- ${l} ---`);
  for (const b of ['physical', 'magical', 'hybrid'])
    show(b, agg(M.deepMerge(cfg(b, l), raw), 200));
}
console.log('#  same, engagement forced to 3 for all — isolates damage SHAPE from the kite/dive stance');
for (const l of ['s5-brutepack', 's5-mixed', 's5-skirmpack']) {
  console.log(`--- ${l} (eng3) ---`);
  for (const b of ['physical', 'magical', 'hybrid'])
    show(b, agg(M.deepMerge(cfg(b, l, {}, BUILDS_EN3), raw), 200));
}
console.log('#  and @ S2 (trivial content) — do builds separate at all?');
for (const l of ['s2-brutepack', 's2-skirmpack']) {
  console.log(`--- ${l} ---`);
  for (const b of ['physical', 'magical', 'hybrid'])
    show(b, agg(M.deepMerge(cfg(b, l), raw), 200));
}

console.log('\n############ Q-B : the canonical two Locations, REALISTIC run (retreat 0.25 + bag 0.9 on)');
for (const l of ['thornwood', 'ashfall']) {
  console.log(`--- ${l} ---`);
  for (const b of ['physical', 'magical', 'hybrid'])
    show(b, agg(cfg(b, l), 200));
}

console.log('\n############ Q-C : auto-focus — is the lowest-HP Strike ignoring Brutes while Skirmishers live?');
console.log('#  hybrid, raw, both packed variants @ S5. Watch ttkB (Brute time-alive) vs incoming split.');
for (const l of ['s5-brutepack', 's5-skirmpack']) {
  for (const b of ['physical', 'magical', 'hybrid'])
    show(`${b} @ ${l}`, agg(M.deepMerge(cfg(b, l), raw), 200));
}

console.log('\n############ Q-D : gear multiplier — how much gear opens ashfall (S5 skirm-pack)?');
for (const b of ['physical', 'magical', 'hybrid']) {
  console.log(`--- ${b} ---`);
  for (const m of [1.0, 1.15, 1.3, 1.5]) {
    const h = BUILDS[b];
    const scaled = { ...h, physicalDamage: h.physicalDamage * m, magicalDamage: h.magicalDamage * m, health: h.health * m, attackSpeed: h.attackSpeed * (1 + (m - 1) * 0.4) };
    show(`x${m}`, agg(M.deepMerge({ hero: scaled, location: LOCATIONS.ashfall }, raw), 150));
  }
}

console.log('\n############ Q-E : CastThreshold with two enemies (magical @ ashfall x1.3, raw)');
for (const th of [0.05, 0.3, 0.6, 0.9]) {
  const h = BUILDS.magical, m = 1.3;
  const scaled = { ...h, physicalDamage: h.physicalDamage * m, magicalDamage: h.magicalDamage * m, health: h.health * m, attackSpeed: h.attackSpeed * (1 + (m - 1) * 0.4), castThreshold: th };
  show(`threshold ${th}`, agg(M.deepMerge({ hero: scaled, location: LOCATIONS.ashfall }, { loot: { keepChancePerKill: 0 } }), 150));
}

console.log('\n############ Q-F : per-Encounter XP settle — what does a mid-Encounter exit forfeit? (fbXp = XP lost)');
console.log('#  realistic (retreat+bag on): the exit lands near a clear, so forfeit is small');
for (const l of ['thornwood', 'ashfall'])
  for (const b of ['physical', 'magical', 'hybrid'])
    show(`${b} @ ${l}`, agg(cfg(b, l), 200));
console.log('#  retreat OFF so Deaths happen mid-Encounter — the whole in-progress Encounter XP is lost');
for (const b of ['physical', 'magical', 'hybrid'])
  show(`${b} @ ashfall die`, agg(M.deepMerge(cfg(b, 'ashfall'), { hero: { retreatHealthFraction: 0 } }), 200));

console.log('\n############ traces');
trace('physical @ thornwood (realistic)', cfg('physical', 'thornwood'), 7, 120);
trace('magical  @ thornwood (realistic)', cfg('magical', 'thornwood'), 7, 120);
trace('physical @ ashfall x1.3 (raw)', M.deepMerge({ hero: scale(BUILDS.physical, 1.3), location: LOCATIONS.ashfall }, raw), 7, 160);
trace('magical  @ ashfall x1.3 (raw)', M.deepMerge({ hero: scale(BUILDS.magical, 1.3), location: LOCATIONS.ashfall }, raw), 7, 160);

function scale(h, m) {
  return { ...h, physicalDamage: h.physicalDamage * m, magicalDamage: h.magicalDamage * m, health: h.health * m, attackSpeed: h.attackSpeed * (1 + (m - 1) * 0.4) };
}
function trace(label, config, seed, seconds) {
  const st = M.createState(config, seed);
  const steps = Math.round(seconds / st.cfg.tick);
  const marks = [];
  for (let i = 0; i < steps && st.phase !== 'ended'; i++) {
    for (const e of M.step(st)) {
      if (e.t === 'clear') marks.push(`t${st.time.toFixed(0)} CLR${e.encounter}(+${e.xp}xp hp${(st.hero.hp / st.hero.maxHp * 100).toFixed(0)}%)`);
      if (e.t === 'end') marks.push(`t${st.time.toFixed(0)} ${e.outcome}(hp${(st.hero.hp / st.hero.maxHp * 100).toFixed(0)}%)`);
    }
  }
  const o = st.outcome || {};
  console.log(`  ${label}:`);
  console.log(`    cleared ${st.encountersCleared}  killed B${st.killsByType.brute}/S${st.killsByType.skirmisher}  ${st.phase === 'ended' ? o.outcome : 'ongoing'}  xp ${Math.round(st.xpGained)} (forfeit ${Math.round(st.xpForfeited)})`);
  console.log(`    ${marks.slice(0, 10).join('  ')}`);
}
