import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const M = require('./combat-model.js');

const BUILDS = {
  physical: {
    physicalDamage: 40, attackSpeed: 1.6,
    magicalDamage: 6, resourceMax: 45, resourceRegen: 6,
    health: 440, healthRegen: 3, armor: 30,
    castThreshold: 0.3, engagement: 2,
  },
  magical: {
    physicalDamage: 9, attackSpeed: 1.0,
    magicalDamage: 26, resourceMax: 130, resourceRegen: 20,
    health: 430, healthRegen: 3, armor: 24,
    castThreshold: 0.4, engagement: 4,
  },
  hybrid: {
    physicalDamage: 26, attackSpeed: 1.3,
    magicalDamage: 15, resourceMax: 85, resourceRegen: 15,
    health: 432, healthRegen: 3, armor: 26,
    castThreshold: 0.4, engagement: 3,
  },
};
const LOCATIONS = {
  easy: {
    sourceLevel: 2,
    enemyHealth: { base: 15, perLevel: 18, exp: 1.1 },
    enemyDamage: { base: 0.8, perLevel: 0.7, exp: 1.0 },
    enemyArmor: { base: 0, perLevel: 0.5, exp: 1.0 },
    enemyAttackSpeed: 0.75, xpPerKill: 16,
    finiteRoster: true, roster: [8, 8], initialSpawn: 2,
    spawnBatch: [1, 1], spawnInterval: 2.4, spawnJitter: 0.6,
  },
  hard: {
    sourceLevel: 5,
    enemyHealth: { base: 20, perLevel: 20, exp: 1.12 },
    enemyDamage: { base: 1.0, perLevel: 0.85, exp: 1.0 },
    enemyArmor: { base: 0, perLevel: 0.8, exp: 1.0 },
    enemyAttackSpeed: 0.85, xpPerKill: 30,
    finiteRoster: true, roster: [12, 12], initialSpawn: 3,
    spawnBatch: [2, 4], spawnInterval: 3.6, spawnJitter: 0.9,
  },
};
const cfg = (b, l, over = {}) => M.deepMerge({ hero: BUILDS[b], location: LOCATIONS[l] }, over);

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
    killsMed: med(rows.map(r => r.enemiesDefeated)),
    durMed: med(rows.map(r => r.duration)),
    xpMin: mean(rows.map(r => r.xpGained / Math.max(1, r.duration / 60))),
    settledFloor: floors.length ? +(mean(floors) / 1).toFixed(2) : '-',
    outc: tally(rows.map(r => r.outcome)),
  };
}
const tally = list => { const m = {}; for (const x of list) m[x] = (m[x] || 0) + 1; return m; };
const show = (label, a) => console.log(
  label.padEnd(20),
  `death ${String(a.deathRate).padStart(4)}`,
  `enc ${String(a.encMed).padStart(2)}`,
  `kills ${String(a.killsMed).padStart(3)}`,
  `dur ${String(a.durMed).padStart(5)}s`,
  `xp/min ${String(a.xpMin).padStart(5)}`,
  `settledHP ${String(a.settledFloor).padStart(4)}`,
  `| ${Object.entries(a.outc).map(([k, v]) => `${k}:${v}`).join(' ')}`,
);

console.log('\n===== Q2: is each build viable? RAW (retreat OFF, bag OFF) — can it out-sustain the Location? =====');
for (const l of ['easy', 'hard']) {
  console.log(`--- ${l} ---`);
  for (const b of ['physical', 'magical', 'hybrid'])
    show(`${b}`, agg(M.deepMerge(cfg(b, l), { hero: { retreatHealthFraction: 0 }, loot: { keepChancePerKill: 0 } }), 200));
}

console.log('\n===== Q2b: each build at its BEST engagement vs hard (retreat OFF, bag OFF) =====');
for (const b of ['physical', 'magical', 'hybrid']) {
  console.log(`--- ${b} ---`);
  for (const en of [1, 2, 3, 4, 5])
    show(`engagement ${en}`, agg(M.deepMerge(cfg(b, 'hard'), { hero: { retreatHealthFraction: 0, engagement: en }, loot: { keepChancePerKill: 0 } }), 150));
}

console.log('\n===== Q2c: gear multiplier — how much gear opens hard? (physical & magical, best engagement) =====');
for (const b of ['physical', 'magical']) {
  console.log(`--- ${b} ---`);
  for (const m of [1.0, 1.1, 1.25, 1.4, 1.6]) {
    const h = BUILDS[b];
    const scaled = { ...h, physicalDamage: h.physicalDamage * m, magicalDamage: h.magicalDamage * m, health: h.health * m, attackSpeed: h.attackSpeed * (1 + (m - 1) * 0.4) };
    show(`x${m}`, agg(M.deepMerge({ hero: scaled, location: LOCATIONS.hard }, { hero: { retreatHealthFraction: 0 }, loot: { keepChancePerKill: 0 } }), 150));
  }
}

console.log('\n===== Q1a: REALISTIC run (retreat@HP 0.25 + bag-full 0.9 ON) — what ends a Run? =====');
for (const l of ['easy', 'hard'])
  for (const b of ['physical', 'magical', 'hybrid'])
    show(`${b} @ ${l}`, agg(cfg(b, l), 200));

console.log('\n===== Q1b: endless spawn (finiteRoster OFF) — is the Encounter still a unit? =====');
for (const l of ['easy', 'hard'])
  for (const b of ['physical', 'hybrid'])
    show(`${b} @ ${l}`, agg(M.deepMerge(cfg(b, l), { location: { finiteRoster: false } }), 150));

console.log('\n===== Q1c: finite Run (encounterCap 8) vs endless — easy, realistic =====');
for (const b of ['physical', 'magical', 'hybrid']) {
  show(`${b} endless`, agg(cfg(b, 'easy'), 150));
  show(`${b} cap8`, agg(M.deepMerge(cfg(b, 'easy'), { run: { finiteEncounters: true, encounterCap: 8 } }), 150));
}

console.log('\n===== Q3: CastThreshold — trickle vs burst (magical @ hard eng4, retreat OFF bag OFF) =====');
for (const th of [0.05, 0.2, 0.4, 0.6, 0.8, 0.95])
  show(`threshold ${th}`, agg(M.deepMerge(cfg('magical', 'hard'), { hero: { retreatHealthFraction: 0, castThreshold: th }, loot: { keepChancePerKill: 0 } }), 150));

console.log('\n===== traces =====');
trace('physical @ hard eng2 (retreat off)', M.deepMerge(cfg('physical', 'hard'), { hero: { retreatHealthFraction: 0, engagement: 2 }, loot: { keepChancePerKill: 0 } }), 7, 120);
trace('magical @ hard eng4 (retreat off)', M.deepMerge(cfg('magical', 'hard'), { hero: { retreatHealthFraction: 0, engagement: 4 }, loot: { keepChancePerKill: 0 } }), 7, 120);

function trace(label, config, seed, seconds) {
  const st = M.createState(config, seed);
  const steps = Math.round(seconds / st.cfg.tick);
  const marks = [];
  for (let i = 0; i < steps && st.phase !== 'ended'; i++) {
    for (const e of M.step(st)) if (e.t === 'clear' || e.t === 'end') marks.push(`t${st.time.toFixed(0)} ${e.t === 'clear' ? 'CLR' + e.encounter : e.outcome}(hp${(st.hero.hp / st.hero.maxHp * 100).toFixed(0)}%)`);
  }
  console.log(`  ${label}: cleared ${st.encountersCleared} killed ${st.enemiesDefeated} | ${st.phase === 'ended' ? st.outcome.outcome : 'ongoing hp' + (st.hero.hp / st.hero.maxHp * 100).toFixed(0) + '%'} | ${marks.slice(0, 12).join('  ')}`);
}
