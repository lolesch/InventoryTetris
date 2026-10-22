#!/usr/bin/env bash
# Publishes the Utility submodule's HEAD and pins it in the parent, in the order that keeps the pin
# resolvable: push the submodule commit first, then commit the pin.
#
#   dev/bump-utility.sh "<parent commit message>" [submodule-branch]
#
# Commit the Utility change inside Assets/Submodules/Utility first. A submodule checkout is detached
# after `git submodule update`; this attaches HEAD to <submodule-branch> (default: the parent's current
# branch name) without moving it, pushes that branch, then commits only the submodule pin in the parent.
# Anything else staged in the parent stays staged.
set -eu

[ $# -ge 1 ] || { echo "usage: dev/bump-utility.sh \"<commit message>\" [submodule-branch]" >&2; exit 2; }
message="$1"
root="$(git rev-parse --show-toplevel)"
sub="Assets/Submodules/Utility"
branch="${2:-$(git -C "$root" branch --show-current)}"

if [ -n "$(git -C "$root/$sub" status --porcelain --untracked-files=no)" ]; then
  echo "Utility has uncommitted changes; commit them inside $sub first." >&2
  exit 1
fi

current="$(git -C "$root/$sub" branch --show-current)"
if [ -z "$current" ]; then
  git -C "$root/$sub" checkout -q -B "$branch"
  current="$branch"
fi

git -C "$root/$sub" push -u origin "$current"

if [ -z "$(git -C "$root" status --porcelain -- "$sub")" ]; then
  echo "The parent already pins Utility at $(git -C "$root/$sub" rev-parse --short HEAD); nothing to commit."
  exit 0
fi

git -C "$root" commit -m "$message" -- "$sub"
git -C "$root" log --oneline -1
