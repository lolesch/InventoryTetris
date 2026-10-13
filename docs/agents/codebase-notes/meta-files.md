# A `CS0103` / `CS0246` that is really a broken `.meta`

Script metas here come in three shapes, and only one of them breaks anything. Name them,
because the two healthy ones keep getting re-reported as bugs:

| Shape | On disk | Unity's verdict |
|---|---|---|
| **full** | 11 lines, ~254 B, ends `assetBundleVariant: ` + newline | fine |
| **short** | 59–62 B (or ~85 B with `timeCreated:`) — `fileFormatVersion` + `guid` and no `MonoImporter:` block | fine; Unity rewrites the full form on its next import |
| **cut** | opens the `MonoImporter:` block but stops mid-line, ~239 B, no trailing newline | rejected |

A **cut** meta fails YAML parsing (`Parser Failure at line 11: Expect ':' ...`), Unity logs
"does not have a valid GUID and its corresponding Asset file will be ignored", the `.cs`
never compiles, and every consumer fails `CS0103: The name 'X' does not exist`. It **looks
like a missing-assembly / asmdef-reference problem and is not.** A **warm Library masks
it** — a session that already imported good metas runs the whole suite green with cut metas
still on disk. A fresh checkout / Library wipe breaks.

**Confirm the shape before touching a meta.** `AssetDatabase.AssetPathToGUID(path)` is the
authority: `""` means Unity ignored it (**cut**), any other value means it resolved
(**full** or **short**). On disk the signature of a **cut** meta is that it starts the
importer block and never finishes it: `MonoImporter` present, last byte not a newline. The
pre-commit hook (`dev/hooks/pre-commit`) runs that check on every staged `*.cs.meta`.

**`grep -L assetBundleVariant` finds the opposite set.** The cut lands *at* that line, so a
**cut** meta still contains the string and the grep skips it; what it returns is every
**short** meta, all of them healthy. A scan built on it hands you a long, confident,
entirely wrong worklist — that is exactly how #80 got filed.

Measured 2026-09-19 (#80): 91 of 282 metas were **short**, none **cut**; EditMode on a copy with no
`Library/` passed 762/762 before and after normalising them, so leave **short** metas alone.

To repair a genuinely **cut** meta: rewrite it canonically (`fileFormatVersion: 2` …
`assetBundleVariant: ` + trailing newline), **preserving the committed GUID** — grep
`Assets/Scenes/*.unity` for that GUID first; scene `m_Script` refs break if it changes.
Then `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` +
`CompilationPipeline.RequestScriptCompilation()` through the bridge.
