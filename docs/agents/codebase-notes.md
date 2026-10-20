# Codebase Notes

Durable, hard-won facts about how *this* repo behaves that you can't get from the code, git history, or CLAUDE.md. This file is the index: open the one note whose trigger you are hitting, not all of them. Compile and test verification is in `unity-verification.md` (headless runs, worktree batch mode, batch Play Mode) and `unity-bridge.md` (the `unity-mcp` bridge, Play Mode by hand).

The notes are the shared channel for that knowledge across machines — an agent's private memory does not travel between devices, they do. Keep them current; prune what stops being true.

| Note (`docs/agents/codebase-notes/`) | Open it when |
|---|---|
| `editing-files.md` | a shell or python script edits source; `Edit` keeps CRLF + UTF-8, `perl -pi` mojibakes it |
| `play-mode-and-assemblies.md` | an asmdef changes or a reference won't resolve; a Play Mode entry behaves differently from the first (reload disabled); a static or `OnDisable` throws on Stop; the Play-exit check is run, or a PlayMode test is written (which tests belong there: `coding-conventions.md`, *Test level*) |
| `meta-files.md` | `CS0103` / `CS0246` doesn't match your diff, or a `.cs.meta` is suspect |
| `input.md` | a key or the pointer is read (`Input.*` throws here; use `GameInput`), or a hotkey test is written |
| `worktrees-and-utility.md` | work starts on a checkout that has uncommitted changes or another session's branch; a compile error doesn't match your diff; the `Utility` submodule is committed in |
| `services-test-fixtures.md` | a persistence, save or simulation test is written |

## Knowledge that lives outside git

Agent memory is per-machine; these notes, `GLOSSARY.md`, `docs/adr/`, `dev/specs/` and GitHub Issues
are the shared channels. A fact that matters on both the laptop and the tower belongs in one of them.

- **`NewArtwork`** branch (off an old `main`, pushed, no PR) is the only copy of three
  imported Asset Store UI packs under `Assets/Art/` (`GUI_Parts`, `Modern GDR - Free
  icons pack`, `RVFX / UIShaderEffects-EdgeEffects`, ~53 MB committed raw). Pull
  selectively into `main`/features, never wholesale. RVFX ships both a uGUI/Canvas and a
  UI Toolkit implementation; this project is Canvas-based, so the uGUI half is the usable one.
- **This repo uses no Git LFS.** Don't set one up without asking — it was tried once for
  a 279 MB audio pack that the user then rejected, and reverted. The only committed
  binaries are 6 pre-existing `*.dll`s.
