import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const M = require('./combat-model.js');

/* quick closed-form matchup math: build DPS shape vs each archetype at a source level.
 * The sim is the source of truth; this is just for sanity-checking a tuning change fast. */

const DEF = { health: 442, armor: 27 };
const BUILDS = {
  physical: { ...DEF, physicalDamage: 46, attackSpeed: 1.6, magicalDamage: 5, resourceRegen: 6 },
  magical:  { ...DEF, physicalDamage: 8, attackSpeed: 1.0, magicalDamage: 23, resourceRegen: 20 },
  hybrid:   { health: 476, armor: 30, physicalDamage: 28, attackSpeed: 1.35, magicalDamage: 14, resourceRegen: 14 },
};
const castCost = 16, castCadence = 0.35, castTargets = 3;
const arch = M.DEFAULT_CONFIG.archetypes;
const curve = (c, S) => c.base + c.perLevel * Math.pow(S, c.exp);

for (const S of [2, 5]) {
  console.log(`\n===== SourceLevel ${S} =====`);
  for (const [an, a] of Object.entries(arch)) {
    const hp = curve(a.health, S), dmg = curve(a.damage, S), arm = curve(a.armor, S);
    console.log(`  ${an.padEnd(11)} HP ${hp.toFixed(0)}  raw dmg ${dmg.toFixed(1)} @AS ${a.attackSpeed} = ${(dmg * a.attackSpeed).toFixed(1)} raw DPS/body  armor ${arm.toFixed(0)}%  xp ${curve(a.xp, S).toFixed(0)}`);
    for (const [bn, b] of Object.entries(BUILDS)) {
      const strikeDps = b.physicalDamage * b.attackSpeed * (1 - arm * 0.01);
      const steadyCast = Math.max(castCadence, castCost / b.resourceRegen);
      const castPerTargetDps = b.magicalDamage / steadyCast;         // sustained, one target's share
      const strikeTtk = hp / strikeDps;                              // Strike alone, this body
      const castTtk = hp / castPerTargetDps;                         // Cast alone, if it's a Cast target
      const oneShot = b.physicalDamage * (1 - arm * 0.01) >= hp ? ' [Strike one-shots]' : '';
      console.log(`    ${bn.padEnd(9)} strikeDPS ${strikeDps.toFixed(0).padStart(3)}  cast/target ${castPerTargetDps.toFixed(0).padStart(2)}  | Strike-TTK ${strikeTtk.toFixed(1)}s  Cast-TTK ${castTtk.toFixed(1)}s${oneShot}`);
    }
  }
  // incoming vs a nominal pack (3 packed bodies) + 1 single
  console.log('  -- incoming vs 3-pack + 1 single (hero armor 27%) --');
  for (const packed of ['brute', 'skirmisher']) {
    const p = arch[packed], s = arch[packed === 'brute' ? 'skirmisher' : 'brute'];
    const inc = (3 * curve(p.damage, S) * p.attackSpeed + curve(s.damage, S) * s.attackSpeed) * (1 - 27 * 0.01);
    console.log(`    ${packed.padEnd(11)} packed: ${inc.toFixed(1)} mitigated DPS onto the hero`);
  }
}
