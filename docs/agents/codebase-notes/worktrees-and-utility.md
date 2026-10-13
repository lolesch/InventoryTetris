# Worktrees and the `Utility` submodule

The primary working directory and the `Assets/Submodules/Utility` checkout are **one folder on
disk, shared across concurrent sessions and branches.** Open `git worktree add <path> -b <branch>`
for any new independent line of work (an issue, a prototype, a docs/spec pass, a follow-up fix while the
checkout holds uncommitted changes); plain checkout only continues the task already checked out. Three
follow-ups sharing one checkout (a menu fix, a key rebinding, a `Map` clamp) ended as one PR of three
unrelated topics, with an agent stashing a foreign patch to commit its own files. Mixing work into one checkout cross-contaminated two
branches once, and the untangle hit a submodule merge conflict. Before trusting a compile error
that doesn't match your diff:

1. `git branch --show-current` — the checked-out branch can change between turns (another
   session or the user switches it) without you running `git checkout`. Uncommitted
   changes survive a non-conflicting switch, so you can find yourself on the wrong branch
   with your edits still present.
2. `git submodule status Assets/Submodules/Utility` — a leading `+` means the submodule's
   checked-out commit ≠ what the current branch pins. Switching the parent branch does
   **not** re-checkout the submodule (no `submodule.recurse` here), so a branch can end
   up compiled against another branch's newer `Utility`. This produces real-looking
   `CS0104` ambiguous-reference errors (two classes, same short name, two namespaces)
   that are not bugs in your branch. Fix with `git submodule update <path>`, never by
   editing source to route around the ambiguity.

**A `Utility` change lands in two steps: commit and push the submodule, then pin it in the parent**
(bumping first is how #79 happened). `git submodule update` leaves the submodule on a detached HEAD, so
a commit made there belongs to no branch. `dev/bump-utility.sh "<parent commit message>"` does the rest:
it attaches HEAD to a branch named after the parent's (without moving it), pushes it, and commits only
the pin. Run it once the Utility commit is made, not before.
