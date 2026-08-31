# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker.

This repo uses the canonical names verbatim, so the mapping is an identity table.
All five labels were verified to exist on `lolesch/InventoryTetris` on 2026-08-31
(`needs-triage`, `needs-info` and `ready-for-human` were created during setup;
`ready-for-agent` and `wontfix` already existed).

| Label in mattpocock/skills | Label in our tracker | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table.

Edit the right-hand column to match whatever vocabulary you actually use. Applying a
label that does not exist on the remote fails rather than creating it — if you change
the right-hand column, create the matching label with `gh label create` first.
