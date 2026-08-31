# Rebuild the probability distribution system behind a testable seam

Date: 2026-08-30
Status: Ready for implementation
Base: cut from `feature/currency-redesign`.

## Problem Statement

**Magic find is inverted and self-destructing.** `IncreasedItemRarity` is meant to
make good loot more common. In `AbstractProbabilityDistribution.GetRandomEnumerator`
it instead shifts an index into the cumulative-probability walk, which re-labels each
CDF band as the band N slots rarer while keeping the band widths. Because index 0 is
`NoDrop`, magic find hands the no-drop bucket the probability mass that belonged to
`Unique`. Measured against the live weights in `Item Rarity Distribution.asset`:

| Magic find | What actually happens |
| --- | --- |
| 0–99 | nothing at all — `j <= i + 0.5` is identical to `j <= i` |
| 100 | 6.7% NoDrop, 0% Common |
| 300 | **46.7% NoDrop**, 53.3% Unique |
| 400+ | **100% NoDrop — nothing ever drops** |
| 500+ | `IndexOutOfRangeException` |

The existing `// TODO: implement falloff => ATM 300% will always drop legendaries`
at `ItemProvider.GetRandomRarity` diagnoses this backwards: at 300% roughly half of
all kills drop nothing.

**Nothing in the system is testable.** `Assets/Scripts/Tests/EditMode/Statistics/`
covers `MutableFloat` and `Currency`, but the distributions cannot be reached from a
test assembly at all: `InventorySystem.Data.asmdef` sits at `Data/Statistics/` and
covers only those four files, so `Data/Distributions`, `Data/Enums`, and the
`[ReadOnly]` attribute all live in `Assembly-CSharp`, which an asmdef test assembly
cannot reference. The module carrying the subtlest maths in the codebase has zero
coverage and no seam through which to get any.

**Five further defects, all latent today because every asset ships `failQuantity: 0`:**

1. `Probabilities` is a property that allocates two arrays, runs a LINQ `OrderBy`, and
   writes to a serialized field — and `GetRandomEnumerator` calls it in both the outer
   loop condition and the inner loop body, so a 5-value enum costs ~20 invocations, 20
   sorts and 20 serialized writes **per item rolled**.
2. The `(uint)` cast on the derived fail weight truncates. Asking for a 10% no-drop on
   a table whose success weights total 6 yields `0.667 → 0`, i.e. 0%. Small tables can
   express only 0%, 14.3%, 25%, 33%… A 5% no-drop chance is not representable.
3. `OnValidate` is the only writer of the derived fail weight into `quantities[0]`, and
   `OnValidate` does not run in player builds. Builds ship whatever the editor last
   baked from the *example* player-count preview fields. `AlliesWithinRange()` also
   returns `0` whenever `Application.isPlaying`, so play mode and edit mode disagree.
4. `quantities[0]` is assumed to be the fail bucket positionally. This holds only
   because every enum starts at 0. `//Crafted = -1` sits commented out in `ItemRarity.cs`;
   uncommenting it silently makes `Crafted` the fail bucket and `NoDrop` a success.
5. Editing an enum destroys designer data — a changed member count discards every tuned
   weight, and a reorder silently remaps weights to the wrong members.

## Solution

Extract the maths into a pure, generic, Unity-free type in its own assembly, and reduce
the ScriptableObjects to thin serialization adapters over it. Sampling takes the random
roll as a parameter rather than calling `Random` internally, so every behaviour above
becomes a plain unit test with no ScriptableObject instantiation and no statistical
flakiness.

Magic find is reimplemented as **Diablo II's cascade**: quality is checked rarest-first,
each rung carrying its own diminishing-returns factor, first hit wins. This was chosen
over two alternatives (a linear weight multiplier and a saturating weight multiplier)
because it introduces no invented tuning constants — the factors are the ones Diablo II
itself ships — and because its crossover behaviour is the desired one: Common dominates
at 0 magic find, Magic takes over in the low hundreds, Rare takes over in the mid
hundreds.

## User Stories

1. As a player, I want magic find to increase my chance of good loot, so that the stat
   does what its name says.
2. As a player, I want magic find never to reduce the number of items that drop, so that
   investing in the stat is never a trap.
3. As a player with no magic find, I want the drop table to match exactly what the
   designer authored, so that the baseline game is the intended one.
4. As a player stacking magic find, I want white items to give way to blue, then blue to
   yellow, so that gear progression visibly changes what the world drops.
5. As a player, I want extreme magic find to hit diminishing returns on the rarest tier,
   so that Uniques stay meaningful rather than becoming the default drop.
6. As a player, I want the game never to crash or silently drop nothing because of a
   stat value, so that high-investment builds remain playable.
7. As a loot designer, I want to author each outcome's likelihood as an integer weight in
   the inspector, so that tuning stays as simple as it is today.
8. As a loot designer, I want to express a small no-drop chance such as 5%, so that fail
   rates are a real design lever rather than a coarse quantized one.
9. As a loot designer, I want the probabilities the inspector shows me to be the
   probabilities the built game uses, so that previewing is not lying to me.
10. As a loot designer, I want to add, remove, reorder, or uncomment an enum member
    without losing every weight I have tuned, so that iterating on the rarity ladder is
    not destructive.
11. As a loot designer, I want the fail bucket identified explicitly rather than by
    position, so that adding a negative enum member does not silently invert the table.
12. As a loot designer, I want to see a sample of rolled outcomes in the inspector, so
    that I can sanity-check a table at a glance.
13. As a loot designer, I want opening or inspecting a distribution asset not to dirty
    it, so that version control stays free of churn.
14. As a loot designer, I want the magic-find crossover points documented, so that I know
    which numbers to move when the ladder feels wrong.
15. As a developer, I want the probability maths reachable from an EditMode test
    assembly, so that I can cover it at all.
16. As a developer, I want sampling to take the roll as a parameter, so that boundary
    behaviour is testable without randomness and distribution shape is testable
    reproducibly from a seeded generator.
17. As a developer, I want rolling an item to allocate nothing per call, so that loot
    generation does not churn the GC on every kill.
18. As a developer, I want the generic table to know nothing about magic find, allies, or
    rarity, so that currency and armour tables are not carrying rarity-specific
    parameters.
19. As a developer, I want a single seam for the whole subsystem, so that swapping the
    magic-find curve later is a body change behind one interface.
20. As a developer, I want `GetRandomEnumerator` renamed to something that says what it
    returns, so that the API stops implying `IEnumerator`.
21. As a maintainer, I want the fail-weight scaling computed at read time rather than
    baked by an editor callback, so that editor and build behave identically.
22. As a maintainer, I want a roll that falls past the last threshold to return the last
    outcome rather than `default(T)`, so that float rounding cannot produce a phantom
    no-drop.

## Implementation Decisions

### The seam

One new assembly, `InventorySystem.Probability`, holding a pure generic
`ProbabilityTable<T> where T : System.Enum`. It has **no Unity dependencies** — being
generic over the enum, it needs neither the game's enums nor the `[ReadOnly]` attribute
(which lives in `Assembly-CSharp` and is a reason the ScriptableObjects must stay there).
`Assembly-CSharp` already auto-references it; the EditMode test assembly adds an explicit
reference and declares its own test enums.

`AbstractProbabilityDistribution<T>` stays exactly where it is and becomes a thin
adapter: serialized weights in, `ProbabilityTable` built, everything delegated.

### The table

- Construction takes the weights, the fail weight, and the fail exponent. Probabilities
  are computed once at construction and cached in a non-serialized array; the table is
  rebuilt when the serialized data changes, never per roll.
- **All internal maths is `float`.** The `uint` round-trip on the derived fail weight is
  removed, which is what makes small fail probabilities representable.
- The fail-weight scaling keeps the existing algebra, which is correct and worth
  preserving: given a designer fail probability `p = failWeight / (failWeight + S)` and
  exponent `e`, the effective fail probability is exactly `p^e`. It is evaluated at read
  time, not written into `quantities[0]`.
- `GetFailExponent()` returns `float`, not `int` — the current `Mathf.FloorToInt`
  discards the 0.5-per-distant-player term entirely for odd player counts.
- **The fail bucket is identified explicitly**, as `default(T)`, never as index 0.
- **No sorting.** Probabilities stay in enum-declaration order. Sorting bought nothing
  and broke the `successProbability` calculation whenever the fail bucket was not the
  rarest entry.
- Sampling is `Sample(float roll)` — a single-pass accumulator over the CDF, allocating
  nothing. A roll past the final threshold returns the last outcome explicitly.
- The returned probability view must not expose the internal array for mutation.

### Magic find — the Diablo II cascade

Rarity-specific, so it lives beside the table rather than inside it; the generic table
gains no magic-find parameter. It is a pure transform from (base probabilities, magic
find) to a new probability vector, which is then sampled through the ordinary path — so
there is exactly one sampling code path for every distribution in the game.

Effective magic find per tier uses Diablo II's formula, `eff = mf × F / (mf + F)`, with
Diablo II's own factors:

| Tier | Factor | Behaviour |
| --- | --- | --- |
| Unique | 250 | saturates hardest — the rarest tier is protected |
| Rare | 600 | saturates gently |
| Magic | — | **linear, no diminishing returns** (as in Diablo II) |
| Common | — | no bonus; it is the cascade's remainder |

The cascade walks rarest-first. Conditional rung probabilities are derived from the base
probability vector rather than authored, so **magic find of 0 reproduces the authored
table exactly**:

```
cond[i] = p[i] / (1 - sum of p[j] for every j rarer than i)
```

At the live weights (Common 160, Magic 80, Rare 40, Unique 20) that gives
`condUnique = 1/15`, `condRare = 1/7`, `condMagic = 1/3`. Each rung is multiplied by
`1 + eff/100`, clamped to 1, then:

```
P(Unique) = pU
P(Rare)   = (1 - pU) × pR
P(Magic)  = (1 - pU) × (1 - pR) × pM
P(Common) = the remainder
```

**The fail bucket is excluded from the cascade entirely.** The cascade operates on the
success mass only and is scaled by `1 - P(NoDrop)`, so `P(NoDrop)` is invariant under
magic find by construction — the property the current implementation violates.

Behaviour at the live weights, for the record and as the regression target:

| Magic find | Common | Magic | Rare | Unique |
| --- | --- | --- | --- | --- |
| 0 | 53.3% | 26.7% | 13.3% | 6.7% |
| 100 | 21.7% | 43.4% | 23.5% | 11.4% |
| 200 | 0.0% | 55.2% | 30.7% | 14.1% |
| 400 | 0.0% | 42.7% | 40.4% | 16.9% |
| 800 | 0.0% | 29.6% | 51.0% | 19.4% |
| limit | 0.0% | 0.0% | 76.7% | 23.3% |

### Serialization and the editor

- `probabilities` and `exampleResults` stop being serialized. They are derived values
  whose only effect today is to dirty the asset on every inspector interaction and
  produce version-control churn. The inspector renders them without persisting them.
- `OnValidate` stops writing to `quantities[0]` and stops consuming ten
  `UnityEngine.Random` rolls from the shared global stream.
- **Enum migration keys on the enum value, not the array index.** Weights survive adding,
  removing, reordering, and uncommenting members; only genuinely removed members lose
  their weight.
- `Enum.GetValues` is called once and cached in a `static readonly` field rather than
  allocating a fresh array inside a loop body.

### Naming

`GetRandomEnumerator` → `Roll`. `AllySensitiveFailQuantity` → `EffectiveFailWeight`
(the generic base should not know what an ally is).

## Testing Decisions

**What makes a good test here.** Assert on external behaviour — the probability vector a
set of weights produces, and the outcome a given roll maps to. Do not assert on internal
representation: not the cached array's identity, not ordering, not how many times the
table was rebuilt. Prior art is `MutableFloatTests.cs`, which locks in modifier
application order and precedence through the public surface only, and `CurrencyTests.cs`,
which pins `TryGetPayment` behaviour through its out-parameters — both `[TestFixture]`
`sealed` classes with one behaviour per `[Test]` and a small local helper to build inputs
readably.

Everything is tested against `ProbabilityTable<T>` and the cascade transform in
`InventorySystem.Probability`, using test-local enums. No ScriptableObject is
instantiated in any test. New file
`Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs` with its own asmdef
referencing the new assembly, mirroring the existing `Statistics` test folder layout.

Coverage:

- **Probabilities.** Weights normalize to a vector summing to 1. A zero-weight member
  gets probability 0. An all-zero table does not divide by zero or produce NaN.
- **Boundaries.** `Sample` at exact CDF boundaries returns the expected outcome; a roll
  of 0 returns the first non-zero-weight outcome; a roll of 1 returns the last; a roll
  past the final threshold returns the last outcome, never `default(T)`.
- **Distribution shape.** Driven by a seeded `System.Random` over a large sample, observed
  frequencies match the authored weights within tolerance — reproducible, not flaky.
- **Fail weight.** A designer fail probability `p` with exponent `e` yields an effective
  fail probability of `p^e`. Small fail probabilities such as 5% are representable on a
  small table — the regression test for the `uint` truncation bug.
- **Magic find, as invariants rather than magic numbers:**
  - magic find of 0 reproduces the authored table exactly;
  - `P(NoDrop)` is unchanged at every magic-find value, including on a table with a
    non-zero fail weight — the headline regression test;
  - `P(Unique)` is monotonically non-decreasing in magic find;
  - `P(Common)` is monotonically non-increasing;
  - the vector sums to 1 across the whole sweep and every entry stays within `[0, 1]`;
  - no magic-find value throws.
- **Enum migration.** Weights survive a reorder and an insertion, keyed by value.
- **Landmarks.** The crossover points below are pinned as tests so that a retune is a
  deliberate, visible change rather than an accident.

## Out of Scope

- **Balancing the rarity ladder.** The factors and base weights ship as specified. Retuning
  them is a designer pass, not this change.
- **Granting magic find from items.** `AbstractItem.cs:99` returns `0f` for
  `IncreasedItemRarity`; nothing in the game grants the stat yet. Wiring it to gear is
  separate work — see Further Notes.
- **The ally-scaling feature itself.** `AlliesWithinRange()` is still a `// TODO` returning
  0 at runtime and there is no multiplayer. This change makes the mechanism correct and
  build-safe; it does not implement real player detection.
- **A `Set` rarity tier.** `//Set = 25` stays commented out. Diablo II's factor of 500
  sits between Rare and Unique if it is ever introduced.
- **Per-equipment-type distributions** for uniques (the two `// TODO: individual
  probabilityDistribution for each equipment type` sites in `ItemProvider`).
- **`ItemTypeData.StatRange`'s `AnimationCurve` roll weighting.** A separate, unrelated
  randomness path.
- **`IncreasedItemQuantity`** and its `// TODO: requires a better formula`.
- Moving `Data/Enums`, `Data/Items`, or `Data/Structs` into asmdef assemblies. The pure
  table is generic, so none of that is needed.

## Further Notes

### Tuning landmarks

These fall out of the base weights and the factors; they are consequences, not inputs,
and they are what to look at when the ladder feels wrong:

| Landmark | Magic find |
| --- | --- |
| Magic overtakes Common | 50% |
| Common reaches 0%, Magic peaks at 55.2% | 200% |
| Rare overtakes Magic | 429% |
| Unique overtakes Magic | 1364% |

Common reaching exactly 0% at 200% magic find is a direct consequence of Diablo II
giving Magic quality no diminishing returns: the Magic rung saturates when
`(80/240) × (1 + mf/100) = 1`. This is faithful to Diablo II, where a high-magic-find
character stops seeing white drops. Note the tension with `ItemRarity.cs:7`, which
describes Common as *"highest base stats => good base for crafting"* — if crafting
becomes a real path, Common's supply disappears at a magic-find level a mid-game
character can reach. Give Magic a large finite factor if that turns out to matter.

### Open: this game's magic-find range is unknown

**Nothing here is calibrated against real gear.** No item grants `IncreasedItemRarity`
today, so neither the per-item magic-find values nor the maximum a fully geared character
could reach in *this* game are known. The reference frame used while choosing this curve
was Diablo II's, where a fully geared dual-wielding Barbarian reaches roughly **1167%** and
a class carrying a shield roughly **1038%**, with most farming builds sitting at 200–400%.

**Revisit this when item balancing starts.** Specifically:

- decide the magic-find budget per gear slot and per affix tier;
- compute the achievable maximum for this game's slot count and affix ranges;
- check that maximum against the landmark table above — if it lands far below ~400%, the
  Rare-overtakes-Magic crossover is unreachable and the factors want lowering; if it lands
  far above ~1200%, the Rare share is heading toward its 76.7% limit and the ladder wants
  a Set tier or a finite Magic factor;
- re-pin the landmark tests to whatever the retune produces.

### Why the cascade rather than a weight multiplier

Two alternatives were plotted against the live weights before this was chosen: a linear
per-tier weight multiplier, and a saturating one of the form `1 + max × mf/(mf + K)`. Both
work and both hold the NoDrop invariant, but both require inventing per-tier constants
with no data to balance them against. The cascade uses Diablo II's published factors and
produces the crossover behaviour described above. All three are the same seam — a pure
function from base weights plus magic find to a distribution — so switching later is a
body change behind one interface, with no test or interface churn.

Charts comparing all three: <https://claude.ai/code/artifact/4e4a6f9f-4292-498b-98cc-d799afe964d9>
