# Field Guide settings

Settings live in `BepInEx\config\com.dev.survivallog.fieldguide.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Changes take effect at the next game start. Delete the file to get the defaults back.

## Controls

| Setting | Default | What it does |
| --- | --- | --- |
| `Hotkey` | `Slash` (`/`) | Opens the page for what the pointer is over (a backpack item, a trader's shelf item, a workbench item, a map location), or opens and closes the window over anything else. Ignored while the pointer is over the window. `None` leaves only the HUD button. |

## Site

| Setting | Default | What it does |
| --- | --- | --- |
| `Source` | `Auto` | Where pages come from. `Auto` reads the offline copy once it is downloaded and the website before that. `Online` always reads the website. `Offline` only reads the downloaded copy and asks you to download it if there is none. |

## Window

| Setting | Default | What it does |
| --- | --- | --- |
| `Layout` | `0.6,0.08,0.36,0.8` | Where the window sits and its size, as fractions of the screen: left, top, width, height. Saved whenever you move or resize the window, so there is no need to edit it by hand. |
| `TextSize` | `80` | How big the site's text is in the window, in percent (50 to 150). The `A-` and `A+` buttons on the title bar change it and save it here. |
