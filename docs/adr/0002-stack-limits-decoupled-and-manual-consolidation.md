# Coin stack limits are decoupled from ratios, and consolidation is manual

Stack limits used to equal each coin's conversion ratio, so a stack could never hold a
full tier's worth and the wallet silently capped around 1199 base units. Limits are now
independent (iron 120, copper 60, silver 20, gold 12) and descend, so bigger coins are
bulkier and hoarding iron costs real grid space — the Tetris expression of "iron is
cheap".

Coins no longer auto-upgrade when a stack fills. They accumulate without bound, which is
what lifts the ceiling and lets the player actually *see* a pile of each coin. Converting
upward is a deliberate `Consolidate()` — value-preserving, remainder left in place —
exposed as a button beside Sort.

## Consequences

`AutoConsolidate` exists as a virtual on the container returning `false`, so a future
stash can opt into automatic consolidation without rework. A reader who expects full
stacks to roll over is looking at a deliberate removal, not a missing feature.
