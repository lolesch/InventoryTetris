# Magic find is Diablo II's rarest-first cascade, applied to success mass only

Magic find walks the rarity tiers rarest-first — try Unique, then Rare, then Magic,
remainder Common — with each rung's conditional probability multiplied by
`1 + eff/100`, where `eff = mf x F / (mf + F)` uses Diablo II's own saturation factors
(Unique 250, Rare 600, Magic linear). Conditional rungs are *derived* from the authored
probability vector rather than authored separately, so magic find of 0 reproduces the
authored table exactly.

The **fail bucket is excluded from the cascade entirely**: the transform runs on success
mass and is rescaled by `1 - P(NoDrop)`, making the chance of getting nothing invariant
under magic find by construction. The previous implementation violated this, and also
inverted the stat — more magic find made loot worse.

## Considered options

Scaling the rare tiers' weights directly is the obvious alternative and was rejected: it
cannot hold `P(NoDrop)` fixed, it has no principled saturation, and it makes the authored
table stop meaning what it says as soon as the stat is non-zero. The cascade is a pure
transform to a new probability vector which is then sampled through the ordinary path, so
there stays exactly one sampling code path in the game.
