# Main Menu smoke-test fixes — draft heroes, Start Game / Continue, hero identity displays

Date: 2026-10-13
Status: Built on `fix/menu-smoke-test`. Settled in conversation after a smoke test of the Changelog
epic's last commit (`7390dd3`) against `2026-10-12-front-end-flow-design.md` and
`2026-10-08-changelog-design.md`. This spec records the decisions and the statements of the
front-end spec they overturn.

## Problem Statement

Playing the Main Menu showed six things the specs either promised and the build did not deliver, or never
asked for:

1. A first Editor launch still created a hero called "Hero" before the player named one.
2. Play and Delete showed without a hero, and Play said "Play" for a hero that had never entered the game.
3. The selected hero's name, level and class showed nowhere (the front-end spec promised all three).
4. The Changelog panel ignored Cancel, and its text overflowed the panel.
5. Equipping gear while the sim was paused left the health and resource bars on their old maximum.

## Decisions

**No hero is created at launch.** `GameBoot.Boot` continues the last hero and creates none, in the Editor
and in a build. Only a Start Fresh Play (the Editor's `Start Fresh Each Play` toggle, over an empty
in-memory store by design) still creates "Hero". The Editor makes a saved hero from *Tools > Hero > New
Saved Hero*. This replaces the front-end spec's "the Editor keeps `LoadLastOrCreate`".

**A new hero is a Draft until the game starts.** Naming a hero on the Main Menu makes a **Draft**: a full
hero held in memory by the `HeroSaveService`, shown as the last hero slot (the create slot follows it),
loaded into the Session so the showcase shows it, but with no file, no entry in `List()` and nothing in the
Account. **Start Game** writes its first save (`SaveDraft`) and then enters it like any hero. Closing the
game, deleting the draft or naming another hero discards it; there is at most one. `Save()` refuses a draft
(`NotFromSave`), so the quit and Save & Exit paths can never turn an unstarted hero into a save. A first
write that fails keeps the player on the menu with the draft intact. Because a hero with a file is one the
player has started, **Continue** means the save exists, and no flag is stored. This replaces "the create
slot creates, loads and selects the new hero" writing the file at once.

**Play and Delete are shown only with something to act on.** Play while a hero that reads is selected,
Delete while any hero (readable or not) is selected; otherwise the menu offers the hero slots alone. This
replaces "disabled / not interactable". The Play button reads "Start Game" for a Draft and "Continue"
otherwise, which overturns the 2026-10-12 amendment that dropped the Continue button: it is one button with
two labels, not two buttons.

**Hero identity displays.** `HeroNameDisplay`, `HeroLevelDisplay` and `HeroClassDisplay` share
`HeroIdentityDisplay`, which follows the Session's hero like `ResourceDisplay` does (hero loads, hero list
changes, stat changes for a level-up) and reads one `HeroIdentity` value. A screen that wants one drops the
component in and wires a label. The Main Menu shows all three above the showcase text.

**The Changelog is a Modal layer.** It registers with the Cancel chain at the Modal rank and closes like its
Close button; a closed panel leaves the press to the layers below. Each tab body sits in a masked
`ScrollRect` viewport.

**Resource displays follow the maximum.** `ResourceDisplay` also refreshes when the resource's total
changes. A raised maximum leaves the current value where it was, so `CurrentHasChanged` never fired; only
the regeneration of a running sim hid it.

## Out of Scope

- A scrollbar graphic on the Changelog (the mouse wheel and drag scroll it).
- Telling the player on the menu why a first save failed; the failure is logged and the draft stays.
- Placing the identity displays in the game scene; the components are ready for it.
- A Settings panel and its Cancel layer (#193, #260).
