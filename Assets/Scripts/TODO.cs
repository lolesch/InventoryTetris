/// KNOWN ISSUES:

// Droping an item selects unexpected positions

// eqiping 2h wont unequip both slots

// comparison shows wrong numbers of first hover => rehover shows correct values
// comparison cant compare against all equipment of same type (i.e. both rings)

// modified affix range can result in higher min than max values => reorder before setting the range

// item stacking seems broken => random items never stack

/// TODO

// fix equiping 2h => unequip offhands when equiping a 2H
// extend TryStackOrSwap in AbstractDimensionalContainer OR implement overrides for CanAddAtPosition or AddAtPosition in the CharacterEquipment

// fix characterStats modifier list => hovering items (previewDisplay) adds modifiers without equipping and never removes them

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// compare rings and weapons to both slots

// hoverable playerStats => tooltip baseValue and mod sum per mod type

// rework comparison

/// COMBAT SIMULATION

// simulate Dummy to take damage
// Weapons should have attackSpeed multiply mod
// Weapons should set Damage base value on equip

// editable Dummy CharacterStats

// lokalPlayer take damage
// HUD to show health

// add skills to deal damage with
// HUD to show skills
// HUD to show Resource

/// ICEBOX

// Sockets? in D2:
// Any weapon except throwing weapons.
// Any body armor.
// Any shields.
// Any headgear.

// Improved MagicFind:
// Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

/// POLISH

///GUSTAV
/*
          2 = 2     1*2 = 2        
        2+2 = 4                  4 = 4
      2+2+2 = 6                
    2+2+2+2 = 8                4+4 = 8
  2+2+2+2+2 = 10             
2+2+2+2+2+2 = 12             4+4+4 = 12

3, 6, 9, 12, 15, 18, 21, 24 */