/* =============================================================================
 * combat-model.js — pure combat-cluster simulation  (v2: two enemy archetypes)
 * InventoryTetris MVP sim loop, issue #18 (the /prototype).
 *
 * THROWAWAY. No DOM, no Unity. Deterministic given a seed. The shape mirrors
 * ADR-0010 + the handoff of 2026-09-03 (two-enemy model) so a validated
 * cadence / spawn / threshold rule can be lifted into the Run/Encounter module
 * later; the numbers here are the thing being tuned.
 *
 * Model, faithful to the live code where it exists:
 *   - Strike : flat PhysicalDamage, cadence 1/AttackSpeed, single LOWEST-HP enemy.
 *              enemy Armor mitigates it. (ADR-0010: the "x(1+AttackSpeed*0.01)"
 *              term is dropped — a real cadence carries AttackSpeed already.)
 *   - Cast   : flat MagicalDamage to each of the N HIGHEST-HP enemies, gated by a
 *              CastThreshold hysteresis on Resource, spending castCost, never
 *              faster than castCadence. Ignores Armor (magical).
 *   - Enemies: TWO parametric archetypes, both off SourceLevel —
 *       Brute      : high HP, slow hits, armored, LOW xp   (Cast food, Strike-resistant)
 *       Skirmisher : low HP,  fast hits, no armor,  HIGH xp (Strike food, Cast overkill)
 *     A Location Packs one type and trickles the other in singly.
 *   - Mitigation : dmg * (1 - resist*0.01)   (BaseCharacterExtensions.CalculateReceivingDamage)
 *   - XP     : settles PER ENCOUNTER CLEAR (handoff decision 1), summed over the
 *              Encounter's actual roster, each enemy's per-type xp balanced by
 *              exp * (1 + (sourceLevel - heroLevel)/100). A Run Recalled mid-
 *              Encounter FORFEITS that Encounter's accrued XP. Experience max
 *              grows heroLevel*100 + 80 per level (LocalPlayer.GainExperience).
 *   - Loot Drops stay per-kill (bag pressure accrues as enemies fall).
 * ========================================================================== */

function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const DEFAULT_CONFIG = {
  tick: 0.1,          // seconds per CombatClock tick
  beat: 1.0,          // seconds between Encounters
  maxSeconds: 900,    // sim safety cap (a survivable endless Run hits this)

  hero: {
    // --- physical half (Strike: flat PhysicalDamage, 1/AttackSpeed cadence) ---
    physicalDamage: 26,
    attackSpeed: 1.3,          // strikes / sec
    // --- magical half (Cast: flat MagicalDamage x castTargets, resource-gated) ---
    magicalDamage: 15,         // per target
    resourceMax: 85,
    resourceRegen: 15,         // per sec
    // --- defence --- (physical & magical share this exact budget; hybrid spends leftover here)
    health: 442,
    healthRegen: 3,            // per sec
    armor: 27,                 // % mitigation of incoming
    // --- behaviour (the six sliders) ---
    castThreshold: 0.4,        // charge Resource to this fraction, then cast to empty
    engagement: 3,             // soft count of enemies to keep engaged
    retreatHealthFraction: 0.25,
    recallBagFillFraction: 0.9,
  },

  combat: {
    castCost: 16,              // flat Resource per cast
    castCadence: 0.35,         // min seconds between casts (burst ceiling)
    castTargets: 3,
  },

  // TWO shared archetype curve-sets. stat = base + perLevel * S^exp.
  // Location supplies only SourceLevel + which type is Packed + the roster mix.
  archetypes: {
    brute: {
      health:      { base: 26, perLevel: 24,  exp: 1.12 },
      damage:      { base: 1.0, perLevel: 0.82, exp: 1.0 },
      armor:       { base: 3,   perLevel: 0.9,  exp: 1.0 },
      attackSpeed: 0.55,
      xp:          { base: 5,   perLevel: 3.5,  exp: 1.0 },
    },
    skirmisher: {
      health:      { base: 10,  perLevel: 9,    exp: 1.06 },
      damage:      { base: 0.5,  perLevel: 0.5,  exp: 1.0 },
      armor:       { base: 0,   perLevel: 0,    exp: 1.0 },
      attackSpeed: 1.6,
      xp:          { base: 13,  perLevel: 8,    exp: 1.0 },
    },
  },

  location: {                                     // default = Thornwood (Location 1)
    sourceLevel: 2,
    packed: 'brute',                              // Packs of this; the other trickles single
    roster: { brute: [9, 9], skirmisher: [5, 5] },// how many of each per Encounter
    packBatch: [2, 3],                            // Pack size for the packed type
    packedSpawnWeight: 0.6,                       // P(next spawn draws the packed type) while both remain
    initialSpawn: 2,
    spawnInterval: 2.6,
    spawnJitter: 0.7,
  },

  loot: {
    keepChancePerKill: 0.28,   // items that pass the filter AND are worth bag space
    avgItemCells: 4,
    bagCells: 72,
  },

  run: {
    finiteEncounters: false,
    encounterCap: 8,
  },
};

function deepMerge(base, over) {
  const out = Array.isArray(base) ? base.slice() : { ...base };
  for (const k of Object.keys(over || {})) {
    const v = over[k];
    out[k] = (v && typeof v === 'object' && !Array.isArray(v) && typeof base[k] === 'object')
      ? deepMerge(base[k], v) : v;
  }
  return out;
}

const curve = (c, S) => c.base + c.perLevel * Math.pow(S, c.exp);

function enemyArchetypes(cfg) {
  const S = cfg.location.sourceLevel;
  const one = a => ({
    maxHealth: curve(a.health, S),
    damage: curve(a.damage, S),
    armor: curve(a.armor, S),
    attackSpeed: a.attackSpeed,
    xp: curve(a.xp, S),
  });
  return { brute: one(cfg.archetypes.brute), skirmisher: one(cfg.archetypes.skirmisher) };
}
// back-compat alias
const enemyArchetype = enemyArchetypes;

/* ---- state ---------------------------------------------------------------- */

function zeroByType() { return { brute: 0, skirmisher: 0 }; }

function createState(config, seed) {
  const cfg = deepMerge(DEFAULT_CONFIG, config || {});
  const rng = mulberry32(seed >>> 0);
  const arch = enemyArchetypes(cfg);

  const st = {
    cfg, rng, arch,
    time: 0,
    phase: 'fighting',           // 'fighting' | 'beat' | 'ended'
    beatUntil: 0,
    outcome: null,

    hero: {
      hp: cfg.hero.health,
      maxHp: cfg.hero.health,
      resource: cfg.hero.resourceMax * 0.5,
      strikeTimer: 0,
      castTimer: 0,
      casting: false,
      level: 1,
      xp: 0,
      xpMax: 280,
    },

    encounter: 0,
    encountersCleared: 0,
    enemies: [],                 // {id,type,hp,maxHp,strikeTimer}
    nextEnemyId: 1,
    spawned: zeroByType(),
    rosterN: zeroByType(),
    spawnTimer: 0,

    // per-Encounter XP accumulator (settles on clear, forfeited on Recall/Death)
    encounterXp: 0,
    encounterXpByType: zeroByType(),

    // running Run totals
    enemiesDefeated: 0,
    killsByType: zeroByType(),
    xpGained: 0,                  // banked (post-clear) only
    xpByType: zeroByType(),       // banked, per type
    xpForfeited: 0,               // accrued XP lost to a mid-Encounter Recall/Death
    bagCells: 0,
    itemsKept: 0,
    minHealthFraction: 1,

    // instrumentation
    dmgStrike: 0, dmgCast: 0, dmgIn: 0,
    dmgStrikeByType: zeroByType(),   // hero Strike damage dealt, by target type
    dmgCastByType: zeroByType(),     // hero Cast damage dealt, by target type
    dmgInByType: zeroByType(),       // incoming damage taken, by source type
    ttkSamples: { brute: [], skirmisher: [] }, // seconds each killed enemy was alive
  };

  beginEncounter(st);
  return st;
}

function rollRange(st, range) {
  const [lo, hi] = range;
  return Math.round(lo + st.rng() * (hi - lo));
}
function rollPack(st) {
  const [lo, hi] = st.cfg.location.packBatch;
  return Math.max(1, Math.round(lo + st.rng() * (hi - lo)));
}

function spawnEnemy(st, type) {
  const a = st.arch[type];
  st.enemies.push({
    id: st.nextEnemyId++,
    type,
    hp: a.maxHealth,
    maxHp: a.maxHealth,
    bornAt: st.time,
    strikeTimer: st.rng() * (1 / a.attackSpeed), // desync
  });
  st.spawned[type] += 1;
}

function rosterRemaining(st) {
  return {
    brute: st.rosterN.brute - st.spawned.brute,
    skirmisher: st.rosterN.skirmisher - st.spawned.skirmisher,
  };
}
function rosterSpent(st) {
  const r = rosterRemaining(st);
  return r.brute <= 0 && r.skirmisher <= 0;
}

function beginEncounter(st) {
  st.encounter += 1;
  st.enemies.length = 0;
  st.spawned = zeroByType();
  st.rosterN = {
    brute: rollRange(st, st.cfg.location.roster.brute),
    skirmisher: rollRange(st, st.cfg.location.roster.skirmisher),
  };
  st.encounterXp = 0;
  st.encounterXpByType = zeroByType();
  st.spawnTimer = st.cfg.location.spawnInterval;

  // initial presence: draw from the packed type first, then the other
  let left = st.cfg.location.initialSpawn;
  const order = [st.cfg.location.packed, other(st.cfg.location.packed)];
  for (const type of order) {
    while (left > 0 && st.spawned[type] < st.rosterN[type]) { spawnEnemy(st, type); left--; }
  }
  st.phase = 'fighting';
}
const other = t => (t === 'brute' ? 'skirmisher' : 'brute');

/* ---- one tick ----------------------------------------------------------------
 * Returns an event list for the UI. Pure w.r.t. st.rng; dt is the tick.
 */
function step(st) {
  if (st.phase === 'ended') return [];
  const dt = st.cfg.tick;
  const ev = [];
  st.time += dt;

  if (st.time > st.cfg.maxSeconds) { end(st, 'Timeout'); return ev; }

  if (st.phase === 'beat') {
    if (st.time >= st.beatUntil) beginEncounter(st);
    regenHero(st, dt);
    return ev;
  }

  // ---- spawn schedule : packed type in Packs, the other one at a time ----
  const loc = st.cfg.location;
  if (!rosterSpent(st)) {
    st.spawnTimer -= dt;
    if (st.spawnTimer <= 0 && st.enemies.length < st.cfg.hero.engagement) {
      const rem = rosterRemaining(st);
      let type;
      if (rem.brute > 0 && rem.skirmisher > 0) {
        type = st.rng() < loc.packedSpawnWeight ? loc.packed : other(loc.packed);
      } else {
        type = rem.brute > 0 ? 'brute' : 'skirmisher';
      }
      let batch = type === loc.packed ? rollPack(st) : 1;
      batch = Math.min(batch, rem[type]);
      for (let i = 0; i < batch; i++) { spawnEnemy(st, type); ev.push({ t: 'spawn', type }); }
      st.spawnTimer = loc.spawnInterval + (st.rng() * 2 - 1) * loc.spawnJitter;
    }
  }

  regenHero(st, dt);

  // ---- hero Strike : lowest-HP enemy (→ tends to eat Skirmishers) ----
  const h = st.hero;
  h.strikeTimer += dt;
  const strikeInterval = 1 / st.cfg.hero.attackSpeed;
  if (h.strikeTimer >= strikeInterval && st.enemies.length) {
    h.strikeTimer = Math.min(h.strikeTimer - strikeInterval, strikeInterval); // 1 action / tick
    const target = lowestHp(st.enemies);
    const dmg = st.cfg.hero.physicalDamage * (1 - st.arch[target.type].armor * 0.01);
    st.dmgStrike += dmg; st.dmgStrikeByType[target.type] += dmg;
    applyToEnemy(st, target, dmg, ev);
    ev.push({ t: 'strike', target: target.id, type: target.type, dmg });
  }

  // ---- hero Cast : N highest-HP enemies (→ tends to eat the Brute Pack) ----
  h.castTimer += dt;
  const c = st.cfg.combat;
  if (h.castTimer >= c.castCadence) {
    h.castTimer = Math.min(h.castTimer - c.castCadence, c.castCadence);
    if (!h.casting && h.resource >= st.cfg.hero.castThreshold * st.cfg.hero.resourceMax) h.casting = true;
    if (h.casting) {
      if (h.resource >= c.castCost && st.enemies.length) {
        h.resource -= c.castCost;
        const targets = highestHp(st.enemies, c.castTargets);
        const per = st.cfg.hero.magicalDamage;
        for (const tg of targets) { st.dmgCast += per; st.dmgCastByType[tg.type] += per; applyToEnemy(st, tg, per, ev); }
        ev.push({ t: 'cast', targets: targets.map(x => x.id), per });
      } else if (h.resource < c.castCost) {
        h.casting = false;
      }
    }
  }

  // ---- enemy Strikes onto the hero ----
  for (const e of st.enemies) {
    const a = st.arch[e.type];
    const eInterval = 1 / a.attackSpeed;
    e.strikeTimer += dt;
    if (e.strikeTimer >= eInterval) {
      e.strikeTimer = Math.min(e.strikeTimer - eInterval, eInterval);
      const mit = a.damage * (1 - st.cfg.hero.armor * 0.01);
      h.hp -= mit;
      st.dmgIn += mit; st.dmgInByType[e.type] += mit;
    }
  }
  h.hp = Math.max(0, h.hp);
  st.minHealthFraction = Math.min(st.minHealthFraction, h.hp / h.maxHp);

  if (h.hp <= 0) {
    st.xpForfeited += st.encounterXp;
    end(st, 'Died'); ev.push({ t: 'end', outcome: 'Died' }); return ev;
  }

  st.enemies = st.enemies.filter(e => e.hp > 0);

  // ---- encounter clear? settle XP here ----
  if (rosterSpent(st) && st.enemies.length === 0) {
    st.encountersCleared += 1;
    st.hero.xp += st.encounterXp;
    st.xpGained += st.encounterXp;
    st.xpByType.brute += st.encounterXpByType.brute;
    st.xpByType.skirmisher += st.encounterXpByType.skirmisher;
    while (st.hero.xp >= st.hero.xpMax) {
      st.hero.xp -= st.hero.xpMax;
      st.hero.level += 1;
      st.hero.xpMax += st.hero.level * 100 + 80;
      ev.push({ t: 'levelup', level: st.hero.level });
    }
    ev.push({ t: 'clear', encounter: st.encounter, xp: Math.round(st.encounterXp) });
    st.encounterXp = 0;
    st.encounterXpByType = zeroByType();
    if (st.cfg.run.finiteEncounters && st.encountersCleared >= st.cfg.run.encounterCap) {
      end(st, 'Recalled:Cap'); ev.push({ t: 'end', outcome: 'Recalled:Cap' }); return ev;
    }
    st.phase = 'beat';
    st.beatUntil = st.time + st.cfg.beat;
  }

  // ---- behaviour triggers (auto-Recall) — forfeit the in-progress Encounter XP ----
  if (st.cfg.hero.retreatHealthFraction > 0 && h.hp / h.maxHp <= st.cfg.hero.retreatHealthFraction) {
    st.xpForfeited += st.encounterXp;
    end(st, 'Recalled:HP'); ev.push({ t: 'end', outcome: 'Recalled:HP' }); return ev;
  }
  if (st.cfg.hero.recallBagFillFraction > 0 && st.bagCells / st.cfg.loot.bagCells >= st.cfg.hero.recallBagFillFraction) {
    st.xpForfeited += st.encounterXp;
    end(st, 'Recalled:Bag'); ev.push({ t: 'end', outcome: 'Recalled:Bag' }); return ev;
  }

  return ev;
}

function regenHero(st, dt) {
  const h = st.hero;
  if (h.hp > 0) h.hp = Math.min(h.maxHp, h.hp + st.cfg.hero.healthRegen * dt);
  h.resource = Math.min(st.cfg.hero.resourceMax, h.resource + st.cfg.hero.resourceRegen * dt);
}

function applyToEnemy(st, e, dmg, ev) {
  if (!e || e.hp <= 0) return;
  e.hp -= dmg;
  if (e.hp <= 0) {
    e.hp = 0;
    st.enemiesDefeated += 1;
    st.killsByType[e.type] += 1;
    st.ttkSamples[e.type].push(+(st.time - e.bornAt).toFixed(1));
    // XP accrues to the per-Encounter pot (balanced by the live GainExperience shape)
    const raw = st.arch[e.type].xp;
    const bal = raw * (1 + (st.cfg.location.sourceLevel - st.hero.level) / 100);
    st.encounterXp += bal;
    st.encounterXpByType[e.type] += bal;
    // loot → bag pressure (still per kill)
    if (st.rng() < st.cfg.loot.keepChancePerKill) {
      st.bagCells += st.cfg.loot.avgItemCells;
      st.itemsKept += 1;
    }
    ev && ev.push({ t: 'kill', id: e.id, type: e.type });
  }
}

function lowestHp(list) {
  let best = null;
  for (const e of list) if (e.hp > 0 && (!best || e.hp < best.hp)) best = e;
  return best;
}
function highestHp(list, n) {
  return list.filter(e => e.hp > 0).sort((a, b) => b.hp - a.hp).slice(0, n);
}

function median(arr) {
  if (!arr.length) return 0;
  const a = arr.slice().sort((x, y) => x - y);
  return a[Math.floor(a.length / 2)];
}

function end(st, outcome) {
  st.phase = 'ended';
  st.outcome = {
    outcome,
    encountersCleared: st.encountersCleared,
    enemiesDefeated: st.enemiesDefeated,
    killsByType: { ...st.killsByType },
    duration: round1(st.time),
    heroLevelEnd: st.hero.level,
    xpGained: Math.round(st.xpGained),
    xpByType: { brute: Math.round(st.xpByType.brute), skirmisher: Math.round(st.xpByType.skirmisher) },
    xpForfeited: Math.round(st.xpForfeited),
    xpPerMin: +(st.xpGained / Math.max(1, st.time / 60)).toFixed(0),
    itemsKept: st.itemsKept,
    bagFill: +(st.bagCells / st.cfg.loot.bagCells).toFixed(2),
    minHealthFraction: +st.minHealthFraction.toFixed(3),
    dmgInByType: { brute: Math.round(st.dmgInByType.brute), skirmisher: Math.round(st.dmgInByType.skirmisher) },
    dmgStrikeByType: { brute: Math.round(st.dmgStrikeByType.brute), skirmisher: Math.round(st.dmgStrikeByType.skirmisher) },
    dmgCastByType: { brute: Math.round(st.dmgCastByType.brute), skirmisher: Math.round(st.dmgCastByType.skirmisher) },
    ttkBrute: median(st.ttkSamples.brute),
    ttkSkirmisher: median(st.ttkSamples.skirmisher),
  };
}
const round1 = x => Math.round(x * 10) / 10;

/* ---- headless full-run driver (for sweeps) ------------------------------- */

function simulate(config, seed) {
  const st = createState(config, seed);
  let guard = 0;
  const cap = Math.ceil(st.cfg.maxSeconds / st.cfg.tick) + 10;
  const hpTrace = [];
  let nextSample = 5;
  while (st.phase !== 'ended' && guard++ < cap) {
    step(st);
    if (st.time >= nextSample) { hpTrace.push(+(st.hero.hp / st.hero.maxHp).toFixed(2)); nextSample += 5; }
  }
  if (st.phase !== 'ended') end(st, 'Timeout');
  st.outcome.hpTrace = hpTrace;
  const late = hpTrace.slice(6);
  st.outcome.settledHpFloor = late.length ? Math.min(...late) : null;
  return st.outcome;
}

function sweep(config, seeds = 200) {
  const rows = [];
  for (let s = 0; s < seeds; s++) rows.push(simulate(config, 1000 + s * 7919));
  const died = rows.filter(r => r.outcome === 'Died');
  const num = arr => arr.slice().sort((a, b) => a - b);
  const pctl = (arr, p) => { const a = num(arr); return a.length ? a[Math.min(a.length - 1, Math.floor(p * a.length))] : 0; };
  const mean = arr => arr.reduce((x, y) => x + y, 0) / (arr.length || 1);
  return {
    seeds,
    deathRate: +(died.length / rows.length).toFixed(3),
    encMed: pctl(rows.map(r => r.encountersCleared), 0.5),
    encMean: +mean(rows.map(r => r.encountersCleared)).toFixed(1),
    durMed: pctl(rows.map(r => r.duration), 0.5),
    xpPerMin: +mean(rows.map(r => r.xpPerMin)).toFixed(0),
    killsBrute: +mean(rows.map(r => r.killsByType.brute)).toFixed(1),
    killsSkirm: +mean(rows.map(r => r.killsByType.skirmisher)).toFixed(1),
    ttkBrute: +mean(rows.map(r => r.ttkBrute)).toFixed(1),
    ttkSkirm: +mean(rows.map(r => r.ttkSkirmisher)).toFixed(1),
    dmgInBrutePct: +(100 * mean(rows.map(r => r.dmgInByType.brute)) /
      Math.max(1, mean(rows.map(r => r.dmgInByType.brute + r.dmgInByType.skirmisher)))).toFixed(0),
    xpBrutePct: +(100 * mean(rows.map(r => r.xpByType.brute)) /
      Math.max(1, mean(rows.map(r => r.xpByType.brute + r.xpByType.skirmisher)))).toFixed(0),
    minHpMean: +mean(rows.map(r => r.minHealthFraction)).toFixed(2),
    settledFloorMean: (() => {
      const f = rows.map(r => r.settledHpFloor).filter(x => x != null);
      return f.length ? +mean(f).toFixed(2) : null;
    })(),
    outcomes: tally(rows.map(r => r.outcome)),
  };
}
function tally(list) {
  const m = {};
  for (const x of list) m[x] = (m[x] || 0) + 1;
  return m;
}

/* ---- exports ------------------------------------------------------------- */
const API = {
  mulberry32, DEFAULT_CONFIG, deepMerge, createState, step, simulate, sweep,
  enemyArchetypes, enemyArchetype, curve,
};
if (typeof module !== 'undefined' && module.exports) module.exports = API;
if (typeof window !== 'undefined') window.CombatModel = API;
