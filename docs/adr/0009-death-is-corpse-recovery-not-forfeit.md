---
status: accepted, not yet implemented
---

# Death is corpse-recovery, not haul-forfeit

When a Run ends in **Death**, something must be lost — the glossary pins Death as "under
a penalty" and "not a game-over." The MVP research doc's first sketch was "Recall keeps
the haul, Death loses most of it" (immediate forfeit). A domain pass had also settled
that loot lifecycle is per-Run: nothing on the ground survives a Run ending, on Recall
or Death alike.

Death does **not** forfeit the Run's loot outright. Equipped gear is never touched. The
penalty is lost XP, a fee off banked currency, and the bag's contents set aside as a
**Corpse** at the Location where the hero was downed. The Corpse is recovered by
re-entering that Location and picking the items back up. There is only one Corpse; a
second Death before recovery destroys the unclaimed one; it persists between Sessions.

A flat forfeit is clean but punishing enough to discourage pushing at all; no material
loss is toothless. Corpse-recovery keeps a real cost — the XP and currency hits are
immediate and unrecoverable, and the corpse run itself risks a second Death that erases
everything — while giving a careful player a route back to their loot. It also makes the
fixed-difficulty Location ladder (see `CONTEXT.md`, *Location*) bite: a Corpse stranded
at the harder Location is only recoverable by surviving there again.

## Consequences

- This reintroduces loot that persists between Runs, for the Death case only — which the
  domain decision had ruled out for Recall-overflow. Recall still deletes uncollected
  **Drops**; only Death produces a Corpse.
- The Corpse is state the save format must carry (it survives a Session quit), unlike
  all other mid-Run state, which is discarded on quit.
- "A second Death destroys the Corpse" bounds the mechanic to at most one persistent
  loot object at a time — deliberate, to keep both the rule and its serialization
  simple.
- Harsh for an MVP by the owner's own assessment; accepted now, revisitable once the
  loop is played.
