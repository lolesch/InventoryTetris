## Review of epic #81 (six tickets closed): `/drift-review` + `/code-review`

Reviewed at `242e4d2` (#86). Tests treated as green per instruction — they were **not** run
in this pass (Editor held `Temp/UnityLockfile`; the shadow-project and unity-mcp routes both
became unavailable mid-run). Not a claim of green, just a stated baseline for reading this.

Two findings below are **retractions of my own** from the first pass — recorded rather than
quietly dropped, because the reasoning error is the same one twice and is worth naming.

---

### Drift: 1 finding

**Fact the tests ran against:** the swap is panel-announces-its-own-context (#75,
`cab1c78`) → entry-points-request/panels-derive (#84 `ed18bef`, #85 `fbf20d6`), introduced
by `fbf20d6`. #75 is a clean ancestor of #82, so the sequence is expand → migrate →
contract with no stranded pre-swap work.

| Mechanism | Vs. | Drift | Recommendation |
|---|---|---|---|
| `AbstractSlotDisplay.QuickMove` (`:376`) and its only callee `QuickMoveToContainer` (`:304`) — zero callers repo-wide; `TryQuickMove` (`:109`, added by #67) inlines its own copy of the same transaction at `:148-160` | #67 — `8dd3f6a` added `TryQuickMove` *beside* #30's `QuickMove` rather than replacing it | swap residue | **done** — both deleted; see below |

**Cleanup applied.** Both methods removed. `OnPointerClick:89` already reached only
`TryQuickMove`, so the surviving `MoveToContainer` arm is the one that runs. Nothing else in
the repo referenced either method, and `BasketSlotDisplay`'s note at `:93` already names
`TryQuickMove`, so no doc comment went stale.

`Cleared:` #67 (its hoist *is* the single dispatch site), #82 (Hero Panel toggle + panel are
the prefactor, nothing replaces them), #83 (*is* the expand half), #84 (six entry points
still request the context), #85 (*is* the swap), #86 (*is* the routing table), #72/#73/#74/#75
(all superseded *by* the swap's contract step, not stranded before it).

Coverage gap, not drift, but it gates this epic: **#87 is still open**, and #81's own ticket
table lists it as a child. The six closed tickets are necessary but not sufficient.

**Retracted (mine, not findings):**

1. ~~The `PanelGroup` at `Example.unity:5545` is a resurrection of the group #85 deleted.~~
   **Wrong.** That group is not over the left panels. Its *only* member is
   `BehaviourSlidersPanel` (the Combat Panel, `:10725`, a child of the InFields face) —
   `{fileID: 347723239}` appears exactly once in the scene, and the four `SidePanel`s all
   have `<RadioGroup> = 0`. It is the deliberate InFields→Combat fade path `RunPhasePanel`'s
   doc describes, and it is the same component on the same GameObject as before — #85
   removed a *different* member from it, not the group. I read `LeftSidePanels` as carrying
   it because of hunk ordering and never checked membership.
2. ~~#72's `IsClearable: 1` is stranded, because `SidePanel` only calls `ToggleState`.~~
   **Wrong, same error.** True of `SidePanel`; false of `BehaviourSlidersPanel`, which is that
   group's member and *does* go through the group-aware `FadeIn`/`FadeOut`. Not stranded.
   Residual only: `IsClearable` is unreachable for a group whose single member is faded *in*
   by the cascade (and `FieldFacePanel.BeforeDisappear` calls bare `combatPanel.FadeOut()`,
   no group). `IsClearable = 1` is therefore inert but harmless — no action.

---

### Code review: `da31611...242e4d2`

**Standards** (docs/agents/coding-conventions.md + Fowler baseline) — 2 findings:

- **Duplicated Code** (hard, same object as the drift finding): `AbstractSlotDisplay.cs` had
  two implementations of one dispatch — the dead `QuickMove` at `:376` and the live
  `MoveToContainer` arm at `:148-160`. #67's own doc comment at `:96-103` names the bug it
  was closing as "four copies with growing odds one is wrong" — the hoist left a fifth.
  **Resolved by the deletion above.**
- **Public API needs an interface** (judgement call): `SidePanelToggle.panel` is a concrete
  `SidePanel` whose `RequestContext` is the toggle→panel contract. Precedent exists
  (`IItemReceiver`, `ICursorSink`), but here the concrete field type *is* the authoring
  constraint (a toggle wired to anything else can't be authored), so the convention's own
  rationale is partly satisfied by the type. Not asking for a change.
- Clean on: seal-by-default, auto-properties over backing fields, lazy `GetComponent`
  caching, and the `#if UNITY_EDITOR` using-guard rule — the two `OnValidate`s at
  `SidePanel.cs:147` / `SidePanelToggle.cs:205` are both properly guarded.

**Spec** (dev/specs/2026-09-20-inventory-context-design.md + #82–#86) — 2 findings:

- **`QuickMoveResolver.cs:70` — the Equipment row resolves through the wrong branch.**
  **Not reproducible - the finding did not match the reviewed commit.** `242e4d2` itself
  already has `Route` testing `source == hub || source == equipment` before the sources
  tuple, so the Equipment rule is live in every context with an arm, and
  `StashContext_Equipment_SendsTheItemToTheStash` / `VendorContext_Equipment_SendsTheItemToTheSellBasket`
  cover it. The description below (an `equipment` test after the hub early-return in a
  per-arm branch) does not describe that code; kept as written, not acted on.
  Spec: "Equipment goes to the sink in any non-`Hero` context" — its own stated rule, distinct
  from the sink arm. In the `Stash` arm the `source == equipment` test sits *after* the
  `source == hub` early-return, so Equipment resolves to `MoveTo(stash)` supplied by the
  arm's own `(stash, …)` sources tuple, never by the Equipment rule. `Vendor` is
  arm-consistent (Equipment → `SellBasket`, same answer either way). **No behavioural
  difference exists** — both paths yield the same sink — so this is a mechanism-vs-stated-rule
  divergence, not a bug. Worth a row because the spec's whole point is that the rule is
  readable; here the Equipment rule is dead code for every context that currently has an arm.
- **Unstated row in #86's table.** **Resolved - the spec now states the row** (the
  routing-table bullet in `2026-09-20-inventory-context-design.md`). The claim below that the
  assertion is unfalsifiable is too strong: `VendorContext_Stash_DoesNothing` fails under a
  refactor that sends every non-hub, non-Equipment source to the hub, so it is kept as is.
  Original finding: The table lists `Vendor` sources as *Supply, Sell Basket*.
  `QuickMoveResolverTests.VendorContext_Stash_DoesNothing` additionally asserts the Stash
  moves nothing under `Vendor`, which the table does not state. It passes trivially (the
  Stash is simply in no tuple), so the assertion is unfalsifiable as written — it would keep
  passing under any future refactor that happened to leave it unlisted.

Not findings: #85's "no Utility submodule script is changed" holds — no submodule *script*
differs from the fixed point, though the pointer moved 7× (all bumps, no ADR-0011
violation). The failure-path cleanups (#86's `PickUpTransaction`, #85's
`SidePanelToggle` resubscribe) are reviewed and sound.

---

### On closing #81

Recommend **not closing yet**, on one ground only: **#87 is open.** It is a child in this
epic's own ticket table, its docs work landed in #100 (ADR 0013, the `CONTEXT.md` glossary
moves) but the issue itself is still open, and #81's "What to build" promises the glossary moves with the mechanism. The six ticket
acceptance criteria are otherwise met.

`docs/agents/issue-tracker.md` previously described #81 as "Done as a goal" and advised
closing it. **Corrected** in this pass: the #81 row and the recommended-order step now name
#87 as the gate and record the two retractions, so the next scan does not re-derive them.
