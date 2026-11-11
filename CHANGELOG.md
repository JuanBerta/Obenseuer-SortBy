# Changelog

## 1.0.2
- Sorting a big storage (a multi-page bank) no longer freezes the game: storages with 100+ slots use a faster routine that places each stack once (same order and stack merging as the game). Config: [Sorting] FastSortFromSlots.

## 1.0.1
- Fixed NullReferenceException spam from the game's inventory navigation while the Sort by menu was open.

## 1.0.0
- The Sort buttons of your inventory and of any storage (bank, backpack, containers) open a **Sort by** menu: Category (the game's order), Name, Value (each), Value (stack), Amount, Quality, Freshness, plus Reverse order. The choice is remembered.
- Hotkeys while the inventory is open: **U** sorts your inventory, **Y** sorts the storage under the mouse.
