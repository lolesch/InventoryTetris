# Do not reorder the `CurrencyType` enum

Enum order and coin *value* order are deliberately allowed to disagree. A previous
reorder — done to make the enum read cheapest-first — silently re-pointed authored
distribution weights and debug buttons, because Unity serializes these by enum value, and
produced two separate live bugs.

Value order belongs in the ladder (ADR-0001), never in the enum's declaration order.
Reordering it is a data migration, not a rename.
