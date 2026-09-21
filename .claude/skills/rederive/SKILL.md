---
name: rederive
description: Re-derive a mechanism from the invariant it should express — grill the human for the rule, probe whether today's mechanism can say it, design the replacement together.
disable-model-invocation: true
---

# Rederive

**Satisfies vs. expresses.** A mechanism *satisfies* an invariant when today's cases
happen to come out right. It *expresses* one when no case can come out wrong. The gap
between the two is filled with **guards**, and a guard is the evidence: if honouring the
rule takes an `if`, the mechanism is the wrong shape.

`SidePanelContext` spent three tickets in that gap. Each Side Panel announced its own
context from `BeforeAppear` (#75) — correct while every context meant exactly one panel.
The rule was always "a trade context is the Hero Panel *plus* a Town Stop's panel," and
the moment that was said out loud the mechanism needed a guard: the Hero Panel belongs to
every context, so it can never announce one. The mechanism mapped panel → context; the
invariant maps context → panels. Backwards, and the wrong arity. #57, #74 and #75 each
refined the direction rather than questioning it, because no step ever asked the human to
state the rule as a mapping.

This skill asks. It sweeps the current mechanism, grills the human for the **invariant**,
probes whether the mechanism can express it, and re-derives one that can — checking the
derived design by **replaying** every case the old one handled.

**Not in scope:** whether the path to the new mechanism is well sequenced. That is
`/drift-review`, and it is the natural next call — this skill designs the destination,
that one audits the route. Also not the spec itself (`/to-spec` writes it from this
skill's Decisions), and not one module's depth (`/simplify`, `codebase-design`).

## Process

### 1. Sweep

Read the mechanism before asking about it. Find every file that answers the question, the
tickets that built it (the workflow in `docs/agents/issue-tracker.md`), and any committed
spec proposing to change it — an unmerged design branch counts, and is easy to miss.

Done when you can name, in one sentence, **the question the mechanism answers**, with a
`file:line` for where it is answered and the ticket numbers that shaped it.

### 2. Grill for the invariant

Invoke `/grilling` with one question: *what rule must always hold, that today's cases only
happen to satisfy?* Take the answer verbatim — the invariant is domain knowledge only the
human has, never one to infer from the code, which encodes the mechanism you are
questioning.

Then write it back as a **mapping**: what determines what, in which direction, at what
arity. `one context → many panels`. `one context → exactly one sink, hub constant`. The
mapping is the whole leverage of step 3; prose that resists being written as one is not
an invariant yet, so keep grilling.

Done when the human has confirmed the mapping in their own words. Put the mapping last in
your message and stop — this step ends on their reply, not on your draft of it.

### 3. Probe

Two probes, each with a forced verdict. Run both.

- **Under-expression** — to honour the invariant, does the mechanism need a guard? Name
  the guard as a condition ("don't announce if the current context already contains me").
  A named guard is a fail, and the direction or arity mismatch in step 2's mapping is
  usually the reason.
- **Over-expression** — can the mechanism represent a state the invariant forbids? Name
  the state ("two sinks", "a context with no hub"). A representable illegal state is a
  fail: it is a bug waiting to be authored, not caught.

A pass on both means the mechanism expresses the invariant and the work is elsewhere —
say so and stop. Report each verdict with its named guard or state before deriving
anything, so the human can dispute the premise while it is still cheap.

### 4. Derive, then replay

Derive the mechanism from the mapping: make the invariant the thing the type or the data
says, so the illegal states of step 3 stop being representable.

Then **replay** it. List every case the old mechanism handled — the phase transitions,
the sibling panels, the out-of-scope deferrals its tickets recorded — and state each
one's outcome under the derived design. The replay is where the holes are: `leftPanels`
hid the Healer panel, so deleting the group left the Healer with nothing to hide it,
which no ticket tracked.

Take a position on the trade-offs rather than listing them. Where the human proposes an
alternative, argue it with a concrete failure case or concede; a pros-and-cons table with
no recommendation defers the work back to them.

Done when every replayed case has a stated outcome and every hole has an owner. This step
also ends on the human's reply.

### 5. Record

Output, in this order:

- **Invariant** — one line, the human's wording from step 2.
- **Probes** — the two verdicts, each with its named guard or illegal state.
- **Mechanism** — old and derived, as the two mappings.
- **Replay** — a table, columns exactly `Case | Old | Derived`, one row per case, each
  cell a phrase with an inline `file:line` or `#ticket`.
- **Decisions** — a flat list, each in the human's words as they settled it, each one a
  sentence someone could paste into a spec without rereading the conversation.

Close with the next command, its target already filled in: `/drift-review` over the
**existing** slice — the open tickets and merged commits this swap strands, not the slice
`/to-tickets` has yet to create. `/to-spec` follows in the same session, since it
synthesises this window rather than re-reading the repo. Decisions are the deliverable:
surface the design, and leave the spec and the issues for the human to trigger.
