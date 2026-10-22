#!/usr/bin/env bash
# Fails when a change to the Utility pin points at a commit that is not on Utility's main.
#
#   dev/check-utility-pin.sh [base-ref [head-ref]]      defaults: origin/main, HEAD
#
# A pin on an unmerged Utility branch resolves today and dangles once that branch is deleted or its PR is
# squashed. Only a pin that differs from base-ref is checked, so an old dangling pin does not fail every PR.
# CI runs this on each PR (.github/workflows/utility-pin.yml). Merge the Utility PR first, with a merge commit
# (a squash makes a new commit and the pin stops matching), then re-pin with dev/bump-utility.sh.
set -eu

base="${1:-origin/main}"
head="${2:-HEAD}"
sub="Assets/Submodules/Utility"

pin="$(git rev-parse "$head:$sub")"
base_pin="$(git rev-parse "$base:$sub" 2>/dev/null || true)"
if [ "$pin" = "$base_pin" ]; then
  echo "Utility pin unchanged from $base ($pin); nothing to check."
  exit 0
fi

# A CI submodule checkout is shallow: deepen it so the ancestry test sees Utility's history.
git -C "$sub" fetch --quiet --no-tags --unshallow origin main 2>/dev/null \
  || git -C "$sub" fetch --quiet --no-tags origin main

if git -C "$sub" merge-base --is-ancestor "$pin" origin/main; then
  echo "Utility pin $pin is on Utility main."
  exit 0
fi

echo "Utility pin $pin is not on Utility's main." >&2
echo "Merge the Utility PR first (merge commit, not squash), then re-pin with dev/bump-utility.sh." >&2
exit 1
