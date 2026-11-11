# Obenseuer Sort By

A BepInEx 6 mod for [Obenseuer](https://store.steampowered.com/app/1554640/Obenseuer/) that adds sort orders and sort hotkeys.

## Features

- **Sort by menu**: the Sort buttons (your inventory and any open storage: bank, backpack, containers) open a small menu:
  - Category (the game's own order)
  - Name
  - Value (each) / Value (stack = price x amount)
  - Amount
  - Quality
  - Freshness
  - Reverse order
  Pick one and it sorts right away; the choice is remembered and used by the hotkeys.
- **Hotkeys** (while the inventory is open):
  - `U`: sort your inventory
  - `Y`: sort the storage under the mouse (your inventory, the bank, the backpack, a container...)
- Equipment slots are never sorted. Sorting uses the game's own routine, so stacks, liquids and item data are kept.

## Configuration

`BepInEx/config/com.juanberta.obenseuer.sortby.cfg`

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| Sorting | SortBy | Category | Current order |
| Sorting | Reverse | false | Reverse the order |
| Sorting | SortButtonOpensMenu | true | Off: Sort buttons sort right away with the current order |
| Keys | SortInventoryKey | U | Sort your inventory |
| Keys | SortHoveredKey | Y | Sort the storage under the mouse |

## Install

Requires BepInEx 6 (x64). Put `Obenseuer-SortBy.dll` in `BepInEx/plugins/`.

## Building

`dotnet build -c Release` (references the game install at `C:\Program Files (x86)\Steam\steamapps\common\Obenseuer`; override with `/p:ObenseuerDir=...`).

## License

MIT
