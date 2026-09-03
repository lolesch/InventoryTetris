# Probability Distribution Rebuild — Implementation Plan

> **For agentic workers:** execute this plan inline in the current session, task-by-task, with a review checkpoint after each task — never dispatch subagents (project rule). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the drop-table maths into a pure, generic, Unity-free `InventorySystem.Probability` assembly, reduce the ScriptableObjects to thin serialization adapters over it, and reimplement magic find as Diablo II's rarest-first cascade so `IncreasedItemRarity` makes good loot *more* common instead of shifting probability mass into the no-drop bucket.

**Architecture:** One new assembly, `InventorySystem.Probability`, holds `ProbabilityTable<T> where T : System.Enum` (weights → a cached probability vector; `Sample(float roll)` takes the roll as a parameter), `MagicFindCascade` (a pure `(base vector, magic find) → new vector` transform), and `WeightMigration` (remap authored weights by enum value). `AbstractProbabilityDistribution<T>` stays where it is, gains a non-generic base for the custom editor, and becomes an adapter: serialized weights in, `ProbabilityTable` built once, everything delegated. `ItemRarityDistribution` binds the cascade to `ItemRarity` with Diablo II's factors. `ItemProvider` renames `GetRandomEnumerator` → `Roll` at its call sites. The magic-find cascade is sampled through the *same* `ProbabilityTable` sampler, so there is exactly one sampling path in the game.

**Tech Stack:** Unity 6000.3.9f1, C# 9 (Roslyn), Unity Test Framework (NUnit) EditMode tests, `.asmdef` assemblies, unity-mcp bridge for compile verification.

**Design:** `dev/specs/2026-08-30-probability-distribution-rebuild-design.md`. Read it first — it carries the problem statement, the measured failure table, the cascade derivation, and the regression targets this plan pins as tests.

## Global Constraints

- **Base branch:** `feature/probability-distribution-rebuild`, currently at the spec commit (`eb02d40`), cut from `feature/currency-redesign` (`7eee33b`). Continue on this branch. Do **not** rebase onto `main`.
- **The `InventorySystem.Probability` assembly has zero Unity dependencies.** Its `.asmdef` sets `"noEngineReferences": true` and `"references": []`. No `using UnityEngine;` in any file under it — use `System.MathF` / `System.Math`, not `Mathf`. If a file there needs Unity, the design is wrong.
- **All internal probability maths is `float`.** No `uint` round-trip on any derived weight — that truncation is spec defect #2 and making 5% no-drop representable is the point.
- **The fail bucket is `default(T)`, identified by value, never by array index 0.** Uncommenting `//Crafted = -1` in `ItemRarity.cs` must not change which bucket is the fail bucket.
- **Probabilities stay in enum-declaration order. No sorting anywhere.**
- **Magic find of 0 must reproduce the authored table bit-for-bit.** This is a test (Task 5) and an invariant of the cascade, not an aspiration.
- **`P(NoDrop)` is invariant under magic find.** The cascade operates on the success mass only. This is the headline regression test (Task 5).
- **Do not reorder or uncomment `ItemRarity` members.** `NoDrop = 0, Common = 5, Magic = 15, Rare = 20, Unique = 30` stays exactly as it is. Retuning the ladder is out of scope (design § Out of Scope).
- **Every new `.cs` / `.asmdef` needs a `.meta`** with a fresh GUID, or Unity generates one on import and the plan stops being reproducible headless. **If the Unity Editor is open and imports a file before you write its `.meta`, Unity already made one** — `git status` shows it; keep Unity's and skip the `printf`. New folders: same rule; let Unity's folder `.meta` stand if the Editor made it, otherwise apply the folder stanza below. If `powershell` is unavailable, swap the GUID subshell for `python -c "import uuid; print(uuid.uuid4().hex)"`. The canonical snippets, referenced as "**write the `.cs.meta`**" / "**write the `.asmdef.meta`**" from here on:

  ```bash
  # .cs.meta  (MonoImporter)
  metafor() { printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
    "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
  # .asmdef.meta  (AssemblyDefinitionImporter)
  asmdefmetafor() { printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
    "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
  # folder .meta  (DefaultImporter, folderAsset: yes)
  foldermetafor() { printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' \
    "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
  ```
  Shell state does not persist between tool calls — paste the relevant function definition into the same command that uses it.
- **Check `git status` before every commit.** Stage only the files the task names. `.idea/` is untracked and not part of this work — leave it.
- **Commits:** one per task (see per-task deviations noted inline where a task cannot land green on its own). Prefix `feat:` / `fix:` / `refactor:` / `perf:` / `test:` / `chore:` / `docs:`, body explains *why*. End every message with:
  ```
  Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
  ```

## Green

**Green** = the project compiles with zero `error CS` *and* EditMode tests pass (`InventorySystem.Data.Tests`, `InventorySystem.Geometry.Tests`, and the new `InventorySystem.Probability.Tests`). See the memory `unity-mcp-compile-verification` for the full mechanics.

**If the Unity Editor is open** (check: `ls Temp/UnityLockfile`) — compile-check via the unity-mcp bridge:
- `Unity_ValidateScript` with `Uri: "Assets/…/File.cs"`, `Level: "standard"`, `IncludeDiagnostics: true` for a single changed file, **or** `Unity_RunCommand` with
  ```csharp
  AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
  global::UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
  ```
  then `Unity_GetConsoleLogs` (not `Unity_ReadConsole` — it has returned 0 entries for real errors here). Expected: no `error CS`. Do not fire `RequestScriptCompilation()` back to back — each costs a domain reload and the bridge drops for 20–40s.
- Tests: the bridge cannot run them. Ask the user to run **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**. Do not propose closing their editor.

**If the Editor is closed** — batch mode compiles *and* runs tests in one shot:
```bash
rm -f Temp/UnityLockfile
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode \
  -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode \
  -testResults "C:/Users/loles/AppData/Local/Temp/claude/pdr-results.xml" \
  -logFile "C:/Users/loles/AppData/Local/Temp/claude/pdr-log.txt"
```
Don't trust the exit code. Read `<test-run … passed= failed=>` from the XML and `grep -c "error CS"` the log. A compile failure produces **no** results XML.

**Negative control (once, before believing the final green):** insert `DELIBERATE_SENTINEL_ERROR` into a changed `.cs`, recompile, confirm it surfaces, remove it, recompile. A false green has happened in this project before.

**`dotnet build` is useless here** — stale `.csproj` files report phantom `CS2001`s and don't compile what you changed.

## What is and is not unit-testable

`InventorySystem.Probability` is a plain C# assembly with no Unity dependency, so **everything in it gets real red-green tests** from `InventorySystem.Probability.Tests` using test-local enums — no ScriptableObject is instantiated in any test. That is `ProbabilityTable<T>`, `MagicFindCascade`, and `WeightMigration` (Tasks 1–5).

`AbstractProbabilityDistribution<T>` and its subclasses are `ScriptableObject`s in `Assembly-CSharp`, which no `.asmdef` test assembly can reference. Tasks 6–9 are verified by compile + the in-editor / play check in Task 10. Do not invent a way to unit-test the adapter — its logic is a one-line delegation to the tested table.

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Assets/Scripts/InventorySystem/Probability/InventorySystem.Probability.asmdef` | Pure, Unity-free assembly def | 1 |
| `Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs` | Weights → cached probability vector; `Sample(float roll)` (instance + static); `ProbabilityOf` | 1–3 |
| `Assets/Scripts/InventorySystem/Probability/WeightMigration.cs` | Remap authored weights by enum value | 4 |
| `Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs` | Pure `(base vector, magic find) → new vector` transform | 5 |
| `Assets/Scripts/Tests/EditMode/Probability/InventorySystem.Probability.Tests.asmdef` | EditMode test assembly | 1 |
| `Assets/Scripts/Tests/EditMode/Probability/TestOutcomes.cs` | Test-local enums | 1 |
| `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs` | Probabilities, boundaries, shape, fail weight | 1–3 |
| `Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs` | Reorder / insert / remove keyed by value | 4 |
| `Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs` | Invariants + landmark pins | 5 |
| `Assets/Scripts/InventorySystem/Data/Distributions/AbstractProbabilityDistribution.cs` | Non-generic editor root (6); `<T>` adapter over the table (7) | 6, 7 |
| `Assets/Scripts/InventorySystem/Data/Distributions/ItemRarityDistribution.cs` | Float fail exponent; `Roll(float magicFind)` via the cascade | 7, 8 |
| `Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs` | Binds `MagicFindCascade` to `ItemRarity` + Diablo II factors | 8 |
| `Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs` | `GetRandomEnumerator` → `Roll` at call sites; rarity roll takes magic find | 7, 8 |
| `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ProbabilityDistributionEditor.cs` | Non-dirtying probability + sample-preview inspector | 9 |
| `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ItemRarityDistributionEditor.cs` | Magic-find slider preview + landmark HelpBox | 9 |
| `Assets/Scripts/InventorySystem/Data/Distributions/*.asset` (7 files) | Reserialize — drop dead `probabilities` / `successProbability` / `exampleResults` keys; `failQuantity` → `failWeight` | 10 |

---

## Task 1: `InventorySystem.Probability` assembly + `ProbabilityTable<T>` normalization

Stand up the pure assembly and its test assembly. `ProbabilityTable<T>` turns a weight vector into a probability vector that sums to 1, in enum-declaration order, computed once at construction. The fail bucket (`default(T)`) gets `failWeight / (failWeight + successSum)` — **no exponent yet** (Task 3 adds it), but the constructor takes the `failExponent` parameter from the start so its signature never churns.

**Files:**
- Create: `Assets/Scripts/InventorySystem/Probability/InventorySystem.Probability.asmdef` (+ `.meta`)
- Create: `Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/InventorySystem.Probability.Tests.asmdef` (+ `.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/TestOutcomes.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs` (+ `.cs.meta`)
- Folder metas for `Assets/Scripts/InventorySystem/Probability/` and `Assets/Scripts/Tests/EditMode/Probability/` (Unity-generated is fine if the Editor is open)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `ToolSmiths.InventorySystem.Probability.ProbabilityTable<T> where T : System.Enum`
    - `static IReadOnlyList<T> Outcomes` — enum members, declaration order, allocated once per closed type
    - `ProbabilityTable(IReadOnlyList<float> weights, float failWeight, float failExponent)` — `weights` parallel to `Outcomes`; the slot at the `default(T)` index is ignored (fail weight is the separate parameter)
    - `IReadOnlyList<float> Probabilities` — the vector, enum order, non-mutable
    - `float ProbabilityOf(T outcome)`
  - Tasks 2, 3 extend this file; Task 7 constructs it from the adapter.

- [ ] **Step 1: Write the pure assembly definition**

Create `Assets/Scripts/InventorySystem/Probability/InventorySystem.Probability.asmdef`:

```json
{
    "name": "InventorySystem.Probability",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

Write its `.asmdef.meta`:
```bash
asmdefmetafor() { printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
asmdefmetafor "Assets/Scripts/InventorySystem/Probability/InventorySystem.Probability.asmdef"
```

- [ ] **Step 2: Write the failing tests**

Create `Assets/Scripts/Tests/EditMode/Probability/TestOutcomes.cs`:

```csharp
namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>Test-local enums — the point of the pure assembly is that these are all it needs.</summary>
    internal enum Coin
    {
        None = 0,   // default(Coin) — the fail bucket
        Copper = 1,
        Silver = 2,
        Gold = 3,
    }

    /// <summary>Mirrors ItemRarity's shape: a zero fail member, then rarer-as-value-rises.</summary>
    internal enum Tier
    {
        Nothing = 0,
        White = 5,
        Blue = 15,
        Yellow = 20,
        Orange = 30,
    }

    /// <summary>No zero member — exercises "the enum has no default bucket".</summary>
    internal enum NoFail
    {
        A = 1,
        B = 2,
        C = 3,
    }
}
```

Create `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs`:

```csharp
using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks ProbabilityTable&lt;T&gt;: weights normalize to a vector summing to 1 in
    /// enum-declaration order; a zero-weight member gets probability 0; an all-zero
    /// table produces no NaN. Fail-weight scaling and sampling are in their own fixtures.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableTests
    {
        private const float Tol = 1e-5f;

        // weights parallel to Coin: [None, Copper, Silver, Gold]; the None slot is ignored.
        private static ProbabilityTable<Coin> Table(float copper, float silver, float gold, float failWeight = 0f, float exponent = 1f) =>
            new(new[] { 0f, copper, silver, gold }, failWeight, exponent);

        [Test]
        public void Weights_NormalizeToAVectorSummingToOne()
        {
            var table = Table(copper: 1f, silver: 2f, gold: 1f);

            var p = table.Probabilities;

            Assert.That(p.Count, Is.EqualTo(4));
            Assert.That(p[0] + p[1] + p[2] + p[3], Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Weights_MapToTheirShareInEnumOrder()
        {
            var table = Table(copper: 1f, silver: 2f, gold: 1f); // total 4

            Assert.That(table.ProbabilityOf(Coin.Copper), Is.EqualTo(0.25f).Within(Tol));
            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0.50f).Within(Tol));
            Assert.That(table.ProbabilityOf(Coin.Gold), Is.EqualTo(0.25f).Within(Tol));
        }

        [Test]
        public void ZeroWeightMember_GetsProbabilityZero()
        {
            var table = Table(copper: 3f, silver: 0f, gold: 1f);

            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0f));
        }

        [Test]
        public void NegativeWeight_IsTreatedAsZero()
        {
            var table = Table(copper: 3f, silver: -5f, gold: 1f);

            Assert.That(table.ProbabilityOf(Coin.Silver), Is.EqualTo(0f));
            Assert.That(table.ProbabilityOf(Coin.Copper) + table.ProbabilityOf(Coin.Gold), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void AllZeroTable_DoesNotDivideByZeroOrProduceNaN()
        {
            var table = Table(copper: 0f, silver: 0f, gold: 0f);

            foreach (var v in table.Probabilities)
                Assert.That(float.IsNaN(v), Is.False, "no entry is NaN");

            // no success mass: the fail bucket owns the whole vector
            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void EnumWithNoDefaultMember_IsAllSuccess_NoNaN()
        {
            var table = new ProbabilityTable<NoFail>(new[] { 1f, 1f, 2f }, failWeight: 10f, failExponent: 1f);

            Assert.That(table.ProbabilityOf(NoFail.C), Is.EqualTo(0.5f).Within(Tol));
            foreach (var v in table.Probabilities)
                Assert.That(float.IsNaN(v), Is.False);
        }

        [Test]
        public void WrongWeightCount_Throws()
        {
            Assert.That(() => new ProbabilityTable<Coin>(new[] { 1f, 2f }, 0f, 1f),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ProbabilitiesView_CannotMutateTheTable()
        {
            var table = Table(copper: 1f, silver: 1f, gold: 1f);

            Assert.That(table.Probabilities, Is.Not.AssignableTo<float[]>(),
                "the view must not be the backing array");
        }
    }
}
```

The test `.asmdef`, `Assets/Scripts/Tests/EditMode/Probability/InventorySystem.Probability.Tests.asmdef`:

```json
{
    "name": "InventorySystem.Probability.Tests",
    "rootNamespace": "",
    "references": [
        "InventorySystem.Probability",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Write the metas (see Global Constraints for the function bodies):
```bash
metafor() { printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
asmdefmetafor() { printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(powershell -Command "[guid]::NewGuid().ToString('N')" | tr -d '\r')" > "$1.meta"; }
asmdefmetafor "Assets/Scripts/Tests/EditMode/Probability/InventorySystem.Probability.Tests.asmdef"
metafor "Assets/Scripts/Tests/EditMode/Probability/TestOutcomes.cs"
metafor "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs"
```

- [ ] **Step 3: Run the tests to verify they fail**

Per **Green**. Expected: compile error — `ProbabilityTable` does not exist. A compile error blocks the run; that is the red.

- [ ] **Step 4: Write `ProbabilityTable<T>` (normalization only)**

Create `Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// The drop-table maths, pure and Unity-free. A weight vector in, a probability
    /// vector out — enum-declaration order, summing to 1, computed once at construction
    /// and cached. <see cref="Sample"/> takes the roll as a parameter (no Random inside),
    /// so every behaviour is a plain unit test.
    ///
    /// The fail bucket is <c>default(T)</c>, identified by value, never by index. Its
    /// probability is the designer's <c>failWeight / (failWeight + successSum)</c> raised
    /// to <c>failExponent</c> (Diablo II's ally scaling) — evaluated here, not baked by an
    /// editor callback, so editor and player build agree.
    /// </summary>
    public sealed class ProbabilityTable<T> where T : Enum
    {
        /// <summary>Enum members, declaration order. Allocated once for the whole closed generic type.</summary>
        public static readonly IReadOnlyList<T> Outcomes =
            Array.AsReadOnly((T[])Enum.GetValues(typeof(T)));

        /// <summary>Index of <c>default(T)</c> in <see cref="Outcomes"/>, or -1 when the enum has no zero member.</summary>
        private static readonly int FailIndex = IndexOfValue(default);

        private readonly float[] _probabilities;                 // parallel to Outcomes
        private readonly IReadOnlyList<float> _probabilitiesView; // non-mutable wrapper, cached

        public ProbabilityTable(IReadOnlyList<float> weights, float failWeight, float failExponent)
        {
            if (weights is null)
                throw new ArgumentNullException(nameof(weights));
            if (weights.Count != Outcomes.Count)
                throw new ArgumentException(
                    $"expected {Outcomes.Count} weights for {typeof(T).Name} in enum order, got {weights.Count}",
                    nameof(weights));

            _probabilities = Compute(weights, failWeight, failExponent);
            _probabilitiesView = Array.AsReadOnly(_probabilities);
        }

        /// <summary>The probability vector, enum-declaration order. Read-only — never the backing array.</summary>
        public IReadOnlyList<float> Probabilities => _probabilitiesView;

        public float ProbabilityOf(T outcome) => _probabilities[IndexOfValue(outcome)];

        private static float[] Compute(IReadOnlyList<float> weights, float failWeight, float failExponent)
        {
            var result = new float[weights.Count];

            var successSum = 0f;
            for (var i = 0; i < weights.Count; i++)
            {
                if (i == FailIndex)
                    continue;
                var w = weights[i] > 0f ? weights[i] : 0f;
                result[i] = w;               // raw success weight; normalized below
                successSum += w;
            }

            if (successSum <= 0f)
            {
                // No success mass — nothing can drop. Fail owns the vector, or the vector
                // is all-zero when the enum has no default member. Either way: no NaN.
                var flat = new float[weights.Count];
                if (FailIndex >= 0)
                    flat[FailIndex] = 1f;
                return flat;
            }

            var f = failWeight > 0f ? failWeight : 0f;
            var pFail = FailIndex >= 0 && f > 0f ? f / (f + successSum) : 0f; // Task 3 adds the exponent
            if (pFail < 0f) pFail = 0f;
            else if (pFail > 1f) pFail = 1f;

            var successScale = (1f - pFail) / successSum;
            for (var i = 0; i < result.Length; i++)
                result[i] *= successScale;   // the fail slot was 0, stays 0

            if (FailIndex >= 0)
                result[FailIndex] = pFail;

            return result;
        }

        private static int IndexOfValue(T outcome)
        {
            for (var i = 0; i < Outcomes.Count; i++)
                if (EqualityComparer<T>.Default.Equals(Outcomes[i], outcome))
                    return i;
            return -1;
        }
    }
}
```

> **Note on `failExponent`:** the parameter is accepted and ignored in this task. Task 3's test drives the `MathF.Pow(..., failExponent)` in. Do not delete the parameter to "clean up" — the signature is fixed.

Write its `.cs.meta`: `metafor "Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs"`

- [ ] **Step 5: Run the tests to verify they pass**

Per **Green**. Expected: all `ProbabilityTableTests` PASS; `InventorySystem.Data.Tests` and `InventorySystem.Geometry.Tests` unaffected; 0 `error CS`.

- [ ] **Step 6: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Probability" "Assets/Scripts/Tests/EditMode/Probability"
git commit -m "feat: add InventorySystem.Probability + ProbabilityTable normalization

A pure, Unity-free assembly for the drop-table maths. ProbabilityTable<T>
turns a weight vector into a cached probability vector in enum-declaration
order, fail bucket identified as default(T) by value. Fail-weight exponent
and sampling land in the next two commits.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 2: `ProbabilityTable<T>.Sample(float roll)`

The single-pass CDF walk. Instance form samples the table's own vector; a static form samples any vector (enum order) — the path the magic-find cascade takes in Task 8, so the game has exactly one sampler. A roll of 0 returns the first non-zero-probability outcome; a roll at or past the final threshold returns the last non-zero outcome — never a phantom `default(T)` from float rounding.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs`
- Modify: `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableTests.cs` (add a `SampleTests` fixture in the same file, or a sibling file — sibling is cleaner)
- Create: `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableSampleTests.cs` (+ `.cs.meta`)

**Interfaces:**
- Consumes: `ProbabilityTable<T>` (Task 1).
- Produces:
  - `T Sample(float roll)` — instance
  - `static T Sample(IReadOnlyList<float> probabilities, float roll)` — free-standing over any enum-order vector

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableSampleTests.cs`:

```csharp
using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks ProbabilityTable&lt;T&gt;.Sample: a single-pass CDF walk over a [0,1] roll.
    /// Exact boundaries resolve to the outcome that owns them; roll 0 skips zero-weight
    /// entries; a roll at or past the final threshold returns the last non-zero outcome,
    /// never default(T); observed frequencies over a seeded sample match the weights.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableSampleTests
    {
        // Tier: [Nothing, White, Blue, Yellow, Orange]
        private static ProbabilityTable<Tier> Table(float white, float blue, float yellow, float orange,
            float failWeight = 0f, float exponent = 1f) =>
            new(new[] { 0f, white, blue, yellow, orange }, failWeight, exponent);

        [Test]
        public void RollOfZero_ReturnsFirstNonZeroOutcome_NotTheZeroWeightFailBucket()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f); // P(Nothing) == 0

            Assert.That(table.Sample(0f), Is.EqualTo(Tier.White));
        }

        [Test]
        public void RollOfOne_ReturnsTheLastNonZeroOutcome()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(1f), Is.EqualTo(Tier.Orange));
        }

        [Test]
        public void RollPastTheFinalThreshold_ReturnsLastOutcome_NeverDefault()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(1.0001f), Is.EqualTo(Tier.Orange));
            Assert.That(table.Sample(42f), Is.EqualTo(Tier.Orange));
        }

        [Test]
        public void RollPastFinalThreshold_SkipsATrailingZeroWeightMember()
        {
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 0f); // Orange has weight 0

            Assert.That(table.Sample(1f), Is.EqualTo(Tier.Yellow));
            Assert.That(table.Sample(2f), Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void ExactCdfBoundary_ResolvesToTheOutcomeThatOwnsIt()
        {
            // white .25, blue .25, yellow .25, orange .25  →  CDF 0.25 / 0.50 / 0.75 / 1.0
            var table = Table(white: 1f, blue: 1f, yellow: 1f, orange: 1f);

            Assert.That(table.Sample(0.25f), Is.EqualTo(Tier.White),  "0.25 is still inside White's band");
            Assert.That(table.Sample(0.50f), Is.EqualTo(Tier.Blue));
            Assert.That(table.Sample(0.75f), Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void StaticSample_OverAnArbitraryVector_WalksTheSameWay()
        {
            var vector = new[] { 0f, 0.5f, 0f, 0.5f, 0f };

            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0f),   Is.EqualTo(Tier.White));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0.5f), Is.EqualTo(Tier.White));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 0.6f), Is.EqualTo(Tier.Yellow));
            Assert.That(ProbabilityTable<Tier>.Sample(vector, 1f),   Is.EqualTo(Tier.Yellow));
        }

        [Test]
        public void ObservedFrequencies_MatchTheWeights_OverASeededSample()
        {
            var table = Table(white: 50f, blue: 30f, yellow: 15f, orange: 5f);
            var rng = new Random(12345);
            var counts = new int[ProbabilityTable<Tier>.Outcomes.Count];

            const int n = 200_000;
            for (var i = 0; i < n; i++)
            {
                var outcome = table.Sample((float)rng.NextDouble());
                counts[(int)IndexOf(outcome)]++;
            }

            AssertShare(counts, Tier.White, 0.50f, n);
            AssertShare(counts, Tier.Blue, 0.30f, n);
            AssertShare(counts, Tier.Yellow, 0.15f, n);
            AssertShare(counts, Tier.Orange, 0.05f, n);

            static void AssertShare(int[] counts, Tier tier, float expected, int n) =>
                Assert.That(counts[(int)IndexOf(tier)] / (float)n, Is.EqualTo(expected).Within(0.01f), tier.ToString());
        }

        private static int IndexOf(Tier tier)
        {
            var values = ProbabilityTable<Tier>.Outcomes;
            for (var i = 0; i < values.Count; i++)
                if (values[i] == tier) return i;
            return -1;
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableSampleTests.cs"`

- [ ] **Step 2: Run to verify they fail**

Per **Green**. Expected: compile error — `Sample` not defined.

- [ ] **Step 3: Add `Sample` to `ProbabilityTable<T>`**

In `ProbabilityTable.cs`, after `ProbabilityOf`:

```csharp
        /// <summary>
        /// Single-pass CDF walk. <paramref name="roll"/> is expected in [0, 1]. A roll of 0
        /// returns the first non-zero-probability outcome; a roll at or past the final
        /// threshold returns the last non-zero outcome — never a phantom default(T).
        /// </summary>
        public T Sample(float roll) => Sample(_probabilities, roll);

        /// <summary>
        /// Samples an arbitrary probability vector in enum-declaration order — the path the
        /// magic-find cascade takes, so the game has exactly one sampler.
        /// </summary>
        public static T Sample(IReadOnlyList<float> probabilities, float roll)
        {
            if (probabilities is null)
                throw new ArgumentNullException(nameof(probabilities));

            var cumulative = 0f;
            var lastNonZero = -1;

            for (var i = 0; i < probabilities.Count; i++)
            {
                if (probabilities[i] <= 0f)
                    continue;

                lastNonZero = i;
                cumulative += probabilities[i];

                if (roll <= cumulative)
                    return Outcomes[i];
            }

            return Outcomes[lastNonZero >= 0 ? lastNonZero : probabilities.Count - 1];
        }
```

- [ ] **Step 4: Run to verify they pass**

Per **Green**. Expected: all `ProbabilityTableSampleTests` PASS (the seeded-frequency test included — it is reproducible, not flaky); everything from Task 1 still green.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs" \
        "Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs.meta" \
        "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableSampleTests.cs" \
        "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableSampleTests.cs.meta"
git commit -m "feat: ProbabilityTable.Sample - a single-pass CDF walk taking the roll

Instance form samples the cached vector; a static form samples any vector so
the magic-find cascade can reuse it. Roll 0 skips zero-weight entries; a roll
past the final threshold returns the last non-zero outcome, never default(T)
- the phantom-no-drop bug the old GetRandomEnumerator could hit on rounding.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 3: Fail-weight exponent scaling

The designer authors a fail probability `p = failWeight / (failWeight + successSum)`; the *effective* fail probability is `p^e` where `e` is the ally-scaling exponent (`GetFailExponent()`, a `float` from Task 7 on). This is the existing algebra — it is correct and worth keeping — evaluated at read time. The `uint` round-trip that made a 5% no-drop unrepresentable on small tables is simply not present: it was never in `ProbabilityTable`, and Task 7 does not add it.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs` (one line in `Compute`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableFailWeightTests.cs` (+ `.cs.meta`)

**Interfaces:**
- Consumes: `ProbabilityTable<T>` (Tasks 1–2). No signature change.
- Produces: nothing new — `failExponent` now actually applies.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableFailWeightTests.cs`:

```csharp
using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks the fail-weight scaling: a designer fail probability p with exponent e
    /// yields an effective fail probability of exactly p^e; small fail probabilities
    /// such as 5% are representable on a small table (the regression for the old uint
    /// truncation); exponent 1 and exponent 0 behave.
    /// </summary>
    [TestFixture]
    public sealed class ProbabilityTableFailWeightTests
    {
        private const float Tol = 1e-4f;

        // Coin: [None, Copper, Silver, Gold] — success weights sum to `successSum`.
        private static ProbabilityTable<Coin> Table(float successSum, float failWeight, float exponent)
        {
            var each = successSum / 3f;
            return new ProbabilityTable<Coin>(new[] { 0f, each, each, each }, failWeight, exponent);
        }

        [Test]
        public void ExponentOne_GivesTheDesignerFailProbabilityDirectly()
        {
            // p = 10 / (10 + 90) = 0.10
            var table = Table(successSum: 90f, failWeight: 10f, exponent: 1f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.10f).Within(Tol));
        }

        [Test]
        public void ExponentTwo_SquaresTheDesignerFailProbability()
        {
            // p = 0.25 → p^2 = 0.0625
            var table = Table(successSum: 30f, failWeight: 10f, exponent: 2f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.0625f).Within(Tol));
        }

        [Test]
        public void FractionalExponent_IsHonoured()
        {
            // p = 0.5, e = 1.5 → 0.5^1.5 = 0.353553...
            var table = Table(successSum: 10f, failWeight: 10f, exponent: 1.5f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.35355f).Within(Tol));
        }

        [Test]
        public void SmallFailProbability_IsRepresentable_OnASmallTable()
        {
            // The old (uint) cast on a table whose success weights total 6 forced
            // a 10% ask down to 0. Here 5% on a small table must land near 5%.
            // p = f / (f + 6) = 0.05  →  f = 6 * 0.05 / 0.95 = 0.31578...
            var table = new ProbabilityTable<Coin>(new[] { 0f, 3f, 2f, 1f }, failWeight: 0.31578f, failExponent: 1f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0.05f).Within(1e-3f));
            Assert.That(table.ProbabilityOf(Coin.None), Is.GreaterThan(0f), "not truncated to zero");
        }

        [Test]
        public void ZeroFailWeight_MeansNoFailBucket()
        {
            var table = Table(successSum: 100f, failWeight: 0f, exponent: 3f);

            Assert.That(table.ProbabilityOf(Coin.None), Is.EqualTo(0f));
            Assert.That(Sum(table), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void SuccessMembers_ShareTheRemainderAfterFail()
        {
            var table = Table(successSum: 90f, failWeight: 10f, exponent: 1f); // P(None) = 0.10

            // each success member is 30/90 of the remaining 0.90
            Assert.That(table.ProbabilityOf(Coin.Copper), Is.EqualTo(0.30f).Within(Tol));
            Assert.That(Sum(table), Is.EqualTo(1f).Within(Tol));
        }

        private static float Sum(ProbabilityTable<Coin> table)
        {
            var s = 0f;
            foreach (var v in table.Probabilities) s += v;
            return s;
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableFailWeightTests.cs"`

- [ ] **Step 2: Run to verify they fail**

Per **Green**. Expected: `ExponentTwo_…`, `FractionalExponent_…`, `SmallFailProbability_…` FAIL — `Compute` currently returns `p`, not `p^e`. `ExponentOne_…` passes already.

- [ ] **Step 3: Apply the exponent in `Compute`**

In `ProbabilityTable.cs`, replace this line:

```csharp
            var pFail = FailIndex >= 0 && f > 0f ? f / (f + successSum) : 0f; // Task 3 adds the exponent
```

with:

```csharp
            var e = failExponent > 0f ? failExponent : 1f;
            var pFail = FailIndex >= 0 && f > 0f
                ? MathF.Pow(f / (f + successSum), e)
                : 0f;
```

Add `using System;` is already present; `MathF` is in `System`. If `MathF` is somehow unavailable in this Unity's runtime profile, use `(float)Math.Pow(f / (f + successSum), e)` — same result.

- [ ] **Step 4: Run to verify they pass**

Per **Green**. Expected: all `ProbabilityTableFailWeightTests` PASS; Tasks 1–2 still green.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs" \
        "Assets/Scripts/InventorySystem/Probability/ProbabilityTable.cs.meta" \
        "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableFailWeightTests.cs" \
        "Assets/Scripts/Tests/EditMode/Probability/ProbabilityTableFailWeightTests.cs.meta"
git commit -m "feat: ProbabilityTable applies the ally-scaling fail exponent (p^e)

Effective fail probability is the designer's p = f/(f+S) raised to the
exponent, evaluated at read time. All-float: a 5% no-drop on a small table
is now representable, which the old (uint) cast truncated to 0.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 4: `WeightMigration.Remap` — keep tuned weights when the enum changes

A pure helper the adapter's `OnValidate` will call. Keys authored weights on the enum **value**, so adding, removing, reordering, or uncommenting a member keeps every weight that still has a home; only a genuinely removed member loses its weight.

**Files:**
- Create: `Assets/Scripts/InventorySystem/Probability/WeightMigration.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs` (+ `.cs.meta`)

**Interfaces:**
- Consumes: nothing.
- Produces: `static float[] WeightMigration.Remap<T>(IReadOnlyList<T> oldOutcomes, IReadOnlyList<float> oldWeights, IReadOnlyList<T> newOutcomes) where T : System.Enum`. Task 7 calls it from `OnValidate`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs`:

```csharp
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks WeightMigration.Remap: authored weights are re-homed by enum VALUE, not by
    /// array position — so a reorder keeps every weight, an insertion gets weight 0, and
    /// only a removed member's weight is dropped.
    /// </summary>
    [TestFixture]
    public sealed class WeightMigrationTests
    {
        [Test]
        public void Reorder_KeepsEveryWeight_MappedByValue()
        {
            var oldOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };
            var oldWeights = new[] { 10f, 20f, 30f };
            var newOrder = new[] { Tier.Yellow, Tier.White, Tier.Blue };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 30f, 10f, 20f }));
        }

        [Test]
        public void InsertedMember_GetsZero_OthersUnchanged()
        {
            var oldOrder = new[] { Tier.White, Tier.Yellow };
            var oldWeights = new[] { 10f, 30f };
            var newOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 10f, 0f, 30f }));
        }

        [Test]
        public void RemovedMember_LosesItsWeight_TheRestSurvive()
        {
            var oldOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };
            var oldWeights = new[] { 10f, 20f, 30f };
            var newOrder = new[] { Tier.White, Tier.Yellow };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 10f, 30f }));
        }

        [Test]
        public void FreshTable_AllZero_WhenNothingWasAuthored()
        {
            var result = WeightMigration.Remap(
                new Tier[0], new float[0], new[] { Tier.White, Tier.Blue });

            Assert.That(result, Is.EqualTo(new[] { 0f, 0f }));
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs"`

- [ ] **Step 2: Run to verify they fail** — compile error, `WeightMigration` undefined.

- [ ] **Step 3: Write `WeightMigration`**

Create `Assets/Scripts/InventorySystem/Probability/WeightMigration.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// Re-homes authored weights when an enum changes. Keys on the enum <em>value</em>,
    /// so adding, removing, reordering, or uncommenting a member keeps every weight that
    /// still has a home; only a genuinely removed member loses its weight.
    /// </summary>
    public static class WeightMigration
    {
        public static float[] Remap<T>(
            IReadOnlyList<T> oldOutcomes,
            IReadOnlyList<float> oldWeights,
            IReadOnlyList<T> newOutcomes) where T : Enum
        {
            if (oldOutcomes is null) throw new ArgumentNullException(nameof(oldOutcomes));
            if (oldWeights is null) throw new ArgumentNullException(nameof(oldWeights));
            if (newOutcomes is null) throw new ArgumentNullException(nameof(newOutcomes));
            if (oldOutcomes.Count != oldWeights.Count)
                throw new ArgumentException("oldOutcomes and oldWeights must be the same length");

            var byValue = new Dictionary<T, float>();
            for (var i = 0; i < oldOutcomes.Count; i++)
                byValue[oldOutcomes[i]] = oldWeights[i]; // last write wins on aliased values

            var result = new float[newOutcomes.Count];
            for (var i = 0; i < newOutcomes.Count; i++)
                result[i] = byValue.TryGetValue(newOutcomes[i], out var w) ? w : 0f;

            return result;
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/InventorySystem/Probability/WeightMigration.cs"`

- [ ] **Step 4: Run to verify they pass** — all `WeightMigrationTests` PASS; everything else green.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Probability/WeightMigration.cs" \
        "Assets/Scripts/InventorySystem/Probability/WeightMigration.cs.meta" \
        "Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs" \
        "Assets/Scripts/Tests/EditMode/Probability/WeightMigrationTests.cs.meta"
git commit -m "feat: WeightMigration.Remap - re-home authored weights by enum value

Editing a rarity ladder (add / remove / reorder / uncomment a member) no
longer discards every tuned weight. Only a genuinely removed member's weight
is dropped. The adapter's OnValidate calls this in Task 7.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 5: `MagicFindCascade.Apply` — Diablo II's rarest-first cascade

A pure `(base probability vector, magic find) → new probability vector` transform. Quality is checked rarest-first; each rung carries its own diminishing-returns factor; first hit wins. The fail bucket is excluded from the cascade entirely — it operates on the success mass and scales by `1 - P(fail)`, so `P(fail)` is invariant under magic find by construction. Magic find of 0 reproduces the input vector exactly.

**Files:**
- Create: `Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs` (+ `.cs.meta`)

**Interfaces:**
- Consumes: `ProbabilityTable<Tier>.Sample` (for one assertion) — otherwise nothing.
- Produces:
  ```csharp
  static float[] MagicFindCascade.Apply(
      IReadOnlyList<float> baseProbabilities, // full vector, enum order, sums to 1
      int failIndex,                          // index of the fail bucket, or -1; excluded
      IReadOnlyList<int> rarityOrder,         // success indices, RAREST FIRST
      IReadOnlyList<float> diminishingFactors,// parallel to rarityOrder; <= 0 means linear
      float magicFind)                        // percent; 0 reproduces baseProbabilities
  ```
  Task 8's `RarityMagicFind` supplies `failIndex`, `rarityOrder`, `diminishingFactors` for `ItemRarity`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs`. These pin the spec's invariants and its regression table (design § "Magic find — the Diablo II cascade").

```csharp
using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks MagicFindCascade against the spec's invariants and regression table.
    /// Tier order is [Nothing, White, Blue, Yellow, Orange]; rarest-first is
    /// [Orange, Yellow, Blue, White] with Diablo II's factors 250 / 600 / linear / linear.
    /// Live weights: White 160, Blue 80, Yellow 40, Orange 20 (== ItemRarity's asset).
    /// </summary>
    [TestFixture]
    public sealed class MagicFindCascadeTests
    {
        private const float Tol = 3e-3f; // the spec table is quoted to 0.1%; this covers its display rounding

        // indices into a Tier vector
        private const int Nothing = 0, White = 1, Blue = 2, Yellow = 3, Orange = 4;
        private static readonly int[] RarestFirst = { Orange, Yellow, Blue, White };
        private static readonly float[] Factors = { 250f, 600f, 0f, 0f };

        private static float[] Base(float nothing, float white, float blue, float yellow, float orange)
        {
            var raw = new[] { nothing, white, blue, yellow, orange };
            var sum = 0f;
            foreach (var v in raw) sum += v;
            for (var i = 0; i < raw.Length; i++) raw[i] /= sum;
            return raw;
        }

        private static float[] Live() => Base(0f, 160f, 80f, 40f, 20f);

        private static float[] Apply(float[] baseVector, float magicFind) =>
            MagicFindCascade.Apply(baseVector, Nothing, RarestFirst, Factors, magicFind);

        [Test]
        public void MagicFindZero_ReproducesTheAuthoredTableExactly()
        {
            var b = Live();
            var result = Apply(b, 0f);

            for (var i = 0; i < b.Length; i++)
                Assert.That(result[i], Is.EqualTo(b[i]).Within(1e-6f), $"index {i}");
        }

        [Test]
        public void RegressionTable_LiveWeights()
        {
            // design § "Behaviour at the live weights ... as the regression target"
            AssertRow(0f,   common: 0.533f, magic: 0.267f, rare: 0.133f, unique: 0.067f);
            AssertRow(100f, common: 0.217f, magic: 0.434f, rare: 0.235f, unique: 0.114f);
            AssertRow(200f, common: 0.000f, magic: 0.552f, rare: 0.307f, unique: 0.141f);
            AssertRow(400f, common: 0.000f, magic: 0.427f, rare: 0.404f, unique: 0.169f);
            AssertRow(800f, common: 0.000f, magic: 0.296f, rare: 0.510f, unique: 0.194f);

            void AssertRow(float mf, float common, float magic, float rare, float unique)
            {
                var r = Apply(Live(), mf);
                Assert.That(r[White],  Is.EqualTo(common).Within(Tol), $"Common @ {mf}");
                Assert.That(r[Blue],   Is.EqualTo(magic).Within(Tol),  $"Magic @ {mf}");
                Assert.That(r[Yellow], Is.EqualTo(rare).Within(Tol),   $"Rare @ {mf}");
                Assert.That(r[Orange], Is.EqualTo(unique).Within(Tol), $"Unique @ {mf}");
            }
        }

        [Test]
        public void PNoDrop_IsInvariantUnderMagicFind_EvenWithANonZeroFailWeight()
        {
            // headline regression: a table that ships fail probability 0.08
            var b = NormalizedWithFail(pFail: 0.08f, white: 160f, blue: 80f, yellow: 40f, orange: 20f);

            foreach (var mf in new[] { 0f, 50f, 100f, 250f, 500f, 1000f, 5000f })
                Assert.That(Apply(b, mf)[Nothing], Is.EqualTo(0.08f).Within(1e-6f), $"P(NoDrop) @ {mf}");
        }

        [Test]
        public void PUnique_IsMonotonicNonDecreasing_And_PCommon_NonIncreasing()
        {
            var prevU = -1f;
            var prevC = 2f;
            for (var mf = 0f; mf <= 2000f; mf += 25f)
            {
                var r = Apply(Live(), mf);
                Assert.That(r[Orange], Is.GreaterThanOrEqualTo(prevU - 1e-5f), $"Unique dipped @ {mf}");
                Assert.That(r[White], Is.LessThanOrEqualTo(prevC + 1e-5f), $"Common rose @ {mf}");
                prevU = r[Orange];
                prevC = r[White];
            }
        }

        [Test]
        public void VectorSumsToOne_AndStaysInRange_AcrossTheWholeSweep()
        {
            for (var mf = 0f; mf <= 5000f; mf += 50f)
            {
                var r = Apply(Live(), mf);
                var sum = 0f;
                foreach (var v in r)
                {
                    Assert.That(v, Is.InRange(-1e-5f, 1f + 1e-5f), $"entry out of range @ {mf}");
                    sum += v;
                }
                Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"sum @ {mf}");
            }
        }

        [Test]
        public void ExtremeMagicFind_DoesNotThrow_AndNeverEmptiesTheDropTable()
        {
            foreach (var mf in new[] { 500f, 5000f, 1e6f, float.MaxValue })
            {
                Assert.That(() => Apply(Live(), mf), Throws.Nothing, $"mf {mf}");
                var r = Apply(Live(), mf);
                Assert.That(r[Nothing], Is.EqualTo(0f).Within(1e-6f), "still no phantom no-drop");
            }
        }

        [Test]
        public void Landmark_CommonReachesZero_At200PercentMagicFind()
        {
            // design § Further Notes — a deliberate, pinned consequence
            Assert.That(Apply(Live(), 199f)[White], Is.GreaterThan(0f));
            Assert.That(Apply(Live(), 200f)[White], Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Landmark_MagicOvertakesCommon_NearFiftyPercent()
        {
            Assert.That(Apply(Live(), 45f)[Blue],  Is.LessThan(Apply(Live(), 45f)[White]));
            Assert.That(Apply(Live(), 55f)[Blue],  Is.GreaterThan(Apply(Live(), 55f)[White]));
        }

        [Test]
        public void Landmark_RareOvertakesMagic_NearFourTwentyNine()
        {
            Assert.That(Apply(Live(), 420f)[Yellow], Is.LessThan(Apply(Live(), 420f)[Blue]));
            Assert.That(Apply(Live(), 440f)[Yellow], Is.GreaterThan(Apply(Live(), 440f)[Blue]));
        }

        private static float[] NormalizedWithFail(float pFail, float white, float blue, float yellow, float orange)
        {
            var k = (1f - pFail) / (white + blue + yellow + orange);
            return new[] { pFail, white * k, blue * k, yellow * k, orange * k };
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs"`

- [ ] **Step 2: Run to verify they fail** — compile error, `MagicFindCascade` undefined.

- [ ] **Step 3: Write `MagicFindCascade`**

Create `Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ToolSmiths.InventorySystem.Probability
{
    /// <summary>
    /// Diablo II's magic-find cascade as a pure vector transform. Quality is checked
    /// rarest-first; each rung carries its own diminishing-returns factor; first hit wins.
    /// Operates on the success mass only, scaled by <c>1 - P(fail)</c>, so <c>P(fail)</c>
    /// is invariant under magic find by construction. Magic find of 0 reproduces the input.
    ///
    /// Effective magic find per rung is Diablo II's <c>eff = mf * F / (mf + F)</c>; a
    /// non-positive factor means linear (no diminishing returns), as Diablo II's Magic
    /// quality. The conditional rung probabilities are derived from the base vector, not
    /// authored, which is what makes magic find of 0 an identity.
    /// </summary>
    public static class MagicFindCascade
    {
        public static float[] Apply(
            IReadOnlyList<float> baseProbabilities,
            int failIndex,
            IReadOnlyList<int> rarityOrder,
            IReadOnlyList<float> diminishingFactors,
            float magicFind)
        {
            if (baseProbabilities is null) throw new ArgumentNullException(nameof(baseProbabilities));
            if (rarityOrder is null) throw new ArgumentNullException(nameof(rarityOrder));
            if (diminishingFactors is null) throw new ArgumentNullException(nameof(diminishingFactors));
            if (rarityOrder.Count != diminishingFactors.Count)
                throw new ArgumentException("rarityOrder and diminishingFactors must be the same length");

            var result = new float[baseProbabilities.Count];

            var pFail = failIndex >= 0 && failIndex < baseProbabilities.Count ? baseProbabilities[failIndex] : 0f;
            var successMass = 1f - pFail;

            if (successMass <= 0f || magicFind <= 0f)
            {
                for (var i = 0; i < baseProbabilities.Count; i++)
                    result[i] = baseProbabilities[i];
                return result;
            }

            var rungs = rarityOrder.Count;
            var boosted = new float[rungs];

            // Base conditional rung probabilities, rarest first:
            //   cond[k] = p[order[k]] / (successMass - sum of p[order[j]] for j < k)
            var remaining = successMass;
            for (var k = 0; k < rungs; k++)
            {
                var p = baseProbabilities[rarityOrder[k]];
                var cond = remaining > 1e-9f ? p / remaining : 0f;

                var factor = diminishingFactors[k];
                var eff = factor > 0f ? magicFind * factor / (magicFind + factor) : magicFind;
                cond *= 1f + eff / 100f;

                boosted[k] = cond < 1f ? cond : 1f; // clamp to 1
                remaining -= p;
            }

            // Re-expand rarest-first into absolute success probabilities, then scale by the
            // success mass. The least-rare rung takes the remainder.
            //   P(order[0]) = b[0];  P(order[k]) = prod(1 - b[j], j < k) * b[k]
            var complement = 1f;
            for (var k = 0; k < rungs; k++)
            {
                var share = k < rungs - 1 ? complement * boosted[k] : complement;
                if (share < 0f) share = 0f;
                result[rarityOrder[k]] = share * successMass;
                complement -= share;
            }

            if (failIndex >= 0 && failIndex < result.Length)
                result[failIndex] = pFail;

            return result;
        }
    }
}
```

Meta: MonoImporter stanza, path `Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs.meta`.

- [ ] **Step 4: Run to verify they pass**

Per **Green**. Expected: every `MagicFindCascadeTests` PASS — the regression rows within `2e-3`, all invariants, both landmarks. Tasks 1–4 still green. **If a regression row is off by more than tolerance, the cascade algebra is wrong — do not widen the tolerance to make it pass.** Re-derive against design lines 178–206.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs" \
        "Assets/Scripts/InventorySystem/Probability/MagicFindCascade.cs.meta" \
        "Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs" \
        "Assets/Scripts/Tests/EditMode/Probability/MagicFindCascadeTests.cs.meta"
git commit -m "feat: MagicFindCascade - Diablo II's rarest-first cascade as a pure transform

(base vector, magic find) -> new vector. Fail bucket excluded; success mass
only; P(NoDrop) invariant by construction. mf 0 is an identity. Pins the
spec's regression table and the Common-hits-zero-at-200%% landmark.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 6: Non-generic `AbstractProbabilityDistribution` root

A compile-only refactor. Add a non-generic `abstract class AbstractProbabilityDistribution : ScriptableObject` and make `AbstractProbabilityDistribution<T>` derive from it. This is what lets **one** `[CustomEditor(typeof(AbstractProbabilityDistribution), true)]` in Task 9 draw every closed distribution — Unity cannot target an open generic. No behaviour changes.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/AbstractProbabilityDistribution.cs`
- Test: none possible — `ScriptableObject` in `Assembly-CSharp`. Verified by compile.

**Interfaces:**
- Consumes: nothing.
- Produces: `ToolSmiths.InventorySystem.Data.Distributions.AbstractProbabilityDistribution` (non-generic) with three abstract members the editor uses:
  - `IReadOnlyList<string> OutcomeNames { get; }`
  - `IReadOnlyList<float> Probabilities { get; }`
  - `string SampleName(float roll)`

- [ ] **Step 1: Add the non-generic base**

At the top of `AbstractProbabilityDistribution.cs`, inside the namespace, above `AbstractProbabilityDistribution<T>`:

```csharp
    /// <summary>
    /// Non-generic root so a single <c>[CustomEditor(typeof(AbstractProbabilityDistribution), true)]</c>
    /// can draw every closed distribution — Unity cannot target an open generic. All
    /// behaviour lives in <see cref="AbstractProbabilityDistribution{T}"/>.
    /// </summary>
    public abstract class AbstractProbabilityDistribution : ScriptableObject
    {
        /// <summary>Outcome names in enum-declaration order — row labels for the inspector.</summary>
        public abstract IReadOnlyList<string> OutcomeNames { get; }

        /// <summary>The current probability vector, enum order, summing to 1. Derived, never serialized.</summary>
        public abstract IReadOnlyList<float> Probabilities { get; }

        /// <summary>Rolls once against <paramref name="roll"/> in [0,1]; returns the outcome's name — inspector sample preview.</summary>
        public abstract string SampleName(float roll);
    }
```

Add `using System.Collections.Generic;` to the file if it is not already there. Change the class line:

```csharp
    public abstract class AbstractProbabilityDistribution<T> : ScriptableObject where T : System.Enum
```

to:

```csharp
    public abstract class AbstractProbabilityDistribution<T> : AbstractProbabilityDistribution where T : System.Enum
```

Leave the rest of `<T>` exactly as it is for this task — it will not compile clean yet because the three abstract members are unimplemented. That is expected; **this task is the abstract declarations, Task 7 is the implementation, and the project is red between them.** Fold both into one commit if you prefer a green history — see Step 3.

- [ ] **Step 2: Compile-check**

Per **Green**. Expected: `error CS0534` — `AbstractProbabilityDistribution<T>` does not implement inherited abstract members. This confirms the base is wired; proceed straight to Task 7 without committing.

- [ ] **Step 3: Do not commit yet**

Task 6 and Task 7 land as **one commit** — the non-generic base is meaningless without the adapter that implements it, and Unity will not compile `Assembly-CSharp` in between. Continue to Task 7; the commit at the end of Task 7 covers both.

---

## Task 7: Rewrite `AbstractProbabilityDistribution<T>` as an adapter

Serialized weights in, `ProbabilityTable<T>` built once and cached, everything delegated. `failQuantity` → `failWeight` (with `[FormerlySerializedAs]` so the 7 assets keep their `0`). `GetFailExponent()` returns `float`. `OnValidate` migrates weights by value via `WeightMigration.Remap` and stops writing derived values into serialized fields. `probabilities`, `successProbability`, `exampleResults`, `EnumerationProbability`, the LINQ `OrderBy`, and the ten `UnityEngine.Random` rolls per validate are all deleted. `GetRandomEnumerator` → `Roll()`. `ItemProvider`'s plain call sites and `ItemRarityDistribution`'s override are updated in the same commit so `Assembly-CSharp` stays green.

**Files:**
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/AbstractProbabilityDistribution.cs`
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/ItemRarityDistribution.cs`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs`
- Test: none possible. Verified by compile + Task 10.

**Interfaces:**
- Consumes: `ProbabilityTable<T>` (Tasks 1–3), `WeightMigration.Remap` (Task 4).
- Produces:
  - `AbstractProbabilityDistribution<T>.Roll() -> T` (replaces `GetRandomEnumerator(float)`)
  - `AbstractProbabilityDistribution<T>.ProbabilityOf(T) -> float`
  - `protected virtual float GetFailExponent()` (was `int`)
  - implements the non-generic `OutcomeNames` / `Probabilities` / `SampleName`
  - `EnumerationQuantity` struct stays (the serialized authoring row); `EnumerationProbability` is gone.
- Task 8 adds `ItemRarityDistribution.Roll(float magicFind)`. Between Task 7 and Task 8, `ItemProvider.GetRandomRarity()` calls the plain `Roll()` — magic find is *disabled*, not broken.

- [ ] **Step 1: Replace the body of `AbstractProbabilityDistribution.cs`**

Keep the file's non-generic base from Task 6. Replace the entire `AbstractProbabilityDistribution<T>` class with:

```csharp
    public abstract class AbstractProbabilityDistribution<T> : AbstractProbabilityDistribution where T : System.Enum
    {
        [System.Serializable]
        public struct EnumerationQuantity
        {
            [HideInInspector, SerializeField] public string name;
            [HideInInspector, SerializeField] public T Enumeration;
            [SerializeField, Min(0)] public uint Quantity;

            public EnumerationQuantity(T enumeration, uint quantity)
            {
                Enumeration = enumeration;
                name = enumeration.ToString();
                Quantity = quantity;
            }
        }

        private static readonly T[] Values = (T[])System.Enum.GetValues(typeof(T));

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("failQuantity")]
        private uint failWeight = 0;
        [SerializeField] private EnumerationQuantity[] quantities = FreshQuantities();

        [System.NonSerialized] private ProbabilityTable<T> _table;
        private ProbabilityTable<T> Table => _table ??= BuildTable();

        private ProbabilityTable<T> BuildTable()
        {
            var weights = new float[Values.Length];
            for (var i = 0; i < weights.Length && i < quantities.Length; i++)
                weights[i] = quantities[i].Quantity;

            return new ProbabilityTable<T>(weights, failWeight, GetFailExponent());
        }

        // ── inspector surface: all derived, nothing serialized ──
        public override IReadOnlyList<string> OutcomeNames => System.Array.ConvertAll(Values, v => v.ToString());
        public override IReadOnlyList<float> Probabilities => Table.Probabilities;
        public override string SampleName(float roll) => Table.Sample(roll).ToString();

        public float ProbabilityOf(T outcome) => Table.ProbabilityOf(outcome);

        /// <summary>The ally-scaling exponent on the fail probability. 1 on the generic base.</summary>
        protected virtual float GetFailExponent() => 1f;

        /// <summary>Rolls one outcome from the authored table.</summary>
        public T Roll() => Table.Sample(UnityEngine.Random.Range(0f, 1f));

        private void OnValidate()
        {
            quantities = Migrate(quantities);
            _table = null; // rebuilt lazily against the new data — never baked into a field
        }

        private static EnumerationQuantity[] FreshQuantities() =>
            System.Array.ConvertAll(Values, v => new EnumerationQuantity(v, 0u));

        private static EnumerationQuantity[] Migrate(EnumerationQuantity[] current)
        {
            current ??= System.Array.Empty<EnumerationQuantity>();

            var oldOutcomes = System.Array.ConvertAll(current, q => q.Enumeration);
            var oldWeights = System.Array.ConvertAll(current, q => (float)q.Quantity);
            var remapped = WeightMigration.Remap(oldOutcomes, oldWeights, Values);

            var next = new EnumerationQuantity[Values.Length];
            for (var i = 0; i < Values.Length; i++)
                next[i] = new EnumerationQuantity(Values[i], (uint)System.Math.Max(0, System.Math.Round(remapped[i])));
            return next;
        }
    }
```

File header: `using System.Collections.Generic;` and `using ToolSmiths.InventorySystem.Probability;` are needed; `using System.Linq;` is **no longer** needed — remove it (the `OrderBy` is gone). Keep `using UnityEngine;`.

> **On the spec's `AllySensitiveFailQuantity` → `EffectiveFailWeight` rename:** there is no standalone member to rename. The effective-fail computation (`S / (1/p^e − 1)`, then `P(fail) = p^e`) now lives entirely inside `ProbabilityTable.Compute` as `pFail`, which is algebraically identical. The inspector shows the effective fail probability directly as the `NoDrop` entry of the probability vector.

- [ ] **Step 2: Update `ItemRarityDistribution.GetFailExponent()` to `float`**

In `ItemRarityDistribution.cs`, replace:

```csharp
        protected override int GetFailExponent() =>
            Mathf.FloorToInt(1f                         // 1 for the killing player
            + AlliesWithinRange() * 1f                  // 1 more for each player that is a) partied with the killing player && b) within two screens
            + RemainingPlayers() * 0.5f);               // 0.5 for each remaining player (either unpartied or far away).
                                                        // => rounded down   
```

with:

```csharp
        protected override float GetFailExponent() =>
            1f                              // 1 for the killing player
            + AlliesWithinRange() * 1f      // 1 more for each partied player within two screens
            + RemainingPlayers() * 0.5f;    // 0.5 for each remaining player (unpartied or far)
```

The `Mathf.FloorToInt` is gone — it discarded the 0.5-per-distant-player term for odd player counts (spec defect: `GetFailExponent()` should return `float`).

- [ ] **Step 3: Make edit-mode and play-mode agree in `AlliesWithinRange()`**

Still in `ItemRarityDistribution.cs`, replace:

```csharp
        private int AlliesWithinRange() => Application.isPlaying ? 0 : Mathf.FloorToInt(Mathf.Min(exampleTotalPlayerCount - 1f, exampleAlliedPlayerCount)); // TODO: requires real inplementation
```

with:

```csharp
        // TODO: real player detection. Until multiplayer exists, the preview fields drive
        // this identically in edit and play mode — the old `Application.isPlaying ? 0`
        // branch made the two disagree (spec defect #3).
        private int AlliesWithinRange() =>
            Mathf.FloorToInt(Mathf.Min(exampleTotalPlayerCount - 1f, exampleAlliedPlayerCount));
```

At the shipped default (`exampleTotalPlayerCount = 1`) this is `0` and the exponent is `1` — unchanged behaviour, now build-safe and consistent.

- [ ] **Step 4: Rename the plain call sites in `ItemProvider.cs`**

Replace every `SomethingDistribution.GetRandomEnumerator()` (no argument) with `SomethingDistribution.Roll()`. There are 10, all in `ItemProvider.cs`:

- line ~80 `itemCategoryDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~95 `equipmentCategoryDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~109 `armamentsDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~129 `weaponCategoryDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~143 `oneHandDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~156 `twoHandDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~169 `offHandDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~182 `jewelryDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~251 `consumableTypeDistribution.GetRandomEnumerator()` → `.Roll()`
- line ~273 `currencyTypeDistribution.GetRandomEnumerator()` → `.Roll()`

Leave the two commented-out `// var unique = correspondingTypeDistribution.GetRandomEnumerator();` lines (they are dead pseudocode; changing them is noise). Then the magic-find call site at line ~291:

```csharp
        // TODO: implement falloff => ATM 300% will always drop legendaries
        private ItemRarity GetRandomRarity() => itemRarityDistribution.GetRandomEnumerator(CharacterProvider.Instance.Player.GetStatValue(StatName.IncreasedItemRarity));
```

becomes (interim — magic find disabled until Task 8):

```csharp
        // magic find re-enabled in the RarityMagicFind task
        private ItemRarity GetRandomRarity() => itemRarityDistribution.Roll();
```

- [ ] **Step 5: Confirm no other caller**

```bash
grep -rn "GetRandomEnumerator" --include=*.cs Assets/Scripts
```
Expected: only the two commented pseudocode lines in `ItemProvider.cs`. Any live hit needs the same rename.

- [ ] **Step 6: Compile-check + negative control**

Per **Green** (bridge — the Editor is open). Expected: 0 `error CS`. The 7 `*Distribution.asset` files still load — `[FormerlySerializedAs("failQuantity")]` carries their `0`; the now-unknown `probabilities` / `successProbability` / `exampleResults` keys are ignored until Task 10 reserializes them. Run the `DELIBERATE_SENTINEL_ERROR` control in `AbstractProbabilityDistribution.cs` once here.

- [ ] **Step 7: Commit (covers Tasks 6 + 7)**

```bash
git status
git add "Assets/Scripts/InventorySystem/Data/Distributions/AbstractProbabilityDistribution.cs" \
        "Assets/Scripts/InventorySystem/Data/Distributions/ItemRarityDistribution.cs" \
        "Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs"
git commit -m "refactor: AbstractProbabilityDistribution is a thin adapter over ProbabilityTable

A non-generic root lands so one custom editor can draw every closed
distribution. <T> keeps only serialized weights + failWeight; the table is
built once and cached, never baked into a field. OnValidate migrates weights
by enum value (WeightMigration) and no longer consumes ten global Random
rolls or writes derived arrays. GetRandomEnumerator -> Roll(); GetFailExponent
returns float (Mathf.FloorToInt discarded the half-player term). Dead:
EnumerationProbability, the LINQ OrderBy, successProbability, exampleResults.

Magic find is disabled for one commit - GetRandomRarity calls the plain
Roll() until the RarityMagicFind task. That is strictly better than the
current inverted behaviour.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 8: `ItemRarityDistribution.Roll(float magicFind)` via the cascade

Bind `MagicFindCascade` to `ItemRarity` — which slot is `NoDrop`, the rarest-first order, Diablo II's factors — in a small `RarityMagicFind` helper, and route `ItemProvider.GetRandomRarity()` back through it. The cascade output is sampled with `ProbabilityTable<ItemRarity>.Sample` — the same sampler — so there is one code path.

**Files:**
- Create: `Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs` (+ `.cs.meta`)
- Modify: `Assets/Scripts/InventorySystem/Data/Distributions/ItemRarityDistribution.cs`
- Modify: `Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs`
- Test: none possible in `Assembly-CSharp`. The maths under it (`MagicFindCascade`) is fully covered by Task 5; this task is the `ItemRarity` binding, verified by compile + Task 10's play check.

**Interfaces:**
- Consumes: `MagicFindCascade.Apply` (Task 5), `ProbabilityTable<ItemRarity>` (Tasks 1–3).
- Produces:
  - `public static float[] RarityMagicFind.Apply(IReadOnlyList<float> baseProbabilities, float magicFind)` — full `ItemRarity`-order vector
  - `public ItemRarity ItemRarityDistribution.Roll(float magicFind)`

- [ ] **Step 1: Write `RarityMagicFind`**

Create `Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs`:

```csharp
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    /// <summary>
    /// Binds <see cref="MagicFindCascade"/> to <see cref="ItemRarity"/>: the fail slot,
    /// the rarest-first order, and Diablo II's per-tier factors (Unique 250, Rare 600,
    /// Magic &amp; Common linear — no diminishing returns). Public so the inspector
    /// preview (Assembly-CSharp-Editor) can call it. Indices resolve by enum value, so
    /// a still-commented tier is simply skipped rather than shifting the others.
    /// </summary>
    public static class RarityMagicFind
    {
        // rarest first; 0f factor == linear
        private static readonly (ItemRarity tier, float factor)[] Ladder =
        {
            (ItemRarity.Unique, 250f),
            (ItemRarity.Rare,   600f),
            (ItemRarity.Magic,    0f),
            (ItemRarity.Common,   0f),
        };

        private static readonly int FailIndex;
        private static readonly int[] RarityOrder;
        private static readonly float[] Factors;

        static RarityMagicFind()
        {
            var values = ProbabilityTable<ItemRarity>.Outcomes;

            int IndexOf(ItemRarity r)
            {
                for (var i = 0; i < values.Count; i++)
                    if (EqualityComparer<ItemRarity>.Default.Equals(values[i], r))
                        return i;
                return -1;
            }

            FailIndex = IndexOf(default); // default(ItemRarity) == NoDrop

            var order = new List<int>();
            var factors = new List<float>();
            foreach (var (tier, factor) in Ladder)
            {
                var idx = IndexOf(tier);
                if (idx < 0) continue; // tier still commented out in ItemRarity.cs
                order.Add(idx);
                factors.Add(factor);
            }

            RarityOrder = order.ToArray();
            Factors = factors.ToArray();
        }

        public static float[] Apply(IReadOnlyList<float> baseProbabilities, float magicFind) =>
            MagicFindCascade.Apply(baseProbabilities, FailIndex, RarityOrder, Factors, magicFind);
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs"`

- [ ] **Step 2: Add `Roll(float magicFind)` to `ItemRarityDistribution`**

In `ItemRarityDistribution.cs`, add (inside the class):

```csharp
        /// <summary>
        /// Rolls a rarity with magic find applied as Diablo II's rarest-first cascade.
        /// A magic find of 0 is identical to <see cref="AbstractProbabilityDistribution{T}.Roll"/>.
        /// P(NoDrop) is unchanged at every magic-find value.
        /// </summary>
        public ItemRarity Roll(float magicFind)
        {
            if (magicFind <= 0f)
                return Roll();

            var cascaded = RarityMagicFind.Apply(Probabilities, magicFind);
            return ProbabilityTable<ItemRarity>.Sample(cascaded, Random.Range(0f, 1f));
        }
```

Add `using ToolSmiths.InventorySystem.Probability;` to the file. `Probabilities` is the inherited non-generic `IReadOnlyList<float>` (enum order). `Random` is `UnityEngine.Random` (already `using UnityEngine;`).

- [ ] **Step 3: Point `GetRandomRarity()` back at the cascade**

In `ItemProvider.cs`, the interim line from Task 7:

```csharp
        // magic find re-enabled in the RarityMagicFind task
        private ItemRarity GetRandomRarity() => itemRarityDistribution.Roll();
```

becomes:

```csharp
        private ItemRarity GetRandomRarity() =>
            itemRarityDistribution.Roll(CharacterProvider.Instance.Player.GetStatValue(StatName.IncreasedItemRarity));
```

- [ ] **Step 4: Compile-check + negative control**

Per **Green**. Expected: 0 `error CS`. Run the `DELIBERATE_SENTINEL_ERROR` control in `RarityMagicFind.cs` once.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs" \
        "Assets/Scripts/InventorySystem/Data/Distributions/RarityMagicFind.cs.meta" \
        "Assets/Scripts/InventorySystem/Data/Distributions/ItemRarityDistribution.cs" \
        "Assets/Scripts/InventorySystem/Runtime/Provider/ItemProvider.cs"
git commit -m "feat: magic find is Diablo II's cascade - IncreasedItemRarity now helps

RarityMagicFind binds MagicFindCascade to ItemRarity (NoDrop excluded,
rarest-first Unique/Rare/Magic/Common, factors 250/600/linear/linear).
ItemRarityDistribution.Roll(magicFind) transforms the base vector and samples
it through the same ProbabilityTable sampler. P(NoDrop) is invariant; mf 0
reproduces the authored table. Replaces the index-shift that handed the
no-drop bucket Unique's probability mass (400%% -> nothing ever dropped).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 9: Non-dirtying inspector — probability view + sample preview + magic-find landmarks

The old inspector dirtied the asset on every interaction because `OnValidate` wrote `probabilities` / `exampleResults` back into serialized fields. Those fields are gone (Task 7). Now render the derived values live in a custom editor that never writes to `serializedObject` unless the designer edits a real field.

**Files:**
- Create: `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ProbabilityDistributionEditor.cs` (+ `.cs.meta`)
- Create: `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ItemRarityDistributionEditor.cs` (+ `.cs.meta`)
- Folder meta for `Assets/Scripts/InventorySystem/Data/Distributions/Editor/` (Unity-generated is fine)
- Test: none — editor code. Verified by compile + Task 10's in-editor check.

**Interfaces:**
- Consumes: `AbstractProbabilityDistribution` (Task 6), `ItemRarityDistribution` + `RarityMagicFind` (Task 8).
- Produces: nothing consumed by other tasks.

- [ ] **Step 1: Write the base editor**

Create `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ProbabilityDistributionEditor.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Distributions;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions.EditorScripts
{
    /// <summary>
    /// Draws the authored fields, then the derived probability vector and a rolled sample
    /// as read-only labels. Nothing here writes to serializedObject, so merely inspecting
    /// an asset never dirties it — the version-control churn the old OnValidate caused.
    /// </summary>
    [CustomEditor(typeof(AbstractProbabilityDistribution), true)]
    public class ProbabilityDistributionEditor : Editor
    {
        private const int SampleSize = 20;
        private string _sample;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // failWeight + quantities — the only writable fields

            var dist = (AbstractProbabilityDistribution)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Probabilities — derived, not saved", EditorStyles.boldLabel);
            DrawVector(dist.OutcomeNames, dist.Probabilities);

            EditorGUILayout.Space();
            if (GUILayout.Button($"Roll {SampleSize} sample outcomes"))
                _sample = RollSample(dist);
            if (!string.IsNullOrEmpty(_sample))
                EditorGUILayout.HelpBox(_sample, MessageType.None);
        }

        protected static void DrawVector(IReadOnlyList<string> names, IReadOnlyList<float> probabilities)
        {
            for (var i = 0; i < names.Count && i < probabilities.Count; i++)
            {
                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rect, probabilities[i], $"{names[i]}   {probabilities[i] * 100f:0.0}%");
            }
        }

        private static string RollSample(AbstractProbabilityDistribution dist)
        {
            var rng = new System.Random();
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < SampleSize; i++)
            {
                var name = dist.SampleName((float)rng.NextDouble());
                counts.TryGetValue(name, out var c);
                counts[name] = c + 1;
            }
            return string.Join("\n", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/InventorySystem/Data/Distributions/Editor/ProbabilityDistributionEditor.cs"`

- [ ] **Step 2: Write the `ItemRarityDistribution` editor**

Create `Assets/Scripts/InventorySystem/Data/Distributions/Editor/ItemRarityDistributionEditor.cs`:

```csharp
using ToolSmiths.InventorySystem.Data.Distributions;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions.EditorScripts
{
    /// <summary>
    /// Adds a magic-find slider that previews the cascaded vector, plus the landmark
    /// crossover points so a retune is a deliberate, visible change (design § Further Notes).
    /// </summary>
    [CustomEditor(typeof(ItemRarityDistribution))]
    public class ItemRarityDistributionEditor : ProbabilityDistributionEditor
    {
        private float _magicFind;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var dist = (ItemRarityDistribution)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Magic-find preview (IncreasedItemRarity %)", EditorStyles.boldLabel);
            _magicFind = EditorGUILayout.Slider(_magicFind, 0f, 1500f);

            var cascaded = RarityMagicFind.Apply(dist.Probabilities, _magicFind);
            DrawVector(dist.OutcomeNames, cascaded);

            EditorGUILayout.HelpBox(
                "Landmarks — consequences of the base weights + Diablo II factors:\n" +
                "  Magic overtakes Common     ~50%\n" +
                "  Common reaches 0%           200%\n" +
                "  Rare overtakes Magic       ~429%\n" +
                "  Unique overtakes Magic    ~1364%\n" +
                "To retune, move the base weights or the factors in RarityMagicFind.cs.",
                MessageType.Info);
        }
    }
}
```

Write its `.cs.meta`: `metafor "Assets/Scripts/InventorySystem/Data/Distributions/Editor/ItemRarityDistributionEditor.cs"`. If Unity has not yet created `Assets/Scripts/InventorySystem/Data/Distributions/Editor.meta`, add it: `foldermetafor "Assets/Scripts/InventorySystem/Data/Distributions/Editor"`.

- [ ] **Step 3: Compile-check**

Per **Green**. Expected: 0 `error CS`. `RarityMagicFind` is `public` (Task 8), so `Assembly-CSharp-Editor` can call it. If Unity reports the editor cannot find `AbstractProbabilityDistribution`, the `Editor/` folder script landed in the wrong assembly — confirm there is no stray `.asmdef` above it.

- [ ] **Step 4: In-editor smoke check**

Ask the user (Editor is open): select `Item Rarity Distribution.asset`. Expect the probability bars (Common 53.3% … Unique 6.7%), a working "Roll 20 sample outcomes" button, and a magic-find slider that shifts the second set of bars — Common to 0% at 200. Selecting the asset and dragging the slider must **not** put a `*` on the asset or show it modified in `git status`.

- [ ] **Step 5: Commit**

```bash
git status
git add "Assets/Scripts/InventorySystem/Data/Distributions/Editor"
git commit -m "feat: non-dirtying distribution inspector + magic-find landmark preview

One [CustomEditor(typeof(AbstractProbabilityDistribution), true)] renders the
derived probability vector and a rolled sample as read-only labels - no write
to serializedObject, so inspecting an asset no longer dirties it. The
ItemRarityDistribution editor adds a magic-find slider previewing the cascade
and pins the crossover landmarks in a HelpBox.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Task 10: Reserialize the assets, verify in play, final green

Everything above is code. This task flushes the 7 distribution assets through Unity so their dead serialized keys drop and `failQuantity` → `failWeight` sticks, then verifies the whole thing in play mode and with a full batch EditMode run.

**Files:**
- Modify (in-editor reserialize): the 7 `Assets/Scripts/InventorySystem/Data/Distributions/*.asset` files — `Item Rarity Distribution`, `Item Category Distribution`, `Equipment Category Distribution`, `Weapon Category Distribution`, `Consumable Type Distribution`, `Currency Type Distribution`, and each `Equipment Type Distribution` variant (there may be several; `grep -rl "AbstractProbabilityDistribution\|m_Script.*a460b874" Assets --include=*.asset` — or just Reserialize All, below).

- [ ] **Step 1: Reserialize the distribution assets**

In the Editor: select the `Assets/Scripts/InventorySystem/Data/Distributions/` folder, then **Assets ▸ Reserialize Assets** (or right-click ▸ Reserialize). Confirm:

```bash
git status --porcelain "Assets/Scripts/InventorySystem/Data/Distributions"
git diff "Assets/Scripts/InventorySystem/Data/Distributions/Item Rarity Distribution.asset"
```

Expected diff on each asset: `failQuantity: 0` → `failWeight: 0`, and the `successProbability` / `probabilities:` / `exampleResults:` blocks **removed**. The `quantities:` array and `exampleTotalPlayerCount` / `exampleAlliedPlayerCount` (rarity only) are unchanged. If any weight value changed, `Migrate` mis-rounded — stop and check `WeightMigration`.

- [ ] **Step 2: Play-test magic find**

Enter Play mode in `Assets/Scenes/Example.unity`.

- With no magic find (default), kill `DummyTarget`s / press the random-loot button repeatedly. Rarity spread should sit near Common 53% / Magic 27% / Rare 13% / Unique 7%, and NoDrop rolls should match the authored fail rate (0% on the shipped asset).
- Temporarily grant magic find: in `AbstractItem.cs:99` change `StatName.IncreasedItemRarity => 0f,` to `=> 300f,` (do **not** commit this). Re-enter play. Expect Common drops to nearly vanish, Rare/Unique to climb, and **loot volume not to drop** — the old build stopped dropping anything at 400%. Revert the line.
- Confirm no `IndexOutOfRangeException` or `Oh oh.. something is wrong` warning in the console at any point (the old code threw at magic find 500+).

- [ ] **Step 3: Full batch EditMode run**

Ask the user to close the Editor, then:

```bash
rm -f Temp/UnityLockfile
"/c/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe" -runTests -batchmode \
  -projectPath "C:/Users/loles/Desktop/LEONID/InventoryTetris" -testPlatform EditMode \
  -testResults "C:/Users/loles/AppData/Local/Temp/claude/pdr-final.xml" \
  -logFile "C:/Users/loles/AppData/Local/Temp/claude/pdr-final.log"
```

Read `<test-run … passed= failed=>` from the XML: every `InventorySystem.Probability.Tests` fixture green, `InventorySystem.Data.Tests` and `InventorySystem.Geometry.Tests` unchanged. `grep -c "error CS" pdr-final.log` → 0. Run the negative control once against `ProbabilityTable.cs` if it has not been run this session.

- [ ] **Step 4: Commit the reserialized assets**

```bash
git status
git add "Assets/Scripts/InventorySystem/Data/Distributions"/*.asset
git commit -m "chore: reserialize distribution assets onto the adapter layout

failQuantity -> failWeight (FormerlySerializedAs carried the 0); the dead
probabilities / successProbability / exampleResults caches are gone from
every *Distribution.asset. No weight values change.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Done when

- `IncreasedItemRarity` raises `P(Unique)` and lowers `P(Common)` monotonically, and **never** changes `P(NoDrop)` — pinned by `MagicFindCascadeTests`.
- Magic find of 0 reproduces `Item Rarity Distribution.asset` exactly.
- No magic-find value throws or empties the drop table (was: `IndexOutOfRangeException` at 500+, 100% NoDrop at 400+).
- `grep -rn "GetRandomEnumerator" --include=*.cs Assets/Scripts` returns only commented pseudocode.
- The whole probability subsystem is reachable from `InventorySystem.Probability.Tests`: `ProbabilityTable<T>`, `MagicFindCascade`, `WeightMigration` all have red-green coverage; no test instantiates a `ScriptableObject`.
- A 5% fail probability is representable on a small table (`ProbabilityTableFailWeightTests`).
- Selecting or tweaking a distribution asset in the inspector does not dirty it — `git status` stays clean after inspecting.
- Editing the `ItemRarity` enum (reorder / insert) keeps every tuned weight, keyed by value.
- Full batch EditMode run: 0 `error CS`, all fixtures green, negative-control checked.

## Not in this plan

- **Balancing the rarity ladder.** Diablo II's factors and the current base weights ship as-is. Retuning is a designer pass (design § Out of Scope).
- **Granting magic find from gear.** `AbstractItem.cs:99` still returns `0f` for `IncreasedItemRarity`. Wiring it to affixes is separate work (design § Further Notes — "revisit when item balancing starts").
- **Real ally detection.** `AlliesWithinRange()` is still preview-field driven. This plan makes the mechanism build-safe and edit/play-consistent; multiplayer player detection is out of scope.
- **A `Set` tier**, per-equipment-type unique distributions, `ItemTypeData.StatRange`'s `AnimationCurve` weighting, and `IncreasedItemQuantity`'s formula — all untouched (design § Out of Scope).
- **Moving `Data/Enums` / `Data/Items` / `Data/Structs` into asmdef assemblies.** The pure table is generic over the enum, so none of that is needed.
