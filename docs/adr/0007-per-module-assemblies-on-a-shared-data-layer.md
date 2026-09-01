---
status: accepted, in progress
---

# Every module gets its own assembly, stacked on a shared `InventorySystem.Data` layer

About three quarters of the code still sits in the predefined `Assembly-CSharp`, which
no asmdef test assembly can reference — the reason `MutableFloat`, `Currency`,
`ProbabilityTable` and now the shared enums were each carved out one at a time. The
target is the shape the sister project (GlyphsHero / AutoBattler) already runs: a strict
dependency stack with nothing left in `Assembly-CSharp`.

The layers, bottom to top:

- **`Utility`** — the existing submodule.
- **`InventorySystem.Geometry`** / **`InventorySystem.Probability`** — leaf value cores,
  Unity-free, already extracted. Footprint/packing maths and the enum-agnostic roll
  table (ADR-0005).
- **`InventorySystem.Data`** — enums, **Item Definitions**, and the authored
  **Distribution** assets. The one layer that both hosts `ScriptableObject`s a designer
  edits and is reachable from a test; its engine reference stays on. Mirrors
  AutoBattler's `Data` deliberately — a Definition is a template a test needs *and* an
  asset a designer opens, so it belongs with the enums, not one layer up.
- **`InventorySystem.Items`** — `ItemInstance`, the **Roll**, the generator. Unity-free.
  One layer above `Data`, the same way AutoBattler keeps `ItemFactory` in `Container`
  rather than `Data`. Built in Phase 1 of the foundational rework.
- **`InventorySystem.Containers`** — the four containers, `Package`, the **Transaction**.
  Extracted by issue #4's spike (its committed fallback) and finished in Phase 2.
- **`InventorySystem.Runtime`** then **`InventorySystem.GUI`** — providers, character,
  the debug harness, then every display and drag surface. Last, because they depend on
  everything and hold the singletons the lower layers must stop calling.

Assembly names keep the `InventorySystem.` prefix where AutoBattler uses bare `Data` /
`Container`: the namespace root here is `ToolSmiths.InventorySystem`, so the prefixed
name reads the same as the namespace it contains.

## Consequences

The blocker is not folder layout — it is the twelve `Provider.Instance` calls that pin
`AbstractDimensionalContainer`, `CharacterEquipment`, `CharacterInventory` and
`AbstractItem`'s constructors to `Assembly-CSharp`. The graph therefore arrives one seam
at a time, each landing on green: issue #4 extracts `Containers` and breaks the provider
coupling with injected interfaces or events; Phase 1 does the same for the Roll. A
big-bang "asmdef everything" pass is rejected — it surfaces every circular reference at
once with no tested intermediate state, which is the failure mode the rework spec is
built to avoid.

`InventorySystem.Statistics` is **not** a separate assembly. `MutableFloat`,
`StatModifier` and `CharacterStatModifier` already live in `InventorySystem.Data`;
AutoBattler splits `Statistics` out, but this project does not copy that split until a
concrete reason appears.

The three test assemblies (`.Data.Tests`, `.Geometry.Tests`, `.Probability.Tests`) stay
separate, and a fourth joins per phase (`.Items.Tests`, then `.Containers.Tests`).
Collapsing them into one `InventorySystem.Tests.EditMode`, as AutoBattler has, is a
later call — once the module set stops moving.

Until a module is extracted, its behaviour cannot be tested without a scene. That is the
standing justification for issue #4's spike and for every "reachable from a test" line
in `dev/specs/2026-08-31-foundational-rework-design.md`.
