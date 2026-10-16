# Diablo II itemization and belt: research (#299)

Parent: #293 (planning map). Tracks into #300 and #303.

**Edition caveat.** Most sources are Lord of Destruction (1.10+) community wikis, plus Diablo II Resurrected (D2R) forum and wiki pages. Where a fact is D2R-only it is marked. Several wikis (PureDiablo, Diablo II Wiki, Breezewiki, Arreat Summit and Lurker Lounge) returned HTTP 403, 409 or Anubis block pages to the fetcher. Claims that come only from search-result snippets are marked **(snippet)**. The Blizzard data tables (`belts.txt`, `MagicPrefix.txt`, `MagicSuffix.txt`, `ItemTypes.txt`) were not retrieved; see Gaps.

## Answers in brief

1. **Quality.** Quality (magic, rare, set, unique, crafted) decides how many affixes an item rolls and whether its stats are fixed. Uniques and sets are fixed-stat items with very low drop rates, which is why they feel special.
2. **Gating.** Item level is generated from the character level. Quality level is fixed per base. Effective item level is the larger of the two. Affix level (alvl) is then computed from it, and only affixes with a level at or below alvl can roll. The vanilla formula is in section 2.
3. **Belt.** Every belt is 4 columns wide. Base is one row (4 slots). Total boxes: Sash 8, Light Belt 8, Belt 12, Heavy Belt 12, Plated Belt 16. Potions are hotkeyed to keys 1 to 4. A column takes the potion type it is first filled with. Drinking drops the rows above down. Picking up potions restocks the belt. Healing and mana potions fill over time. Rejuvenation restores a percentage instantly.

## 1. Item quality

Two separate axes exist. Do not confuse them.

**Tier (base level group).** The gambling screen calls these Normal, Exceptional and Elite. They change the base item and its requirements, not the affix roll. Exceptional and Elite chances depend on how far the character level sits above the item's quality level. Source: [Battle.net Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml).

**Quality (rolled or fixed affixes).** Named in the table below.

| Quality | Name colour | Affixes | Drop rate (gambling screen) | Source |
|---|---|---|---|---|
| Magic | Blue | At most 1 prefix and 1 suffix | About 89.85% of gambled items | [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml); affix cap **(snippet)** via [PureDiablo Item quality](https://www.purediablo.com/d2wiki/Item_quality) |
| Rare | Yellow | Up to 3 prefixes and 3 suffixes; a rare cannot repeat a family **(snippet)** | About 1 in 10 | [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml); affix cap **(snippet)** via [Breezewiki Rare Items](https://breezewiki.discard.no/diablo/wiki/Rare_Items) (page itself blocked) |
| Set | Green | Preset stats; bonuses grow when more pieces of the set are worn **(snippet)** | About 1 in 1,000 | [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml); [PureDiablo Set](https://www.purediablo.com/d2wiki/Set) **(snippet)** |
| Unique | Gold | Fixed affixes; usually one unique per base type **(snippet)** | About 1 in 2,000 | [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml) |
| Crafted | Orange | 1 or more fixed properties plus 1 to 4 random affixes (count depends on ilvl band); original affixes are removed | Crafting only | [Battle.net Crafted Items](https://classic.battle.net/DIABLO2EXP/ITEMS/crafteditems.shtml) |

Notes:

- The gambling page gives the magic, rare, set and unique drop rates. Magic items are the bulk of drops. Sources: [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml).
- The Inferior and Superior quality labels that the task brief asked about are not covered by any source I could read. Treat them as unverified.
- Sockets (for runewords and white-base sockets) are not covered in a fetched primary source. The Breezewiki snippet says a rare can have a single socket **(snippet)**. A runeword base must be a plain item: not magic, rare, set or unique. This comes from a guide snippet **(snippet)** via [DiabloBytes runeword guide](https://www.diablobytes.com/d2-resurrected/guides/runeword-guide/).

**Why uniques feel unique.**

- Stats are fixed rather than rolled. Set and unique pieces come with preset modifiers, although the values can fluctuate slightly **(snippet)** via [PureDiablo Item quality](https://www.purediablo.com/d2wiki/Item_quality).
- Drop rate is roughly 1 in 2,000 on the gambling screen, against about 1 in 10 for rares. Source: [Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml).
- There is usually one unique per base type **(snippet)**. The name is unique to the base, so the player can recognise the item at a glance.
- Build-enabling effects (for example a unique that unlocks a skill mechanic) are a design observation of this game. No source here documents them, so I have not included a claim about them.

## 2. How ilvl, qlvl, alvl and magic level gate affixes

### Inputs

- **Item level (ilvl).** For gambled items, ilvl is generated from the character level, from -5 to +4, with a minimum of 5. It is never shown to the player and is used only inside the affix formulas. Source: [Battle.net Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml).
- **Quality level (qlvl).** Fixed per base item. Examples from the gambling page: Ancient Armor is 40, Diadem is 85. A high qlvl lets an item roll higher-level affixes even on a low-level character. Source: [Battle.net Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml).
- **Effective ilvl.** If qlvl is higher than ilvl, the item uses qlvl instead. Source: [Battle.net Crafted Items](https://classic.battle.net/DIABLO2EXP/ITEMS/crafteditems.shtml) (crafted path); the same rule is stated for Project Diablo 2 **(mod)** in [Project Diablo 2 Item Affixes](https://wiki.projectdiablo2.com/wiki/Item_Affixes).

### Affix level (alvl) formula

Vanilla rule, as stated on the Battle.net crafted-items page:

```
cap = 99 - floor(qlvl / 2)
if ilvl < cap:   alvl = ilvl - floor(qlvl / 2)
else:            alvl = 2 * ilvl - 99
```

A prefix or suffix can spawn only if its required level is at or below alvl. Source: [Battle.net Crafted Items](https://classic.battle.net/DIABLO2EXP/ITEMS/crafteditems.shtml).

Crafted ilvl comes first: `ilvl = floor(0.5 * crafter_clvl) + floor(0.5 * input_ilvl)`, capped at 99, then raised to qlvl if qlvl is higher. Source: same page.

### Worked examples

Example A, from the Battle.net page itself. A crafter at clvl 78 uses a base with ilvl 85 and qlvl 86.

- ilvl = floor(0.5 * 78) + floor(0.5 * 85) = 39 + 42 = 81.
- Effective ilvl = max(81, 86) = 86.
- Cap = 99 - floor(86 / 2) = 99 - 43 = 56. Since 86 is not below 56, alvl = 2 * 86 - 99 = **73**.

Source: [Battle.net Crafted Items](https://classic.battle.net/DIABLO2EXP/ITEMS/crafteditems.shtml).

Example B, my own arithmetic with the same formula. An Ancient Armor base (qlvl 40) at ilvl 60.

- Cap = 99 - 20 = 79. 60 is below 79, so alvl = 60 - 20 = **40**.
- Any affix with a required level up to 40 can spawn on it.

Example B is derived from the formula above, not from a source example.

### Magic level (maglvl), mod only

Project Diablo 2 **(mod)** adds a magic level for non-elite wands, staves, orbs and circlets. Its formula is `alvl = ilvl + maglvl` when maglvl is greater than 0. The example it gives: a tiara at ilvl 77 with maglvl 13 gives alvl 90. Source: [Project Diablo 2 Item Affixes](https://wiki.projectdiablo2.com/wiki/Item_Affixes).

The same page says diadems always have alvl 99, but the magic-level formula does not produce that for ilvl above 81. I did not resolve this. Vanilla Diablo II does not show a magic level in any source I could read. Do not port maglvl to the vanilla design without checking.

### Arreat Summit numbers

Forum users say the parenthesised numbers on Arreat Summit are the minimum alvl needed to spawn an affix, not a character level. Other posters call the site inaccurate. Both claims are dated and come from Lurker Lounge threads I could not fetch; they are cited from the search snippets **(snippet)** via [Lurker Lounge thread 8190](https://lurkerlounge.com/forums/printthread.php?tid=8190) and [Lurker Lounge thread 11851](https://lurkerlounge.com/forums/printthread.php?tid=11851). Do not rely on Arreat numbers without checking the data tables.

### Value ranges

Each affix rolls a value inside a min-max range that depends on its own row in the affix tables. I did not retrieve `MagicPrefix.txt`, `MagicSuffix.txt` or `ItemTypes.txt`, so I cannot give the per-affix ranges. This is the single largest gap for #300.

## 3. The belt

### Grid per belt

All belts are 4 columns wide. Source: [PlanetDiablo Armor guide (archive)](https://gamespy-archives.quaddicted.com/sites/www.planetdiablo.com/diablo2/items/guide/armor/index.html).

Total box counts, from the fextralife Belts page:

| Base belt | Boxes | Implied rows x cols | Source |
|---|---|---|---|
| Sash | 8 | 2 x 4 | [Fextralife Belts](https://diablo2.wiki.fextralife.com/Belts) |
| Light Belt | 8 | 2 x 4 | same |
| Belt | 12 | 3 x 4 | same |
| Heavy Belt | 12 | 3 x 4 | same; also [Breezewiki Heavy Belt](https://breezewiki.discard.no/diablo/wiki/Heavy_Belt) **(snippet)**, 12 boxes |
| Plated Belt | 16 | 4 x 4 | same |

Unique and elite belts for cross-check **(snippet)**: Lenymo (sash) 8, Snakecord (light) 8, Nightsmoke (belt) 12, Goldwrap (heavy) 12, Bladebuckle (plated) 16, Spiderweb Sash (elite) 16. Source: [Unique Belts](https://diablo2.diablowiki.net/Unique_Belts) and [Belts - Value/usage guide](https://us.forums.blizzard.com/en/d2r/t/belts-valueusage-guide-for-begginers/128567).

**Conflict.** The PlanetDiablo archive says a sash adds no rows and other belts add up to three rows. That contradicts the fextralife counts (sash 8 boxes). The fextralife counts agree with the cross-checks above, so I prefer them. Confirm against `belts.txt`.

### Potion-to-column assignment

No primary source documents a fixed type-to-column table. The mechanism described across sources:

- A column takes the potion type it is first filled with. The D2R forum thread quotes this as the column "learning" its type **(D2R)**. Source: [Blizzard forum, Potion Tips](https://us.forums.blizzard.com/en/d2r/t/potion-tips-no-moar-juggling/19385).
- The default layout described in that thread is column 1 health, column 2 mana, column 3 rejuvenation, column 4 portal tome. The same thread later reports column 4 points to town scrolls, and that only the controller path works **(D2R, unconfirmed)**.
- Shift-click or shift-right-click moves a potion from inventory into a matching column when there is open belt space. The sources disagree on which click. PureDiablo says Shift+Right-click **(D2R wiki)**; Breezewiki says Shift+Left-click **(snippet)**. Source: [PureDiablo Potion](https://www.purediablo.com/d2wiki/Potion) (blocked, cited from its summary via [Potions Introduction](https://www.purediablo.com/?p=4503)).

### Refill

- Picking up potions restocks the belt. Potions auto-stack left to right when you pick them up without the inventory open **(snippet)**. Source: [Potions Introduction](https://www.purediablo.com/?p=4503).
- Drinking a potion drops the potions above it down, keeping the bottom row full. Source: [PlanetDiablo Armor guide](https://gamespy-archives.quaddicted.com/sites/www.planetdiablo.com/diablo2/items/guide/armor/index.html) and [Potions Introduction](https://www.purediablo.com/?p=4503).
- Refill from inventory into an empty column (the "ethereal placeholder") is **D2R, controller only, unconfirmed**. Other posters could not reproduce it. Source: [Blizzard forum, Potion Tips](https://us.forums.blizzard.com/en/d2r/t/potion-tips-no-moar-juggling/19385).
- Vendor shortcut: Shift+Right-click at a vendor fills the belt with that potion type. Source: [Potions Introduction](https://www.purediablo.com/?p=4503).

### Hotkeys

Potions are hotkeyed to the non-keypad number keys 1 to 4. With more than one row, only the top row shows unless the cursor hovers over the belt; the tilde key keeps the rows expanded. Source: [PlanetDiablo Armor guide](https://gamespy-archives.quaddicted.com/sites/www.planetdiablo.com/diablo2/items/guide/armor/index.html). Hotkey 1-4 also appears in **(snippet)** [PureDiablo Potion](https://www.purediablo.com/d2wiki/Potion).

### Potion kinds

- **Kinds:** healing, mana, rejuvenation, full rejuvenation, antidote, stamina, thawing; throwing potions are also weapons. Source: [Potions Introduction](https://www.purediablo.com/?p=4503).
- **Tiers:** healing and mana each come in Minor, Light, standard, Greater and Super. Mixing a small potion with a large one slows the fill rate. Source: [Potions Introduction](https://www.purediablo.com/?p=4503).
- **Healing and mana (over time).** They fill gradually over several seconds, averaging their value over that time. They are subject to a double-fill chance. Source: [Potions Introduction](https://www.purediablo.com/?p=4503).
- **Rejuvenation (instant, percentage).** Restores 35% of life and 35% of mana instantly. Not affected by the double-fill chance. D2R wiki. Sources: [Fextralife Rejuvenation Potion](https://diablo2.wiki.fextralife.com/Rejuvenation+Potion) (D2R) and [Potions Introduction](https://www.purediablo.com/?p=4503).
- **Healing potion (D2R numbers).** Replenishes life over time. Per-class amounts 100 (necromancer, sorceress, druid), 150 (amazon, paladin, assassin), 200 (barbarian). Source: [Fextralife Healing Potion](https://diablo2.wiki.fextralife.com/Healing+Potion), D2R.
- **Cube.** Three healing potions transmute into one greater healing potion. Source: [Fextralife Healing Potion](https://diablo2.wiki.fextralife.com/Healing+Potion).

## Gaps and conflicts

1. **Data tables not retrieved.** `belts.txt`, `MagicPrefix.txt`, `MagicSuffix.txt` and `ItemTypes.txt` were not reachable. Searches found no mirror. Per-affix value ranges and the exact belt box counts still need the tables.
2. **Belt rows conflict.** PlanetDiablo (sash adds no rows) versus Fextralife (sash 8 boxes). Fextralife is preferred because it agrees with the unique-belt counts.
3. **Potion column behaviour is D2R and controller-only.** Keyboard behaviour for LoD is unconfirmed.
4. **Magic level is mod-only.** Project Diablo 2 **(mod)**. Vanilla is unconfirmed.
5. **Diadem alvl 99 claim (PD2) does not match its own maglvl formula.** Unresolved.
6. **Inferior and Superior labels** are not sourced.
7. **Blocked wikis.** PureDiablo (Potion, Belt, Item quality, Alvl), Breezewiki (Belts, Belt slot, Rare Items, Heavy Belt), Diablo II wiki (Sash, Heavy Belt, Healing Potion) and Lurker Lounge returned 403, 409 or Anubis block pages. Claims from them are marked as snippets or summaries.

## Sources

Primary (Blizzard, classic.battle.net):

- [Diablo II: Lord of Destruction, Gambling](https://classic.battle.net/diablo2exp/basics/gambling.shtml)
- [Diablo II: Lord of Destruction, Crafted Items](https://classic.battle.net/DIABLO2EXP/ITEMS/crafteditems.shtml)

Community and wiki (fetched or partially fetched):

- [PlanetDiablo Armor guide (archive)](https://gamespy-archives.quaddicted.com/sites/www.planetdiablo.com/diablo2/items/guide/armor/index.html)
- [Potions Introduction (PureDiablo)](https://www.purediablo.com/?p=4503)
- [Fextralife Belts](https://diablo2.wiki.fextralife.com/Belts)
- [Fextralife Healing Potion (D2R)](https://diablo2.wiki.fextralife.com/Healing+Potion)
- [Fextralife Rejuvenation Potion (D2R)](https://diablo2.wiki.fextralife.com/Rejuvenation+Potion)
- [Project Diablo 2 Item Affixes (mod)](https://wiki.projectdiablo2.com/wiki/Item_Affixes)
- [Blizzard forum, Belts guide (D2R)](https://us.forums.blizzard.com/en/d2r/t/belts-valueusage-guide-for-begginers/128567)
- [Blizzard forum, Potion Tips (D2R)](https://us.forums.blizzard.com/en/d2r/t/potion-tips-no-moar-juggling/19385)

Search snippets only (page not fetched):

- [PureDiablo Item quality](https://www.purediablo.com/d2wiki/Item_quality)
- [PureDiablo Set](https://www.purediablo.com/d2wiki/Set)
- [PureDiablo Potion](https://www.purediablo.com/d2wiki/Potion)
- [Breezewiki Rare Items](https://breezewiki.discard.no/diablo/wiki/Rare_Items)
- [Breezewiki Heavy Belt](https://breezewiki.discard.no/diablo/wiki/Heavy_Belt)
- [Unique Belts (diablo2.diablowiki.net)](https://diablo2.diablowiki.net/Unique_Belts)
- [DiabloBytes runeword guide](https://www.diablobytes.com/d2-resurrected/guides/runeword-guide/)
- [Lurker Lounge thread 8190](https://lurkerlounge.com/forums/printthread.php?tid=8190)
- [Lurker Lounge thread 11851](https://lurkerlounge.com/forums/printthread.php?tid=11851)
