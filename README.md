[**English**](./README.md)  | [**中文简体**](./README-zh.md)

> ## ⚠️ This is a Modified Version / 本仓库为修改版
>
> This repository is a **modified version** of [**xlljc/GunfireDungeon**](https://github.com/xlljc/GunfireDungeon).
> The original project and this modified version are **both released under the [AGPL v3](LICENSE)** license. All original copyright belongs to the original author.
>
> - **Original author:** 小李xlxl — https://space.bilibili.com/259437820
> - **Modifier:** klhuyjnvbnvbnb (Bilibili) / [AssassinHLQ](https://github.com/AssassinHLQ) (GitHub)
> - **See [Modifications](#modifications) below for the full list of changes.**

Is the main branch not updated for a long time? Please switch to the [develop](https://github.com/xlljc/GunfireDungeon/tree/develop) branch to view the latest code.



## A Dungeon Shooter Game Developed with Godot  

**Godot Version:** `4.4 mono`  

**.Net Version:** `9.0`  

---
### Game Definition  

**Game Title:** 《枪火地牢》  

**English Title:** 《Gunfire Dungeon》  

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
### Launching the Project  

The directory structure of the git repository is as follows:  
> ├ GunfireDungeon_Document (Directory for update logs and related files) 
>
> └ GunfireDungeon_Godot (Godot project directory)  

Ensure `.NET 9` and `Godot Mono 4.4` are installed.  

Godot download link: [https://godotengine.org/download](GunfireDungeon_Document/文档资源/setting.png)  

.NET 9 download link: [https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0](GunfireDungeon_Document/文档资源/setting.png)  

Open `GunfireDungeon_Godot/project.godot` using Godot Mono.  

On first launch, enable these two plugins:  

![setting.png](GunfireDungeon_Document/文档资源/setting.png)  

---
### Other  

**Development Log:** [Development Log.md](GunfireDungeon_Document/开发日志.md)  

**Bilibili:** [https://space.bilibili.com/259437820](GunfireDungeon_Document/文档资源/setting.png)  

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

### 3. Fonts

* **Removed the bundled commercial trial bitmap fonts** (`DinkieBitmap-*Demo`, `VonwaonBitmap-*`)
* Replaced with **[Ark Pixel Font](https://github.com/TakWolf/ark-pixel-font)** (`ArkPixel-12px-zh_cn.ttf`), licensed under **SIL OFL 1.1** — see `resource/font/ArkPixel-LICENSE.md`
* Pixel-font import settings tuned (antialiasing / hinting / subpixel positioning adjusted) so glyphs stay crisp at large sizes
* Font sizes normalized to **integer multiples of the 12px base size**

### 4. Bug Fixes

* Fixed the settings panel overflowing the screen and spilling content outside its frame
* Fixed errors caused by encyclopedia entries that have no icon configured

### 5. Cleanup

* Removed unused import caches and dead constants

---

> These notes are a record of changes only. **All original copyright belongs to the original author.**

