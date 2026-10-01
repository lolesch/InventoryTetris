---
status: accepted
---

# Entry points request the Inventory Context, and panels derive from it

A Town Stop's **Side Panel** used to announce its own Side Panel Context from its
appear and disappear hooks, and clear it on the way out. Nothing asks for a context
anywhere; every context is a fact some panel publishes about itself. That direction is
now inverted: entry points — a toggle's click or hotkey edge, a Run phase change — request
an **Inventory Context**, and every panel subscribes and derives its own visibility from
the answer. No panel announces anything.

## What made the old direction right, and what made it wrong

Announcing was right while every context meant exactly one panel. Then a context is a
1:1 fact and the panel that is up *is* the context — the announcement costs nothing, is
always current by construction, and cannot be stale, because the two things are the same
thing. #75 built on that: move the announcement to the panel's lifecycle hooks and "this
context is active" and "this panel is visible" become one fact instead of two that happen
to agree.

It stopped being right the moment the Hero Panel joined every context. The rule had always
been "a trade context is the Hero Panel **plus** a Town Stop's panel", and saying that out
loud is enough to break the mechanism: **a panel that belongs to every context can never
be the thing that names one.** The Hero Panel would have to announce a context it is a
member of, or not announce while being up — and either way the code needs a guard ("don't
announce if the current context already contains me") to hold the rule in place. A guard
is the evidence that the mechanism is the wrong shape rather than the rule being hard.

The arity was wrong too, and no step caught it because each ticket refined the
implementation instead of questioning the direction. The mechanism mapped panel →
context. The invariant maps context → panels. Backwards, and one-to-many against
one-to-one. #57, #74 and #75 each made the announcing direction work a little better
without ever asking what the mapping should have been.

The states the old direction could represent but the rule forbids make it concrete: a Town
Stop context with no Hero Panel; a context that says `Vendor` while the Vendor panel is
hidden; "Hero Panel open" indistinguishable from "nothing open", since both are `None`.
Visibility and context were two independently settable facts that could disagree — and
they did: a panel's group-aware fade path runs the full disappear lifecycle, context clear
included, on a panel that was never shown.

## Considered options

**Keep announcing, and guard the Hero Panel.** The guard is the smallest diff: "if the
context already contains me, don't announce." It leaves the mechanism *satisfying* the
rule rather than *expressing* it — every future Town Stop has to remember not to
double-announce, and a panel that belongs to no context, or to several, has no defined
behaviour at all. The rule stays in the panel's head instead of in one table.

**Keep a second authority alongside the subscription.** Derive visibility from the context
but leave the announcements running, so anything still reading the announced value keeps
working. Two authorities over one fact is the bug class the swap exists to remove, and it
is the same mistake #85 found in the toggles: a pressed button and an open panel that can
disagree.

**Derive everything, from one statement, and let the type forbid the illegal states.** The
active context is single-valued, so `Stash` and `Vendor` cannot both be active; the
*derived panel set* is a separate flags type, so a context can legitimately name two
panels. Keeping them as one flags type would make "both Town Stops open" representable —
the over-expression this rework exists to remove. Mutual exclusion over the left panels
then falls out of the type instead of being enforced at runtime by a group, so the
exclusivity group *over the side panels* is deleted rather than kept alongside. The
`PanelGroup` class itself is untouched and still used where a group is the right shape —
the InTown/InFields faces, for instance, which are a phase pair rather than a context.

## Consequences

The context is the input; panels are the output. Adding a Town Stop is authoring a value on
a panel and a toggle, not wiring a graph — a panel references only its own context and
nothing else, and the Hero Panel is referenced by nothing at all, because its membership
in every non-`None` context is stated once in the derivation table.

**Closing is always `None`, with no per-context clear.** The idempotent clear existed only
because independently-announcing panels could each try to close a context they did not
own. With requests, that cannot happen, so there is nothing to be idempotent about — and
closing a Town Stop's panel closes the Hero Panel with it rather than falling back to
`Hero`. One keypress ends the whole trading situation, which is what the rule says and
what the genre convention does.

**A handover is one change, not two.** Stash to Vendor is a single context change with no
intermediate `None`, so the Hero Panel's own derived answer never flips and it stays up
throughout, with no flicker. This is a deliberate behaviour change from #75's
"handover still publishes `None` between the two announcements" criterion. The two
subscribers that depended on that gap both key on the *new* value rather than on the
`None` — the Sell Basket cancels on any context that is not the Vendor, the Healer refills
on entering the Healer — so both still fire exactly once per handover, and now fire only
on a genuine change rather than on a re-request of the context already active.

**One layer decides visibility.** The toggle drives the request and nothing else — it no
longer fades the panel it drives, and its pressed visual resyncs from the context so a
button and a panel cannot disagree. A staged Sell Basket sale cancels the instant the
Vendor leaves, not once its fade finishes.

**A context with no rows moves nothing.** The `Hero` and `Healer` contexts have no sink
rows yet, so a Quick Move there does nothing. Both are stated outcomes, not omissions: the
Hero Panel's sink would be Equipment, which duplicates right-click and has no ticket, and
the Healer gained the Vendor's Sell Basket rows in #121, so it no longer resolves to
nothing. If the Hero Panel's row lands, it is a row in the routing table, not a change to
the mechanism.

**The Healer refill lives in `CharacterProvider`, not on a scene object.** The provider
subscribes to the context and refills on a genuine entry into `Healer` — the same
"key on the new value" rule the Sell Basket uses. An earlier `HealerAction` component placed
in the scene did the same job and only added something to misplace.

The rule itself lives in an engine-free `InventoryContextState` below the provider, for the
reason #54 gave: the provider compiles into `Assembly-CSharp`, which no test assembly can
reference. The provider carries the state to the scene and forwards; it is not the rule.
