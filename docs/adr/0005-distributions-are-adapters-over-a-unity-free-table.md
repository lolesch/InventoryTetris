# Probability distributions are thin adapters over a Unity-free table

`AbstractProbabilityDistribution` is a `ScriptableObject` and looks like it should own
sampling, but it delegates to `ProbabilityTable<T>` in `InventorySystem.Probability`,
which has no Unity dependency and is unit-tested directly. The ScriptableObject's job is
authoring and serialization; the maths lives where a test can reach it without a scene.

The generic table is deliberately **enum-agnostic** — it takes no rarity or currency
type — which is why magic find lives beside it rather than inside it. That kept the
probability rebuild from having to move the shared enums out of `Assembly-CSharp`; the
item model cannot avoid that move and will make it later.

## Consequences

All distribution assets were reserialized onto the adapter layout, so the change is not
reversible by editing code alone. This is the same shape as `MutableFloat` and `Currency`
and is the template the codebase is converging on: a pure, tested core with a thin Unity
adapter over it.
