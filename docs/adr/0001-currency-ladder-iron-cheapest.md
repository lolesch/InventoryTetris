# The coin ladder is 5 / 12 / 20, with iron as the cheapest denomination

Coins are iron -5-> copper -12-> silver -20-> gold, so gold stays worth exactly 1200
base units and **no item price needed retuning** when the ladder changed — the ratios
multiply, and `5 x 12 x 20 == 20 x 12 x 5`. The structure is English pound-shilling-pence
(12 pence to a shilling, 20 shillings to a pound), picked because it is a real ladder
people already have intuitions about.

Iron sits *below* copper, reversing the previous order. Iron is ~5% of the Earth's crust
against copper's ~0.007%, and was almost never coined: cheap, heavy, brittle when cast,
and it rusts away the impression. Copper was genuine coinage from the 3rd century BCE.
It also makes the top three tiers the medal podium (bronze < silver < gold), and gives
iron a reason to be the trash coin.

## Consequences

Icons were left alone — grey iron is still iron, orange copper still copper. Only the
value mapping moved. See ADR-0003 for the enum-order constraint this interacts with.
