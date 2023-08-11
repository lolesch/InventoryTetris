// TODO

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// rework all to support AbstractItems instead of AbstractItemObjects => AbstractItemObjects contain an AbstractItem

// keep item preview within the screen
// -> anchor the preview based on the cursor relative position to the screen center so that it always points towards the center
// -> the item comparison should follow these rulez

// preview possible item stat ranges

/// known Issues:

// modified affix range can result in higher min than max values => reorder before setting the range
// comparison color seems off - eaqual values are highlighted => reenable the red color for lower values and doublecheck the results
// rework the equipment slot selection
// => go over all slots and check for allowed equipment types. 
// => if multiple found 
//      prefer empty slots for autoEquip
//      compare to all
// => for dropping get the hovered slot and try adding there
