import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const M = require('./combat-model.js');

// quick matchup math for a build vs a location archetype
const BUILDS = {
  physical: { physicalDamage: 40, attackSpeed: 1.6, magicalDamage: 6, resourceMax: 45, resourceRegen: 6, health: 440, armor: 30, castCost: 16 },
  magical:  { physicalDamage: 9,  attackSpeed: 1.0, magicalDamage: 26, resourceMax: 130, resourceRegen: 20, health: 410, armor: 20, castCost: 16 },
  hybrid:   { physicalDamage: 26, attackSpeed: 1.3, magicalDamage: 15, resourceMax: 85, resourceRegen: 15, health: 425, armor: 25, castCost: 16 },
};
const LOC = {
  easy: { S: 2, hp: { base: 15, perLevel: 18, exp: 1.1 }, dmg: { base: 0.8, perLevel: 0.7, exp: 1 }, armor: { base: 0, perLevel: 0.5, exp: 1 }, as: 0.75 },
  hard: { S: 5, hp: { base: 20, perLevel: 20, exp: 1.12 }, dmg: { base: 1.0, perLevel: 1.0, exp: 1 }, armor: { base: 0, perLevel: 0.8, exp: 1 }, as: 0.85 },
};
const curve = (c, S) => c.base + c.perLevel * Math.pow(S, c.exp);
const castCadence = 0.55;

for (const [ln, L] of Object.entries(LOC)) {
  const eHp = curve(L.hp, L.S), eDmg = curve(L.dmg, L.S), eArm = curve(L.armor, L.S);
  console.log(`\n${ln.toUpperCase()}  S${L.S}: enemy HP ${eHp.toFixed(0)}, dmg ${eDmg.toFixed(1)} raw, armor ${eArm.toFixed(0)}%, AS ${L.as}`);
  console.log(`  incoming per enemy = ${(eDmg * L.as).toFixed(1)} raw DPS`);
  for (const [bn, b] of Object.entries(BUILDS)) {
    const strikeDps = b.physicalDamage * b.attackSpeed * (1 - eArm * 0.01);
    const steadyCast = Math.max(castCadence, b.castCost / b.resourceRegen);
    const castDps1 = b.magicalDamage / steadyCast;
    const castDps3 = b.magicalDamage * 3 / steadyCast;
    const mit = 1 - b.armor * 0.01;
    const ehp = b.health / mit; // effective HP vs physical
    const ttk1 = eHp / (strikeDps + castDps1);
    const ttk3set = eHp / (strikeDps + castDps3 / 3); // rough: one of a 3-pack
    console.log(`  ${bn.padEnd(9)} strikeDPS ${strikeDps.toFixed(0)} castDPS 1t/3t ${castDps1.toFixed(0)}/${castDps3.toFixed(0)}  | vs1 TTK ${ttk1.toFixed(1)}s  vs3 TTK/ea ${ttk3set.toFixed(1)}s  | eHP ${ehp.toFixed(0)} -> secs-to-die @3eng ${(ehp / (eDmg * L.as * mit * 3)).toFixed(0)}s`);
  }
}
