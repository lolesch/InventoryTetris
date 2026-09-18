# Coding Conventions

## Lazy GetComponent caching

```csharp
field ? field : field = GetComponent<T>();
```

OK if lookup always succeeds (same GameObject + `[RequireComponent]`).
BAD if it can legitimately stay null forever (optional lookup, e.g. component on a
*parent*) — null can't tell "not looked up yet" from "looked up, none found", so it
re-runs `GetComponent` every access.

Fix: resolve explicitly.
- `Awake()` — once, covers runtime-instantiated objects.
- `OnValidate()` — re-resolve when the relationship can change at edit time (reparent).

Reassign the cached ref *after* any call using the old value — else identity guards
elsewhere (`x.Owner != this`) see the new value early and no-op.

## Auto-properties over backing fields

```csharp
[field: SerializeField, ReadOnly] public T X { get; private set; }
```

not

```csharp
[SerializeField, ReadOnly] protected T x = null;
public T X => x;
```

`private set` by default. `protected set` only if a subclass writes it directly.

## Keep ownership guards on public mutators

`Select(toggle)` etc: keep `toggle.Owner != this` even if every current call site
already satisfies it. Guards the public API against future/external callers.

## Seal classes by default

No inheritors → `sealed`. Drop only when something actually subclasses it.

## Guard `UnityEditor` usings, not just call sites

```csharp
#if UNITY_EDITOR
using UnityEditor;
#endif
```

BAD: guarding the call site but leaving the `using` bare. Player builds don't reference
`UnityEditor.dll`, so a bare `using UnityEditor;` fails to compile (`CS0246`) regardless of
whether anything under it runs — the guard only works if the `using` is inside it too.

Skip it only when the whole file already compiles editor-only by construction (asmdef
`includePlatforms: ["Editor"]`, or an `Editor`/`EditMode` folder name — see
`codebase-notes.md`'s asmdef section); guarding there is a no-op.
