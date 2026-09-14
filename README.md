[**English**](./README.md)  | [**中文简体**](./README-zh.md)

---

# READ ME FIRST / 请先读这里

### This is a MODIFIED VERSION — NOT the original project

**本仓库是魔改版，不是原版。想找原版请去 [xlljc/GunfireDungeon](https://github.com/xlljc/GunfireDungeon)。**

| | Original / 原版 | This repo / 本仓库 |
|---|---|---|
| **Repository** | [xlljc/GunfireDungeon](https://github.com/xlljc/GunfireDungeon) | [AssassinHLQ/**GunfireDungeon-Mod**](https://github.com/AssassinHLQ/GunfireDungeon-Mod) |
| **Title** | GunfireDungeon / 枪火地牢 | **"建筑学院：1999" (Architecture School: 1999)** |
| **Author** | [小李xlxl](https://space.bilibili.com/259437820) | Modified by [klhuyjnvbnvbnb](https://space.bilibili.com/1463614316) |
| **Status** | Upstream, official / 原作者的官方版本 | **Fork + mod / 在原作者基础上魔改** |
| **Dungeon floors** | Single layer loop / 单层 | **9 floors + 1 hidden floor / 9 层 + 1 隐藏层** |
| **Difficulty** | Fixed / 固定 | **Scales with floor / 随层数递增** |
| **Key rebinding** | No / 无 | **Yes, 17 actions / 17 个可改键** |
| **Auto-targeting** | No / 无 | **Yes, locks the nearest enemy / 有，自动锁定最近的敌人** |
| **Pixel font** | Bundled trial fonts | **Ark Pixel Font + Fusion Pixel Font (both SIL OFL 1.1)** |

> **Both are released under [AGPL v3](LICENSE).** All original copyright belongs to the original author.
>
> - **Original author:** 小李xlxl — https://space.bilibili.com/259437820
> - **Modifier:** [klhuyjnvbnvbnb](https://space.bilibili.com/1463614316) (Bilibili) / [AssassinHLQ](https://github.com/AssassinHLQ) (GitHub)
> - **Full list of changes:** [Modifications](#modifications) below, or the **「修改说明」** button on the in-game main menu.

---

# About download sources (please read)

## The author has never published any packaged build

This repository provides **source code only**. There are **no executables, no archives,
no installers** — no Release attachments, no file-hosting links, no `.exe` of any kind.

**Consequently:**

- Any `.exe` / installer / "portable" / cloud-drive download claiming to be this game
  **was NOT published by the author**
- Such files are **outside the author's control** and may contain **trojans, crypto miners,
  ransomware, or credential stealers**
- The author **cannot and never has** inspected, vouched for, or endorsed any third-party build

**The only way to run it right now is to build from source** (see [How to run](#how-to-run) below).

This project **has not been released on any store or platform yet**; if that ever changes,
it will be announced in this repository first.

> ### Disclaimer
>
> The author **provides source code only**. Any third-party build, repack, or redistribution
> is **entirely unrelated to the author**. **The author accepts no liability for any loss
> caused by using executables obtained from unofficial sources.** Verify sources yourself;
> use at your own risk.
>
> If you find someone distributing an untrusted executable under this game's name, please
> report it in [Issues](https://github.com/AssassinHLQ/GunfireDungeon-Mod/issues).

---

Is the main branch not updated for a long time? Please switch to the [develop](https://github.com/xlljc/GunfireDungeon/tree/develop) branch to view the latest code.



## A Dungeon Shooter Game Developed with Godot  

**Godot Version:** `4.7.1 mono` (upgraded from upstream `4.4 mono`)  

**.Net Version:** `9.0`  

---
### Game Definition  

**Game Title:** 《建筑学院：1999》  

**Original Title:** 《枪火地牢 GunfireDungeon》 — this project is a modified version of it  

**English Title:** 《Architecture School: 1999》  

**Art Style:** 2D Pixel  

**Game Tags:** Roguelite, Top-down, Dungeon Exploration, Twin-stick Shooter  

**Reference Games:** 《Enter the Gungeon》, 《Noita》  

**Core Overview:** The game consists of multiple dungeon layers, each composed of several rooms separated by doors. Players must clear all enemies in a room before proceeding to the next. Through exploration, combat, and collecting dropped items and passives, players grow stronger, defeat bosses, and advance to the next layer. This cycle repeats until the final boss is defeated to complete the game.  

**Game Background:** Under development  

**The game includes a powerful built-in map editor, allowing players to create and share their own maps.**  

---
### Preview Images  

##### In-Game  

![gif](GunfireDungeon_Document/文档资源/preview0.png)  
![png](GunfireDungeon_Document/文档资源/preview1.png)  

##### Map Editor  

Room Manager  

![png](GunfireDungeon_Document/文档资源/preview2.png)  

Room Terrain Editing  

![png](GunfireDungeon_Document/文档资源/preview3_gif.gif)  

Room Decoration Editing  

![png](GunfireDungeon_Document/文档资源/preview2_gif.gif)  

Room Preset Editing  

![png](GunfireDungeon_Document/文档资源/preview3.png)  

---
### Running the Project / 如何运行

This repository is **self-contained** — you only need this repo, no need to download the original project.

> **Requires `Godot 4.7.1 .NET (Mono)`**, not the standard build. The C# target framework is `net9.0`.

1. Install **Godot 4.7.1 .NET** — [godotengine.org/download](https://godotengine.org/download) (pick the **.NET** version)
   - The standard (non-.NET) build **will not work** — this project is C#.
2. Install **.NET SDK 9.0 or newer** — [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0)
3. Open `GunfireDungeon_Godot/project.godot` with Godot 4.7.1 .NET.
4. On first open, the editor imports assets and compiles the C# project (this takes a minute).
   - If a prompt about enabling plugins appears, enable them and restart.
   - **Enable these plugins:** `ds_inspector`, `ds_ui`, `game_plugin`, `node_presetting`
5. Press **F5** to run.

**Troubleshooting**

| Symptom | Cause / Fix |
|---|---|
| `Failed to create an autoload, script '.../InitUiManager.cs' is not compiling` | The C# assembly has not been built yet. Let the editor finish compiling, or run `dotnet build GunfireDungeon_Godot/GunfireDungeon.csproj` once, then reopen. |
| `Index p_index = 2 is out of bounds` + `Cannot call method 'add_child' on a null value` | Harmless pre-existing upstream editor warning from the `node_presetting` plugin. Does not affect the game. |
| Project asks to upgrade the format | Already on Godot 4.7 format — no upgrade needed. |

---

### Project Layout / 目录结构

> ├ GunfireDungeon_Document (update logs & related files / 开发日志与相关文件)
>
> └ GunfireDungeon_Godot (Godot project directory / Godot 工程目录)

**Godot download link:** [https://godotengine.org/download](https://godotengine.org/download)  

**.NET download link:** [https://dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0)  

---
### Other  

**Development Log:** [Development Log.md](GunfireDungeon_Document/开发日志.md)  

**Original author (Bilibili):** https://space.bilibili.com/259437820  

**Modifier (Bilibili):** https://space.bilibili.com/1463614316  

**Modifier (GitHub):** https://github.com/AssassinHLQ  

**Project reference plugin:** 

* Ds_Ui：https://github.com/xlljc/Ds_Ui
* godot-node-presetting：https://github.com/DeerLuuu/godot-node-presetting

---

## Modifications

This section lists every change made in this modified version relative to the upstream project, as required by **AGPL v3 Section 5** ("prominent notices stating that You modified it").

The same list is shown **in-game** via the **「修改说明」** button on the main menu.

### 1. Gameplay & Progression

* Added a **floor loop system** — the dungeon now has **10 floors**
* Reaching the exit **advances to the next floor** instead of ending the run immediately
* Victory condition changed to **clearing floor 10**
* Player **HP / shield / weapons / items are preserved across floors**
* **Enemy HP scales with floor number** (+16% per floor)
* A notification pops up when entering a new floor
* Added a **current-floor indicator** at the top of the HUD

### 2. UI & Display

* Game now runs in **fullscreen** with aspect-preserving scaling
* **Main menu font size ×3**
* **Settings panel font size ×4**
* Added a **key binding panel** — 17 rebindable actions
* Key bindings are saved to the save file and persist across restarts
* Added a one-click **"restore default key bindings"** button

### 3. Fonts & UI Assets

* Replaced with **[Ark Pixel Font](https://github.com/TakWolf/ark-pixel-font)** (`ArkPixel-12px-zh_cn.ttf`), licensed under **SIL OFL 1.1** — see `resource/font/ArkPixel-LICENSE.md`
* Ark Pixel 12px only ships 24,471 glyphs and **is missing a number of common characters** (`筑 廊 恐 滚 奖` …) — including `筑` in the game's own title. **[Fusion Pixel Font](https://github.com/TakWolf/fusion-pixel-font)** (`FusionPixel-12px-zh_hans.ttf`, 36,558 glyphs, also 12px, same author, **SIL OFL 1.1**) is therefore bundled as a **fallback font** — see `resource/font/FusionPixel-OFL.txt`
* The two are combined by `resource/font/GameFont.tres` as *base font + fallback*, so the **base font's metrics are unchanged** and missing glyphs are drawn by the fallback — still pixel-perfect, never a system font
* Pixel-font import settings tuned (antialiasing / hinting / subpixel positioning adjusted) so glyphs stay crisp at large sizes
* Font sizes normalized to **integer multiples of the 12px base size**
* **Main menu / pause (ESC) menu / settings menu now use new UI assets** (borders, sliders)
* UI assets by **[Kenney](https://kenney.nl)** (`Fantasy UI Borders`, `UI Pack`), licensed under **CC0 1.0** — see `resource/sprite/ui/fantasyBorder/LICENSE.md`
* **New animated parallax background for the main menu**: a dungeon stone hall with three equally-sized arched openings looking out onto a dusk sky and distant mountains; three parallax layers (sky slowest, mountains faster, stone wall static)
* Text now has a **dark outline** so it stays readable over the brighter background
* **Encyclopedia item names and descriptions enlarged**; long descriptions wrap automatically
* **The pause (ESC) menu gained a stone panel backing**, matching the main menu and settings menu
* **New UI sound effects**: button click, mouse hover, checkbox toggle, slider release
* UI sounds play on the SFX bus, so the in-game "SFX volume" slider controls them
* **Key rebinding now supports mouse buttons** (left / right / middle / side); press **ESC to cancel** a rebind, and the right mouse button can be bound normally
* **"Restore default keys" now takes effect immediately**, no restart required
* Background sky and clouds by **[ansimuz](https://ansimuz.com)** (`Sunny Land 2D Pixel Art Pack`), licensed under **CC0 1.0**
* Background mountains and stone wall are **procedurally generated** by this mod, using a brick palette sampled from the base game's own `TileSet1` — see `resource/sprite/ui/mainBackground/LICENSE.md`
* **UI sound effects by [Kenney](https://kenney.nl)** (`Interface Sounds`), licensed under **CC0 1.0** — see `resource/sound/ui/LICENSE.md`
* **All in-game sound effects replaced with [Kenney](https://kenney.nl) CC0 audio** (`Impact Sounds`, `Sci-Fi Sounds`, `RPG Audio`) — see `resource/sound/LICENSE.md` for the licence and the full mapping table
* Trade-off: **Kenney has no realistic gunshots**, so firearms now use energy-weapon (laser) sounds

### 4. Bug Fixes

* Fixed the settings panel overflowing the screen and spilling content outside its frame
* Fixed errors caused by encyclopedia entries that have no icon configured
* Fixed individual CJK characters missing from the pixel font (shown as tofu boxes); room type names normalized to the `XX房间` format
* Fixed mouse-bound actions (Fire, Roll) being displayed as "not set"
* Fixed mouse clicks doing nothing while rebinding a key
* Fixed pressing ESC to cancel a rebind instead binding ESC to that action (if already mis-bound, press "Restore default keys" once to recover)

### 5. Cleanup

* Removed unused import caches and dead constants

---

> These notes are a record of changes only. **All original copyright belongs to the original author.**

