---
status: accepted
---

# A Send fields a Population, and progress is one Unlock graph

ADR-0010 settled "endless Encounters, no cap, no rescale": a finite Run was dead config, because the bag or
the retreat line always ended a Run first. Two things changed. Retreat is gone (hero-travel spec), so nothing
ends a Run on its own, and the game needs something to finish: a Location a new hero can Complete, and a next
Location that opens because of it. "Finished" needs a home in the model, and so does "opened".

## Decision

1. **A Send fields a Population.** A Location authors a `[min,max]` count of Encounters, rolled on every Send
   and Relocate. After the last clear the Location is **Complete**: the fight stops and the hero stands where
   he is. ADR-0010's Run-scale bullet ("endless Encounters, no cap") is superseded. The Encounter, its fixed
   Roster and the beat between Encounters are unchanged.
2. **Progress is one Unlock graph over Locations, Quests and locked Town Stops.** Each Unlockable names what
   it Unlocks when it reaches Active and when it reaches Complete, on the unlocking side, and several sources
   are an OR. A state is ordered, `Hidden < Locked < Active < Complete`, and only moves forward. Only the
   Completed set is saved; every other state is derived from it.

## Considered options

**Finite Encounters as a Run mode, judged dead in ADR-0010.** That judgement measured how often a cap ended a
Run before the bag or the retreat line did, under a model with nothing to finish. With Retreat gone and the
Complete as the reward, the Population is the goal of a Send, not a cap on it. Rejected as no longer
applying.

**Only Quests unlock things.** One kind of link, but every Location step would need an authored Quest ("Clear
Thornwood") to open the next, and a Location could never open anything by being Completed. The owner wants a
Location's Complete to open the next Location without a Quest. Rejected.

**Requirements authored on the unlocked side** (a Location lists what it needs). Same data, but it reads
opposite to how a player experiences progress ("Completing Thornwood opens Ashfall"), and an OR becomes a list
on the target instead of membership in two sources' lists. Rejected; the registry builds the reverse index.

**Store the Active set too.** Faster to read, but two saved sets can disagree. Rejected; Active is derived.

## Consequences

- The Encounter simulation gains a Complete phase, and the tests that pin the endless loop are rewritten.
- The Hero save gains the Completed ids, and the hero saves the moment something Completes, even mid-Run.
- A broken graph (a loop, an unreachable Unlockable, a Town Stop that claims to Complete) fails at load.
- AND gates, other Quest objectives and Quest rewards can be added to the graph later without a new mechanism.
