# Editing source through a shell

## Scripted edits from git-bash

**Reach for `Edit` first, for one file or fifty.** It keeps CRLF and UTF-8 (next section), needs no
quoting, and a repeated `old_string`/`new_string` pair is cheaper than a script that re-implements the
line-ending handling (eight such scripts died on a stray quote in the transcripts of 10/5-10/7).
`Edit` and `Write` refuse a file you have not `Read` in the current context, and a compaction empties
that record: after one, re-`Read` the file before editing instead of falling back to `python`.

If a script is genuinely the right tool (a pattern across many files), it runs under git-bash on
Windows, so a `python` or `.exe` call sees Windows paths and the shell sees MSYS ones:

- **`/tmp` is not the same folder to both.** A script written to `/tmp/x.py` and run by `python` fails
  to import its neighbours. Write throwaway scripts to the session's scratchpad directory and run them
  from there.
- **Inline `python - <<'EOF'` heredocs break on a stray backtick or quote in the body** (one killed a
  command with `Permission denied`). Past a few lines, write the script to a file and run it.
- **Anything that rewrites many files must keep each file's line endings** (CRLF and LF both exist in
  the working tree). Read with `newline=''`, detect `\r\n`, and convert the replacement strings.

## Source is CRLF + UTF-8 — stream editors can still corrupt it

Source under `Assets/Scripts/` and the docs are **CRLF in the working tree, LF in the index**
(`.gitattributes`: `*.cs` and `*.md` are `text eol=crlf`, so this holds whatever `core.autocrlf`
says). The docstrings are dense with em dashes (—) and other non-ASCII. Because git normalises
on add, a stream editor's line-ending flip no longer shows up as churn, and a new file from `Write`
arrives LF and stays that way: converting it to CRLF changes nothing git sees. The encoding damage does:

- **`perl -0pi -e` mojibakes existing UTF-8** (— becomes â) as soon as the replacement
  string itself contains a wide character — it switches to character semantics on output
  only. The pre-commit hook rejects mojibake in added lines of `*.cs` and `*.md`.
- **`sed -i`** rewrites with LF; harmless to git now, but it still changes the working copy that
  Unity and the editors read.

Neither failure shows up in a test run: the code still compiles and the suite still passes.

**Use an editor tool that preserves encoding and endings** (Claude Code's `Edit`) for
anything touching these files, even mechanical multi-file renames. If a stream editor is
genuinely the right tool, restrict the glob to files that will actually match and check
`git diff` afterwards.
