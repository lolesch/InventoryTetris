

///
// fix the item drop positioning

// implement item rarity => and according display coloring



/// Loot Generation:
/* when there is a loot drop event (enemy died, destructable destroyed, chest opened, etc)
 * calculate an amount of loot to drop
 * this could be based on character level, enemy level, area level...
 * ...
 * each loot is generated on the fly:
 * 
 * LOOT LEVEL
 * => match the loot to the current character progression
 * 
 * RARITY / QUALITY
 * => each rarity has its drop chance
 * get a random number between 0 and the sum of all rarity chances
 * return the highest rarity thats chance <= to the random roll
 * 
 * now that we know the items rarity we can define derived values
 * 
 * LOOT TYPE
 * => this defines if it is equipment, consumable, skills etc
 * each type has its own table of modifiers
 * and each type of type (i.e. a weapon) excludes modifiers from that table
 * => smart loot
 * 
 * MODIFIER
 * => each rarity has its own table of modifiers to pick from
 * => lower rarity have higher numers but less modifiers and vise versa
 * break things down to keep modifiers impactfull
 * noone needs +1% block chance...
 * 
 * MIN MAX RANDOM
 * random roll within the modifiers min max values
 * 
 * REQUIREMENTS / ITEM VALUE
 * => these are derived values from the random modifiers
*/

