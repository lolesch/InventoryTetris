/// KNOWN ISSUES:

// dragDroping an item sometimes selects an unexpected position

// equipment positions is buggy
// => cant equip 2nd ring via dragDrop
// => 2h wont unequip both slots

// comparison shows wrong numbers of first hover => rehover shows correct values
// comparison cant compare against all equipment of same type (i.e. both rings)

// modified affix range can result in higher min than max values => reorder before setting the range

// item stacking seems broken => random items never stack

/// TODO

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// fix offhand and second ring slot interaction (can equip but can't unequip/select)
// fix equiping 2h + 1h => unequip offhands when equiping a 2H
// compare rings and weapons to both slots

// rework the equipment slot selection
// => go over all slots and check for allowed equipment types. 
// => if multiple found 
//      prefer empty slots for autoEquip
//      compare to all
// => for dropping get the hovered slot and try adding there

// rework all to support AbstractItems instead of AbstractItemObjects => AbstractItemObjects contain an AbstractItem

// hoverable playerStats => tooltip baseValue and mod sum per mod type

// rework comparison

/// COMBAT SIMULATION

// simulate Dummy to take damage
// Weapons should have attackSpeed multiply mod
// Weapons should set Damage base value on equip
// => later add skills to deal damage with

/// ICEBOX

// Sockets? in D2:
// Any weapon except throwing weapons.
// Any body armor.
// Any shields.
// Any headgear.

// Improved MagicFind:
// Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

/// POLISH
