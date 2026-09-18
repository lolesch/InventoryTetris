/// KNOWN ISSUES:
// comparison shows wrong numbers on the unequipped item
// comparison cant compare against all equipment of same type (rings, 2h vs both weapon slots)

#region TODO
// rework item comparison
// compare rings and weapons to both slots
#endregion TODO

#region PLAYER STATS DETAILS
// hoverable playerStatDisplays => tooltip baseValue and mod sum per mod type

// DPS display
#endregion PLAYER STATS DETAILS

#region COMBAT SIMULATION
// Weapons should have attackSpeed multiply mod
// Weapons should set Damage base value on equip

// editable Dummy CharacterStats

// add skills to deal damage with
// HUD to show skills
#endregion COMBAT SIMULATION

#region CRAFTING SYSTEM
// adjustable affix amount
// adjustable affixes
// adjustable affix values
// adjustable item rarity
// lock affixes
// ...
#endregion CRAFTING SYSTEM

#region CRAFTABLE SKILLS
// base skills have a set of mods
// crafting allows to adjust, add or combine mods
/* sample mods are:
 * damageType
 * delayTime
 * radius
 * projectileAmount
 * ... */
#endregion CRAFTABLE SKILLS

#region ITEM SOCKETS
// implement the simplest version of sockets you can come up with
// design socketables - this goes into attribute design
#endregion ITEM SOCKETS

#region ICEBOX
// Improved MagicFind:
// in D2 => Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

// Add stash tabs
// -> this might require to make each stashTab its own inventory to interact with
// -> add special tabs like in PoE

// add a source to statModifiers to remove all modifiers of that source
#endregion ICEBOX

#region ITEM MOVEMENT
// research if "Handle Item" should instead send the package to the tradeProvider to decide what to do with that item based on the open inventory displays
#endregion ITEM MOVEMENT

#region POLISH
//
#endregion POLISH
