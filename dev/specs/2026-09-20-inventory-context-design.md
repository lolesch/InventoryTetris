# Inventory Context

Date: 2026-09-20
Status: Scoping spec — not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `feature/sidepanel-hotkey-rework` at `e0d3f8a`.
Supersedes: `dev/specs/2026-09-19-required-companion-panels-design.md`, committed only on
`feature/required-companion-panels`. That branch should not be merged.
Derived with `/rederive`; the route to it audited twice with `/drift-review`.

## Problem Statement

The player opens the Vendor and gets a Supply with nowhere to put anything — the Hero Panel
is not part of what opened, so a purchase has no visible destination and a Quick Move has no
target. Opening the Stash has the same shape. Closing one panel leaves the other one up with
nothing to trade against.

Underneath, "which Side Panel is open" is a fact each Side Panel announces about *itself*,
the moment it appears. That worked while every context meant exactly one panel. It stops
working the moment the rule is said out loud: a trade context is the Hero Panel **plus** a
Town Stop's panel. The Hero Panel belongs to every context, so it can never be the thing
that names one — and the mechanism would need a guard ("don't announce if the current
context already contains me") to pretend otherwise.

The same mismatch shows up as states the code can represent but the rule forbids: a Town
Stop context with no Hero Panel; an Inventory Context that says `Vendor` while the Vendor
panel is hidden; "Hero Panel open" being indistinguishable from "nothing open", because both
are `None`. Visibility and context are two independently settable facts, so they can
disagree — a panel's group-aware fade path runs the full disappear lifecycle, including the
context clear, on a panel that was never shown.

Quick Move routing has the matching problem: a hand-written matrix with one branch per
(context, source container) pair, which every new Town Stop grows by hand, and which has
already shipped one row with a passing test and no caller.

## Solution

One **Inventory Context** is the single answer to both questions. It is set by the entry
points, and everything else is derived from it:

- **Which panels are visible** — a context names the Hero Panel plus at most one Town Stop
  panel. Panels do not announce; they subscribe, and each one knows only its own context.
- **Where a Quick Move lands** — a context names one sink, with the Inventory as a constant
  hub. Everything inbound arrives in the Inventory; everything outbound leaves for the sink.

Inventories are never opened solo. Opening the Stash, the Vendor or the Healer opens the
Hero Panel with it. Closing the Hero Panel closes whatever needed it, and closing a Town
Stop's panel closes the Hero Panel too — either way the context becomes `None`.

The exclusivity group over the left panels goes away. With a single-valued context, "at most
one Town Stop panel is up" is a property of the type rather than a rule a group enforces at
runtime, and the second authority disappears with it.

## User Stories

1. As a player, I want opening the Vendor to also show my Hero Panel, so that a purchase has
   a visible destination.
2. As a player, I want opening the Stash to also show my Hero Panel, so that I can move
   Packages between them without an extra step.
3. As a player, I want opening the Healer to also show my Hero Panel, so that every Town Stop
   behaves the same way.
4. As a player, I want to open my Hero Panel on its own, so that I can check my gear without
   visiting a Town Stop.
5. As a player, I want closing my Hero Panel to close whatever Town Stop panel is open, so
   that I am never left looking at a Supply with nowhere to put things.
6. As a player, I want closing a Town Stop's panel to close my Hero Panel too, so that one
   keypress ends the whole trading situation.
7. As a player, I want switching from the Stash to the Vendor to leave my Hero Panel up
   throughout, so that it does not flicker every time I change Town Stop.
8. As a player, I want a shift-click in my Inventory to go wherever the open Town Stop says,
   so that I do not have to remember a different rule per panel.
9. As a player, I want a shift-click on equipped gear to go to the same place, so that
   unequipping and moving are one act.
10. As a player, I want a shift-click in the Stash to come back to my Inventory, so that
    retrieval is the same gesture as storage.
11. As a player, I want a shift-click in the Sell Basket to come back to my Inventory, so
    that I can unstage a Package I changed my mind about.
12. As a player, I want a shift-click on the Supply to buy, in every context, so that buying
    stays a Supply-local act.
13. As a player, I want a Package arriving in my Inventory from the Stash to auto-equip when
    its slot is empty and auto-equip is on, so that retrieving gear equips it.
14. As a player, I want my Hero Panel to be openable while I am in the Field, so that I can
    look at my gear during a Run.
15. As a player, I want the Town Stop panels to stay unreachable while I am in the Field, so
    that a stray keypress cannot open a panel I cannot use.
16. As a player, I want Send, Recall, Death and Go Venture to close any Town Stop panel, so
    that leaving Town leaves its business behind.
17. As a player, I want a staged sale to cancel whenever the Vendor panel goes away, by any
    route, so that Packages are never stranded in a basket I cannot see.
18. As a player, I want an in-flight purchase drag to return to the Supply when the Vendor
    goes away, for the same reason.
19. As a player, I want a Town Stop toggle's pressed visual to match whether its panel is
    actually up, so that the minimap never lies about where I am.
20. As a player, I want clicking an already-open Town Stop toggle to close it, so that the
    toggle is a toggle.
21. As a developer, I want one enum to be the single answer to "which panels are up" and
    "where does a Quick Move go", so that the two can never disagree.
22. As a developer, I want a panel to reference nothing but its own context, so that adding a
    Town Stop is authoring a value rather than wiring a graph.
23. As a developer, I want the Hero Panel to be referenced by nothing at all, so that its
    ubiquity is stated once in the context table rather than repeated per Town Stop.
24. As a developer, I want a context that forbids a state to be unable to represent it, so
    that an illegal pairing is an authoring impossibility rather than a bug to catch.
25. As a developer, I want Quick Move routing expressed as a per-context sink and source set,
    so that a new Town Stop adds a row instead of a branch.
26. As a developer, I want the exclusivity group over the left panels removed rather than
    kept alongside the subscription, so that "why is this panel hidden" has one answer.
27. As a developer, I want phase reachability to decide which contexts are available, so that
    Send and Go Venture drive the context rather than reaching into panels.
28. As a developer, I want the toggles' radio group to keep answering only "which button is
    pressed", so that the input/content split #74 established survives the swap.
29. As a developer, I want zero changes to any script under the shared Utility submodule, so
    that this stays a game-code rework of a concurrently-edited dependency (ADR-0011).
30. As a maintainer, I want the glossary term and an ADR updated in the same change, so that
    the next reader is not told the context is announced by the panel that owns it.

## Implementation Decisions

- **The enum.** `None`, `Hero`, `Stash`, `Vendor`, `Healer`. Single-valued: exactly one is
  active. `None` is a real state, as it is today. `InField` is deliberately **not** a member
  — the Hero Panel is openable during a Run, so the Run phase constrains which contexts are
  reachable rather than being one of them.
- **The name.** `SidePanelContext` is retired; the concept is no longer about Side Panels.
  The glossary term becomes **Inventory Context**, and the state object is renamed to match.
  `CONTEXT.md`'s *Side Panel Context*, *Side Panel*, *Hero Panel* and *Quick Move* entries
  all move with it — *Quick Move* currently says the destination is "chosen by which Side
  Panel is open", which stops being true.
- **The panel set is derived, not authored per pair.** A context maps to the Hero Panel plus
  at most one Town Stop panel. The Hero Panel's membership in every non-`None` context is
  stated once, in that mapping — not as a companion reference repeated on each Town Stop
  panel, and not as a branch inside each panel.
- **The panel set is a different type from the context.** The active context is single-valued
  so that `Stash` and `Vendor` cannot both be active; the derived set is a set so that a
  context can name two panels. Keeping them as one flags type would make the illegal
  combination representable, which is the over-expression this rework exists to remove.
- **Entry points request; panels derive.** A toggle calls its panel, which asks the provider
  to change context. The provider publishes; every panel derives its own visibility from the
  new value. The announcement moves off the panel's appear hook, where #75 put it, to the
  toggle's edge — which is where #57 originally had it, and which is correct once the Hero
  Panel joins every context.
- **Closing is always `None`.** There is no per-context clear. The idempotent clear existed
  only because independently-announcing panels could each try to close a context they did not
  own; with requests that cannot happen. Closing a Town Stop panel therefore does not fall
  back to `Hero` — it closes the Hero Panel too, which is what the rule says and what the
  genre convention does.
- **No intermediate `None` on handover.** Stash to Vendor is one context change, not a close
  followed by an open. This is a deliberate behaviour change from #75's contract; the two
  subscribers that depended on it already guard on "context is not Vendor", so both still
  fire, and the Hero Panel stops flickering.
- **`InventoryProvider` stays the source of truth**, holding the current context and
  forwarding to the engine-free state object exactly as it does today. #54's rationale is
  unchanged: the provider compiles into `Assembly-CSharp`, which no test assembly can
  reference, so the rule has to live below it to be reachable.
- **The routing table.** Per context: a constant hub (the Inventory), one sink, and a set of
  sources. `Hero` names Equipment as its sink but has no rows yet (see Out of Scope).
  `Stash` names the Stash as both sink and source. `Vendor` names the Sell Basket as sink,
  with the Supply and the Sell Basket as sources. The rule is three lines: the hub goes to
  the sink; Equipment goes to the sink in any non-`Hero` context; a source goes to the hub.
- **The Supply is source-only.** It can be emptied but is never a sink — the same role
  Equipment plays outside the `Hero` context. Its own shift-click stays a buy in every
  context, which is a Supply-local act rather than a table row.
- **The Healer is a Town Stop context of the Vendor's shape**, with its own containers. Its
  exact roster is not settled here: the glossary already allows a Town Stop with a Supply and
  no Sell Basket (ADR-0012), and the table admits that shape without a special case.
- **The Hero Panel becomes a real panel.** Today it is a bare scene object with no panel
  component and no toggle, and the only hotkey mechanism in the project is the Town Stop
  toggle's own serialized key. Making it an authored panel with a toggle is a prerequisite of
  everything above, and no existing ticket owns it — the superseded companion spec deferred
  it to a "separate ticket" that was never created, and #69 explicitly defers the input layer.
- **The exclusivity group over the left panels is deleted**, along with the manual
  clear-the-group calls on Send and Go Venture, which become context changes. It is the only
  such group in game code; the class and its tests stay in the submodule untouched.
- **The toggles' radio group stays**, answering only "which button is pressed". It resyncs
  from the context change event instead of from the group it no longer shares an authority
  with. This preserves #74's input/content split, with the context on the content side.
- **Phase reachability replaces the group's phase wiring.** Only `None` and `Hero` are
  reachable during a Run. A phase change that leaves the active context unreachable drops it
  to `None`. The existing non-interactable gating on the Town Stop toggles stays as the
  input-side expression of the same fact — it is no longer the authority, in the same way the
  radio group is no longer the visibility authority.
- **The Combat Panel stays phase-driven and ungrouped.** With the Town Stop contexts
  unreachable during a Run, nothing can share the left side with it, so it needs no group
  membership to stay exclusive.
- **No submodule changes** (ADR-0011). The panel and toggle base classes, the group classes
  and the tween core are all untouched; everything here is game code.
- **An ADR records the inversion**, because it reverses a rationale currently written into
  three places — #75's body, the Side Panel class doc and the `CONTEXT.md` entry. A panel
  announcing its own context was right for one-panel-per-context, and became wrong the moment
  the Hero Panel joined all of them.

## Testing Decisions

A good test here exercises the rule, not the wiring: it asks "given this context, what is
visible and where does a Quick Move land", never "did this component call that one". Both
seams already exist and are engine-free, in `InventorySystem.Containers` — no new seam is
introduced, and the two existing fixtures are reshaped rather than replaced.

- **The context state.** Prior art is the existing side-panel state fixture, which covers the
  default state, the set/clear idempotency contract and the change-event contract against the
  real object the provider forwards to, not a copy of it. It grows: the derived panel set per
  context (the Hero Panel present in every non-`None` context, absent from `None`); closing
  any context giving `None` rather than falling back; a handover publishing one change rather
  than two; phase reachability dropping an unreachable context.
- **The routing table.** Prior art is the existing quick-move fixture, which builds a roster
  of distinct containers and asserts one intent per (context, source) pair by reference
  identity. It is rewritten against the sink/sources table: every context's hub-to-sink,
  Equipment-to-sink and source-to-hub rows, the Supply's buy in every context, and `None`
  moving nothing. The Vendor and Stash rows already covered must come out identical in
  behaviour.
- **Two seams, not one.** Folding routing into the state object would drag the container
  roster into it and cost it the rosterless, engine-free shape that makes it directly
  testable. Two existing seams beat one new merged one.
- **Not seamed:** the panel subscription, the toggle request, the minimap rewiring and the
  toggle pressed-visual resync. These are lifecycle and wiring with no decision logic, and
  #74's testing decisions gave the same reasoning for not seaming the minimap controller.
- **The editor smoke pass** is the replay list, every row with a stated expectation: Send with
  a Town Stop open; Send with only the Hero Panel open; Recall; Death; Go Venture; a
  Stash-to-Vendor handover; a staged sale cancelling on Vendor close by every route; an
  in-flight purchase returning to the Supply; the Healer panel; the Hero Panel opened during a
  Run; the toggle pressed-visual after a phase-driven close; clicking an open toggle to close
  it; and each Quick Move row per context.
- **Green means** compiling clean through the unity-mcp bridge — `dotnet build` does not tell
  the truth here — and a human running the EditMode suite.

## Out of Scope

- **Hero-context shift-click** (Inventory to Equipment and back). It would duplicate what
  right-click already does, so the `Hero` row stays empty for now. If it lands later it is a
  row in the table, not a change to the mechanism.
- **Routing acquisition through one auto-equip entry point.** The Stash-to-Inventory row wants
  a Package to auto-equip into an empty slot, and that belongs to the existing
  acquisition-entry-point work, already built on its own branch. This epic depends on it
  rather than reimplementing it.
- **The centralised, rebindable input service.** The Hero Panel's toggle uses the same
  serialized-key mechanism the Town Stop toggles use today; migrating all of them is its own
  epic (#69).
- **Any change to a shared Utility submodule script** (ADR-0011).
- **The Healer's container roster and what it trades.** This epic makes the Healer a context
  of the Vendor's shape; what it stocks is a separate design question.
- **Sell Basket behaviour.** Staging, preview, confirm, cancel and the modal Supply block
  (ADR-0012) are unchanged. Only how the basket learns the Vendor went away is touched, and it
  already learns it from the context event.
- **The persistence tier the shared Stash needs** (#68).
- **Whether the Side Panel toggles should become plain buttons.**

## Further Notes

The route to this design was audited before it was written, and the audit found four stranded
fixes, one orphaned resolution, two coverage gaps and one swap residue. Four of those are
carried by this spec rather than left for the ticket slice to rediscover: the Healer's context
membership, the Hero Panel prerequisite, the obsolete companion spec, and which layer owns
phase reachability.

The rest is a merge decision rather than a design one. The base branch carries three closed
tickets whose mechanisms this rework deletes — the panel group's adoption, the inversion that
made a panel announce its own context, and that inversion's "handover still publishes None"
criterion — plus a phase gate whose authority moves. Either that branch stops short of the
context work and this epic absorbs it, or it merges and the revert is paid for afterwards.
That choice belongs to whoever sequences the epic, and it should be made before the first
ticket rather than discovered during it.

One open ticket is fully resolved by commits landed under three sibling numbers, and should be
closed as superseded rather than built.
