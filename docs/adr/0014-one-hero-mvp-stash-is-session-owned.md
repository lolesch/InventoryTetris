---
status: accepted
---

# One hero for the MVP: the Stash is Session-owned

The Stash is described as where loot can be stored *and exchanged among heroes*. The
glossary cannot express that: a **Session** owns "the hero, the four containers, the
wallet and XP", and a Stash shared between heroes cannot be Session-owned — it needs a
persistence tier above Session (an account or profile), and no such term exists.

For the MVP there is one **Hero** per Session, so the Stash is Session-owned and
persists with it. No new tier is introduced and no term is coined for one. The shared
Stash is deferred, not rejected.

Three questions are deliberately left open until a shared Stash is actually on the
roadmap, because they are product calls rather than modelling ones:

1. What the tier above Session is called (it will need a `CONTEXT.md` entry with an
   *Avoid* list like every other term).
2. Whether the Stash alone moves up to it, or the Wallet too (a shared bank).
3. Which of the four containers, if any, stay per-hero.

## Consequences

- Session persistence may serialise the Session with its containers as a unit today.
  That is the natural implementation and it is correct for the MVP — but it is a
  *decision with a known exit cost*, not a neutral default. Keep the Stash and Wallet
  save sections separable from the hero's, so lifting them to a higher tier later is a
  change of owner rather than a format rewrite.
- `CONTEXT.md` carries a deferral note on the **Session** entry and on the Stash role in
  **Inventory / Stash / Supply / Sell Basket**, so the next reader need not re-derive
  this.
- Revisit when picking among several saved heroes (see *Hero*) is scheduled — the two
  features are the same decision.

## Amended by ADR-0015

"Session-owned" above reads as *held by the Session through the Hero State*: the Session
no longer owns the hero, containers, Wallet or XP directly. They live in the **Hero
State**, which the Session holds and a hero load replaces as a unit. The decision is
unchanged: the Stash is per-hero for the MVP and saves with the Hero State, and lifting it
to a tier above the Session later changes its owner, not the format.
