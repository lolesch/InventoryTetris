---
name: drift-review
description: Audit the path to a planned or already-built outcome for drift — sequencing that counters your own stated approach, stranded fixes, coverage gaps, swap residue — never whether the outcome itself is right.
disable-model-invocation: true
---

# Drift Review

**Outcome vs. path.** The outcome of a plan or a merge can be exactly right and the path
that reached it still wrong. Epic #71's spec was fine; the ticket slice fixed the old
Side-Panel exclusivity mechanism (`RadioGroup.ClearSelection()`) in ticket #72, then
replaced that mechanism with `PanelGroup` in #74 — #72 is verified behavior on a code path
#74 deletes, work that a scene-hierarchy-first path would have made unnecessary to fix at
all. Before that, `MapPanel`/`MinimapPanel` were built as workarounds and later replaced
wholesale. Same failure, twice: a **mechanism swap** — replacing what answers one question
("which panel is visible") with a structurally different implementation — got treated as
independent, addable-in-any-order tickets instead of the expand/migrate/contract sequence
`/to-tickets`' own wide-refactor rule calls for. The predictable result is a **stranded
fix**: work whose acceptance criteria, tests, and play-mode verification all pass against a
mechanism nothing calls anymore the moment the swap lands. A stranded fix is one species of
**drift** — outcome and path disagreeing about what was needed. Sequencing that just
counters your own instinct, with nothing technically wasted, is another; step 4 tests for
both.

This skill checks the path, in two places:

- **Planned** — a spec plus its ticket slice (an epic and children, or a fresh
  `/to-tickets` draft), before or during `/implement`.
- **Built** — merged commits, for a swap that already happened but wasn't fully retired.

**Not in scope:** whether the outcome itself is right — the spec being satisfiable, an
issue's acceptance criteria being sufficient. That's `/code-review`'s Spec axis or
`/to-tickets`' user quiz. This skill assumes the destination is correct and checks only
whether the route taken (or proposed) to it is the one you'd actually have picked, and
whether anything along it gets built then thrown away. It also doesn't replace
`/improve-codebase-architecture` (shallow-module depth, found by free exploration) — that's
about one module's shape, not a multi-step effort's sequencing.

## Process

### 1. Pick the target

If the user named a spec, epic issue, branch, or commit range, use it. Otherwise ask:
planned (an upcoming or in-flight ticket slice) or built (already merged)?

### 2. Get the reference path

Ask, unless already said in this conversation: *"If you were doing this yourself, what
order would you tackle it in, and why?"* Take the answer verbatim as the reference path —
this is a judgment only the user can supply, not one to infer or paraphrase. If a spec
already states a suggested order and the user has nothing to add, confirm that's the
reference path in one line rather than re-asking from scratch.

If they decline, or say the actual order is fine: skip step 4's order test. The other tests
need no reference path.

### 3. Reconstruct the actual path

List the tickets (in Blocked-by / frontier order) or commits (chronological) as actually
sequenced or proposed. Name the mechanism swap within it, if there is one: what answered
the question before, what answers it after, which step introduces the new one. A spec's
Solution / Implementation Decisions section usually names a swap directly ("X is replaced
by Y"); for a built-only target with no spec, a "refactor: replace X with Y" commit message
is the same signal — `git log --oneline --grep replace` first, widen if nothing comes back.

**Planned target, additionally:** fetch the epic and every child ticket (the workflow in
`docs/agents/issue-tracker.md`) and each ticket's state. For every ticket still open, check
whether a merged commit or a closed sibling already satisfies its acceptance criteria — the
attribution test in step 4 needs this.

Done when you can list the actual order as a numbered list, and, if a swap exists, state in
one sentence each what the old and new mechanisms are and which step introduces the new one.

### 4. Apply the tests

Each test below finds one species of drift, and each needs its own precondition — skip
whichever one isn't met: **Waste tests** need step 3 to have found a swap; **Order test**
needs step 2's reference path; **Attribution test** needs a planned target.

**Waste tests**, for every ticket or commit that touches the *old* mechanism:

- Does the swap-introducing ticket/commit stop calling it, delete it, or route around it?
  If the old mechanism has no callers left once the swap lands, anything that fixed or
  hardened it is now stranded — flag it.
- Does a ticket note a gap explicitly ("not covered here: X — that's ticket Y's slice")?
  Open Y and confirm X is actually in its acceptance criteria. If it silently isn't, that's
  a coverage gap the swap will ship without.
- **Built target only:** grep the current codebase for the pre-swap type, field, or method
  name. A hit that's unreachable, unused, or serialized-but-unread is swap residue — the
  retirement half of expand/migrate/**contract** never happened.

**Order test**, only if step 2 produced a reference path: diff the actual order (step 3)
against it. Where they diverge on what comes first, that's a sequencing mismatch — flag it
even when nothing is technically wasted, and name what the actual order cost that the
reference order wouldn't have (information deferred, verification duplicated, risk carried
longer).

**Attribution test**, for planned targets: for every open ticket, does a commit already
merged under a *different* ticket's number satisfy its acceptance criteria? That's an
**orphaned resolution** — the fix landed and works, but under a sibling ticket's number
instead of its own, so the ticket sits open and undiscoverable from its own number until
someone reads the code instead of the tracker.

A ticket or commit clears when none of the tests flags it: its fix survives the swap, it
*is* the swap, it matches the reference order, or its own number already credits its
resolution.

### 5. Report

State the fact the tests ran against, in one sentence: the swap (old mechanism → new
mechanism → introduced by which ticket/commit) if step 3 found one, otherwise the reference
path vs. the actual order. Don't narrate having verified it ("I confirmed that...") — that's
process, not finding.

Findings are a table, not a bulleted field list — a table's cells cap the sentence a field
tempts you to write. Columns exactly: `Mechanism | Vs. | Drift | Recommendation`. One row
per finding, each cell a phrase or short clause with an inline `file:line` or `#ticket`
citation, never a quote block, never more than one sentence. `Vs.` names what the finding is
measured against: the replacing ticket/commit/spec-section; the reference-path step it
should have followed instead, for a sequencing mismatch; or the commit that actually
resolved it, for an orphaned resolution — "compared against" reads correctly in all three
without checking `Drift` first. `Drift` is `stranded fix` / `coverage gap` / `swap residue`
/ `sequencing mismatch` / `orphaned resolution`. `Recommendation` is one of: reorder (name
the ticket that should be the frontier instead), retire-together (fold into the swap
ticket), patch-the-gap (name the one thing missing), or relink (comment the resolving commit
onto the orphaned ticket, then close it).

Below the table: `Cleared:` one line, each ticket/commit that passed plus a parenthetical
reason of ≤6 words — no elaboration.

Close with 1–2 sentences: the counts per drift kind, and the practical implication — which
ticket is actually the next frontier, or what the swap ticket's scope needs to grow by.
That's the last line. No "want me to…" offer, no restating the table in prose. The report is
the deliverable, meant to be pasted into `/to-tickets` or an issue edit — stop there.
Findings are diagnostic, not action — per this repo's own rule, surface drift and let the
user decide the resequencing; don't edit issues or revert commits unasked. If they do ask:

### 6. If asked to act

Resequencing tickets goes through `/to-tickets`, so its vertical-slice and wide-refactor
rules apply to the new order too. Everything else — closing, relabeling, commenting — uses
the `gh` conventions in `docs/agents/issue-tracker.md`.
