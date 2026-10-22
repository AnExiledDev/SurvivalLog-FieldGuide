# Field Guide

A BepInEx mod for Survival Log. Read the [Survival Log Database](https://sldb.core-directive.com/en/) inside the game: items, crafting, maps and more, in a window you can put anywhere.

## What it does

- **Look things up with `/`:** hover an item in your backpack or on a trader's shelf, or a location on the map, and press `/` to open its page. Over nothing, `/` opens or closes the window.
- **A window you place yourself:** drag it by its title bar and resize it from any edge or corner. It opens where you left it, at the size you left it. `A-` and `A+` on the title bar make the text smaller or larger.
- **Book button on the HUD:** a book button in the bottom-left tool column opens and closes the window.
- **Phone app:** the phone's home screen gets a Field Guide app that shows the site's phone layout. `Back` closes it.
- **Offline copy:** the title bar offers `Download for offline` (about 150 MB). Once downloaded, the guide reads the copy on your PC, no internet needed, and offers an update when the site has a newer one.
- **Follows the game's language:** English or Chinese.

While the pointer is over the window or the phone app is open, typing goes to the guide (for the site's search) and the game's hotkeys wait.

## Requirements

- Survival Log 1.1.19131. A game update can break it until the mod is updated.
- [BepInEx for Survival Log](https://www.nexusmods.com/survivallog/mods/12). Install it first.
- An internet connection to read the site online or to download the offline copy.

## Install

1. Install BepInEx. If no other mods were installed before, start the game once so BepInEx finishes setup, then quit.
2. Download `FieldGuide-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-FieldGuide/releases/latest) and extract it into the game folder (the folder with `SurvivalLog.exe`), merging the folders. `FieldGuide.dll` ends up in `BepInEx\plugins\`.

## Uninstall

Delete `FieldGuide.dll` from `BepInEx\plugins`. The offline copy is in `BepInEx\FieldGuide`; delete that folder too to free the space.

## Configuration

See [CONFIG.md](CONFIG.md).

## Troubleshooting

The mod writes `BepInEx\field-guide.log`. A working install logs `===== Field Guide 0.1.1 started` when the game starts. If the book button never shows, check that line and look for errors in `BepInEx\LogOutput.log`. A failed download says why in the same log; press the button again to retry.

## Feedback

Found a bug or have an idea? Come say hi on the [Core Directive Discord](https://discord.gg/trTsyxUFqg). For bugs, please include `BepInEx\LogOutput.log` and `BepInEx\field-guide.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

## Build

```
dotnet build src/FieldGuide -c Release -p:GameDir="C:/path/to/Survival Log"
dotnet test tests/FieldGuide.Tests
```

## License

MIT, by AnExiledDev. See [LICENSE](LICENSE).
