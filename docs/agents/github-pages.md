# GitHub Pages

The site at <https://lolesch.github.io/InventoryTetris/> builds from the `GitPage` branch, path `/docs`.

Leave `docs/agents/` and `docs/adr/` out of a `main` -> `GitPage` merge. `exclude:` in
`docs/_config.yml` (byte-identical on both branches) is the backstop that stops Jekyll publishing them.
