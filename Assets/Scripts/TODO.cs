/// TODO

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// fix offhand and second ring slot interaction (can equip but can't unequip/select)

// fix equiping 2h + 1h => unequip offhands when equiping a 2H

// rework all to support AbstractItems instead of AbstractItemObjects => AbstractItemObjects contain an AbstractItem

// compare rings and weapons to both slots

/// POLISH

// Sockets? in D2:
// Any weapon except throwing weapons.
// Any body armor.
// Any shields.
// Any headgear.

// Improved MagicFind:
// Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

/// KNOWN ISSUES:

// modified affix range can result in higher min than max values => reorder before setting the range
// comparison color seems off - eaqual values are highlighted => reenable the red color for lower values and doublecheck the results
// rework the equipment slot selection
// => go over all slots and check for allowed equipment types. 
// => if multiple found 
//      prefer empty slots for autoEquip
//      compare to all
// => for dropping get the hovered slot and try adding there

/// COMBAT SIMULATION

// Dummy to take damage
// damage calculation based on equipment
// add damage types and resistances
// => later add skills to deal damage with
