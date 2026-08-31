---
status: accepted, not yet implemented
---

# The rework order is item split, then transaction, then wallet

Three missing seams sit under almost every unfinished feature: the item is not separable
from the item roll, item movement has no transactional guarantee, and nothing persists.
The item split goes first because the transaction's snapshot needs an immutable rolled
instance to be sound, and because a wallet extracted before the item model would be
rebuilt against it afterwards.

This **reverses the ordering** in the earlier item-movement spec, which had
`ItemTransaction` landing first. That spec's content still stands; only its position
moved.

## Consequences

Persistence is designed but not built. Phase 1 must honour its constraints anyway — an
explicit serialized definition id rather than a Unity asset GUID, and an
instance-to-POCO round trip — because retrofitting serialization onto a grown item model
is far more expensive than honouring the constraint from the start. A reader who finds
save-shaped seams with no save system is looking at that decision.
