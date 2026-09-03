/* =============================================================================
 * combat-model.js — pure combat-cluster simulation
 * InventoryTetris MVP sim loop, issue #18 (the /prototype).
 *
 * THROWAWAY. No DOM, no Unity. Deterministic given an injected RNG. The shape
 * mirrors ADR-0010 so a validated cadence/spawn/threshold rule can be lifted into
 * InventorySystem.Encounter later; the numbers here are the thing being tuned.
 *
 * Model, faithful to the live code where it exists:
 *   - Strike : flat PhysicalDamage, cadence 1/AttackSpeed, single lowest-HP enemy.
 *              (ADR-0010: the "x (1 + AttackSpeed*0.01)" term is dropped — a real
 *               cadence already carries AttackSpeed through frequency.)
 *   - Cast   : flat MagicalDamage to each of the N highest-HP enemies, gated by a
 *              CastThreshold hysteresis on Resource, spending castCost each,
 *              never faster than castCadence.
 *   - Enemy  : one parametric archetype, Strike only, every stat off SourceLevel.
 *   - Mitigation : dmg * (1 - resist*0.01)   (BaseCharacterExtensions.CalculateReceivingDamage)
 *   - XP     : exp * (1 + (sourceLevel - heroLevel)/100), Experience max grows
 *              heroLevel*100 + 80 per level  (LocalPlayer.GainExperience)
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
    // --- defence ---
    health: 432,
    healthRegen: 3,            // per sec
    armor: 26,                 // % mitigation
    // --- behaviour (the six sliders) ---
    castThreshold: 0.4,        // charge Resource to this fraction, then cast to empty
    engagement: 3,             // soft count of enemies to keep engaged
    retreatHealthFraction: 0.25,
    recallBagFillFraction: 0.9,
  },

  combat: {
    castCost: 16,              // flat Resource per cast
    castCadence: 0.35,         // min seconds between casts (the burst ceiling; steady cadence = castCost/regen ~ 1.07s)
    castTargets: 3,
  },

  location: {
    sourceLevel: 3,
    // enemy archetype: stat = base + perLevel * S^exp  (one archetype, Strike only)
    enemyHealth:      { base: 17, perLevel: 19,  exp: 1.11 },
    enemyDamage:      { base: 0.9, perLevel: 0.78, exp: 1.0 },
    enemyArmor:       { base: 0,  perLevel: 0.65, exp: 1.0 },
    enemyAttackSpeed: 0.8,
    xpPerKill: 22,

    // spawn schedule
    finiteRoster: true,
    roster: [10, 10],          // [min,max] total enemies drawn per Encounter
    initialSpawn: 2,           // present at t=0
    spawnBatch: [1, 2],        // [1,1] trickle ; [4,4] charging Pack
    spawnInterval: 3.0,
    spawnJitter: 0.7,
  },

  loot: {
    keepChancePerKill: 0.4,    // items that pass the filter AND are worth bag space
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

function enemyArchetype(loc) {
  const S = loc.sourceLevel;
  return {
    maxHealth: curve(loc.enemyHealth, S),
    damage: curve(loc.enemyDamage, S),
    armor: curve(loc.enemyArmor, S),
    attackSpeed: loc.enemyAttackSpeed,
  };
}

/* ---- state ---------------------------------------------------------------- */

function createState(config, seed) {
  const cfg = deepMerge(DEFAULT_CONFIG, config || {});
  const rng = mulberry32(seed >>> 0);
  const arch = enemyArchetype(cfg.location);

  const st = {
    cfg, rng, arch,
    time: 0,
    phase: 'fighting',           // 'fighting' | 'beat' | 'ended'
    beatUntil: 0,
    outcome: null,               // set when phase === 'ended'

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
    enemies: [],                 // {hp,maxHp,strikeTimer,id}
    nextEnemyId: 1,
    spawnedThisEncounter: 0,
    rosterThisEncounter: 0,
    spawnTimer: 0,

    // running Run totals
    enemiesDefeated: 0,
    xpGained: 0,
    bagCells: 0,
    itemsKept: 0,
    minHealthFraction: 1,

    // instrumentation (per-window dps meters)
    dmgStrike: 0, dmgCast: 0, dmgIn: 0,
  };

  beginEncounter(st);
  return st;
}

function rollRoster(st) {
  const [lo, hi] = st.cfg.location.roster;
  return Math.round(lo + st.rng() * (hi - lo));
}
function rollBatch(st) {
  const [lo, hi] = st.cfg.location.spawnBatch;
  return Math.max(1, Math.round(lo + st.rng() * (hi - lo)));
}

function spawnEnemy(st) {
  st.enemies.push({
    id: st.nextEnemyId++,
    hp: st.arch.maxHealth,
    maxHp: st.arch.maxHealth,
    strikeTimer: st.rng() * (1 / st.arch.attackSpeed), // desync
  });
}

function beginEncounter(st) {
  st.encounter += 1;
  st.enemies.length = 0;
  st.spawnedThisEncounter = 0;
  st.rosterThisEncounter = rollRoster(st);
  st.spawnTimer = st.cfg.location.spawnInterval;
  const first = Math.min(st.cfg.location.initialSpawn,
    st.cfg.location.finiteRoster ? st.rosterThisEncounter : Infinity);
  for (let i = 0; i < first; i++) { spawnEnemy(st); st.spawnedThisEncounter++; }
  st.phase = 'fighting';
}

/* ---- one tick ----------------------------------------------------------------
 * Returns an event list for the UI ('strike','cast','kill','spawn','clear',
 * 'levelup','end'). Pure w.r.t. st.rng; caller owns dt (the CombatClock delta).
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

  // ---- spawn schedule ----
  const loc = st.cfg.location;
  const rosterSpent = loc.finiteRoster && st.spawnedThisEncounter >= st.rosterThisEncounter;
  if (!rosterSpent) {
    st.spawnTimer -= dt;
    if (st.spawnTimer <= 0 && st.enemies.length < st.cfg.hero.engagement) {
      let batch = rollBatch(st);
      if (loc.finiteRoster) batch = Math.min(batch, st.rosterThisEncounter - st.spawnedThisEncounter);
      for (let i = 0; i < batch; i++) { spawnEnemy(st); st.spawnedThisEncounter++; ev.push({ t: 'spawn' }); }
      st.spawnTimer = loc.spawnInterval + (st.rng() * 2 - 1) * loc.spawnJitter;
    }
  }

  // ---- hero regen ----
  regenHero(st, dt);

  // ---- hero Strike : lowest-HP enemy ----
  const h = st.hero;
  h.strikeTimer += dt;
  const strikeInterval = 1 / st.cfg.hero.attackSpeed;
  if (h.strikeTimer >= strikeInterval && st.enemies.length) {
    h.strikeTimer = Math.min(h.strikeTimer - strikeInterval, strikeInterval); // 1 action / tick
    const target = lowestHp(st.enemies);
    const dmg = st.cfg.hero.physicalDamage; // enemies have no per-hit phys resist beyond armor
    applyToEnemy(st, target, dmg, ev);
    st.dmgStrike += dmg;
    ev.push({ t: 'strike', target: target?.id, dmg });
  }

  // ---- hero Cast : hysteresis on Resource, N highest-HP enemies ----
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
        for (const tg of targets) applyToEnemy(st, tg, per, ev);
        st.dmgCast += per * targets.length;
        ev.push({ t: 'cast', targets: targets.map(x => x.id), per });
      } else if (h.resource < c.castCost) {
        h.casting = false;
      }
    }
  }

  // ---- enemy Strikes : all onto the hero ----
  const eInterval = 1 / st.arch.attackSpeed;
  for (const e of st.enemies) {
    e.strikeTimer += dt;
    if (e.strikeTimer >= eInterval) {
      e.strikeTimer = Math.min(e.strikeTimer - eInterval, eInterval);
      const raw = st.arch.damage;
      const mit = raw * (1 - st.cfg.hero.armor * 0.01);
      h.hp -= mit;
      st.dmgIn += mit;
    }
  }
  h.hp = Math.max(0, h.hp);
  st.minHealthFraction = Math.min(st.minHealthFraction, h.hp / h.maxHp);

  // ---- resolve deaths / end conditions ----
  if (h.hp <= 0) { end(st, 'Died'); ev.push({ t: 'end', outcome: 'Died' }); return ev; }

  // remove dead enemies (already credited in applyToEnemy)
  st.enemies = st.enemies.filter(e => e.hp > 0);

  // encounter clear?
  const spent = loc.finiteRoster && st.spawnedThisEncounter >= st.rosterThisEncounter;
  if (spent && st.enemies.length === 0) {
    st.encountersCleared += 1;
    ev.push({ t: 'clear', encounter: st.encounter });
    if (st.cfg.run.finiteEncounters && st.encountersCleared >= st.cfg.run.encounterCap) {
      end(st, 'Recalled:Cap'); ev.push({ t: 'end', outcome: 'Recalled:Cap' }); return ev;
    }
    st.phase = 'beat';
    st.beatUntil = st.time + st.cfg.beat;
  }

  // behaviour triggers (auto-Recall)
  if (h.hp / h.maxHp <= st.cfg.hero.retreatHealthFraction) {
    end(st, 'Recalled:HP'); ev.push({ t: 'end', outcome: 'Recalled:HP' }); return ev;
  }
  if (st.bagCells / st.cfg.loot.bagCells >= st.cfg.hero.recallBagFillFraction) {
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
    // XP per kill (LocalPlayer.GainExperience shape)
    const exp = st.cfg.location.xpPerKill;
    const bal = exp * (1 + (st.cfg.location.sourceLevel - st.hero.level) / 100);
    st.hero.xp += bal;
    st.xpGained += bal;
    while (st.hero.xp >= st.hero.xpMax) {
      st.hero.xp -= st.hero.xpMax;
      st.hero.level += 1;
      st.hero.xpMax += st.hero.level * 100 + 80;
      ev && ev.push({ t: 'levelup', level: st.hero.level });
    }
    // loot → bag pressure
    if (st.rng() < st.cfg.loot.keepChancePerKill) {
      st.bagCells += st.cfg.loot.avgItemCells;
      st.itemsKept += 1;
    }
    ev && ev.push({ t: 'kill', id: e.id });
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

function end(st, outcome) {
  st.phase = 'ended';
  st.outcome = {
    outcome,
    encountersCleared: st.encountersCleared,
    enemiesDefeated: st.enemiesDefeated,
    duration: round1(st.time),
    heroLevelStart: 1,
    heroLevelEnd: st.hero.level,
    xpGained: Math.round(st.xpGained),
    itemsKept: st.itemsKept,
    bagFill: +(st.bagCells / st.cfg.loot.bagCells).toFixed(2),
    minHealthFraction: +st.minHealthFraction.toFixed(3),
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
  // "settled" health floor: min HP fraction seen after the 30s opening (Inf if it never got there)
  const late = hpTrace.slice(6);
  st.outcome.settledHpFloor = late.length ? Math.min(...late) : null;
  return st.outcome;
}

function sweep(config, seeds = 200) {
  const rows = [];
  for (let s = 0; s < seeds; s++) rows.push(simulate(config, 1000 + s * 7919));
  const died = rows.filter(r => r.outcome === 'Died');
  const num = arr => arr.slice().sort((a, b) => a - b);
  const pct = (arr, p) => { const a = num(arr); return a.length ? a[Math.min(a.length - 1, Math.floor(p * a.length))] : 0; };
  const mean = arr => arr.reduce((x, y) => x + y, 0) / (arr.length || 1);
  return {
    seeds,
    deathRate: +(died.length / rows.length).toFixed(3),
    encMed: pct(rows.map(r => r.encountersCleared), 0.5),
    encMean: +mean(rows.map(r => r.encountersCleared)).toFixed(1),
    durMed: pct(rows.map(r => r.duration), 0.5),
    xpPerMin: +mean(rows.map(r => r.xpGained / Math.max(1, r.duration / 60))).toFixed(0),
    itemsMed: pct(rows.map(r => r.itemsKept), 0.5),
    minHpMean: +mean(rows.map(r => r.minHealthFraction)).toFixed(2),
    outcomes: tally(rows.map(r => r.outcome)),
  };
}
function tally(list) {
  const m = {};
  for (const x of list) m[x] = (m[x] || 0) + 1;
  return m;
}

/* ---- exports ------------------------------------------------------------- */
const API = { mulberry32, DEFAULT_CONFIG, deepMerge, createState, step, simulate, sweep, enemyArchetype, curve };
if (typeof module !== 'undefined' && module.exports) module.exports = API;
if (typeof window !== 'undefined') window.CombatModel = API;
