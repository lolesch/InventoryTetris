# Coding Conventions

## Lazy GetComponent caching

```csharp
field ? field : field = GetComponent<T>();
```

Use when the lookup always succeeds (same GameObject + `[RequireComponent]`).

An optional lookup (e.g. component on a *parent*) can stay null forever, and null can't
tell "not looked up yet" from "looked up, none found" — every access re-runs
`GetComponent`. Resolve explicitly instead:
- `Awake()` — once, covers runtime-instantiated objects.
- `OnValidate()` — re-resolve when the relationship can change at edit time (reparent).

Reassign the cached ref *after* any call using the old value — else identity guards
elsewhere (`x.Owner != this`) see the new value early and no-op.

## Auto-properties over backing fields

```csharp
[field: SerializeField, ReadOnly] public T X { get; private set; }
```

replaces `[SerializeField, ReadOnly] protected T x = null; public T X => x;`.

`protected set` only if a subclass writes it directly.

## Ownership guards on public mutators

`Select(toggle)` etc. keep `toggle.Owner != this` even when every current call site
satisfies it — it protects the API from future and external callers.

## Public API needs an interface

Every class or MonoBehaviour that other systems call into gets an `IThing`, and callers
depend on that instead of the concrete type. Keeps call sites mockable and keeps
serialized fields and Unity lifecycle methods out of the contract.

## Enforced by the pre-commit hook

`dev/hooks/pre-commit` (enable: `git config core.hooksPath dev/hooks`) rejects what a linter would:

- A class that nothing inherits and that is not `sealed`, `abstract`, `static` or `partial`.
- A `using UnityEditor;` outside an `Editor`/`Tests` folder that is not under `#if UNITY_EDITOR`
  (a player build has no `UnityEditor.dll`, so it fails with `CS0246` even if nothing runs).

Files that compile editor-only by construction (asmdef `includePlatforms: ["Editor"]`, an
`Editor`/`EditMode` folder; see `codebase-notes.md`'s asmdef section) need no guard.

## Reverse `for` loops

```csharp
for (var i = list.Count; i-- > 0;)
```

`i` is already the last valid index inside the body.
