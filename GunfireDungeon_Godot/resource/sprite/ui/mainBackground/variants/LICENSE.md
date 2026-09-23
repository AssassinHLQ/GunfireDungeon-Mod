# 主菜单随机背景素材说明 / Main Menu Background Variants

本目录的 10 个 PNG 是主菜单的**随机背景池**，由
`src/game/ui/game/main/MainBackground.cs` 在每次进入主菜单时随机挑选一张铺满全屏。

主菜单一共有 **11 种**背景：本目录这 10 张 + 原来那张视差石墙大厅
（`../bg_sky.png` / `../bg_ridge.png` / `../bg_wall.png`，见 `../LICENSE.md`）。

---

## ⚠️ 这 10 张图**不在 git 仓库里**

授权条款禁止再分发美术源文件，而本仓库是公开仓库 —— 所以它们被 `.gitignore` 排除，
**只在打包发行时从本机磁盘打进 pck**。

| | 仓库里 | 打包发行版里 |
|---|---|---|
| 10 张背景 PNG | ❌ 没有 | ✅ 有 |
| `LICENSE.md`（本文件）| ✅ 有 | ✅ 有 |
| 程序化生成的视差大厅（选项 0）| ✅ 有 | ✅ 有 |

**别人 `git clone` 下来会怎样**：`MainBackground.cs` 会照常尝试加载，
加载不到就**退回视差大厅背景**并打一条 warning（不会黑屏、不会崩）。
也就是源码构建版是 1 种背景，发行版是 11 种。

**本机怎么补回来**：跑同目录的 `restore_variants.ps1`，或照下面的对照表手工复制。

---

## 素材来源

| 项目 | 内容 |
|---|---|
| 素材包 | **New free backgrounds part1 ~ part4**（CraftPix 免费素材）|
| 来源站 | [CraftPix.net](https://craftpix.net/freebies/) 免费区 |
| 发布方 | CraftPix / 其 itch.io 账号 [Free Game Assets](https://free-game-assets.itch.io/) |
| 授权页 | <https://craftpix.net/file-licenses/> （随包附带的 `license.txt` 即指向该页）|
| 授权类型 | **CraftPix Freebie License**（免费素材条款 §2）|
| 署名 | **不需要**（条款原文：*No attribution or link back to this site is required*）|

### 授权条款要点（§2 Freebie Products）

| 允许 | 说明 |
|---|---|
| ✅ 用于任意数量的**个人与商业**项目 | §2.1.1 |
| ✅ **修改**后使用（改尺寸、调色、裁剪等）| §2.1.2 |
| ✅ **出售 / 分发**使用这些素材的游戏 | §2.1.3 |

| 限制 | 说明 |
|---|---|
| ❌ 不得把**美术源文件**（PNG / JPG / PSD 等）再出售 | §2.2.1 |
| ❌ 不得以"**另一个终端用户能够取用这些美术文件**"的方式再分发 | §2.2.1 |
| ❌ 不得用于训练 / 微调 AI 模型 | §3.1.1 |

> **本项目的处理**：第 2.2.1 条 → 这 10 张图**不入公开仓库**，只随打包发行。
> 玩家从发行包里解包取出图片属于**解包者的行为**，不是本项目的分发行为。
> 随包附带的 **PSD 源文件未使用**，也未纳入本仓库。

---

## 文件对照表

原包目录名 → 本项目文件名。命名改为连续编号是为了让 `MainBackground.cs` 里的
路径数组可以直接顺序列出，不必在代码里写 `"background 1"` 这种带空格的目录名。

| 本项目文件 | 原始位置（`New free backgrounds partN/backgroundM/orig_big.png`）| 尺寸 |
|---|---|---|
| `bg_v01.png` | part1 / background 1 | 2304×1296 |
| `bg_v02.png` | part1 / background 2 | 2304×1296 |
| `bg_v03.png` | part1 / background 3 | 2304×1296 |
| `bg_v04.png` | part2 / background 1 | 2304×1296 |
| `bg_v05.png` | part2 / background 2 | 2304×1296 |
| `bg_v06.png` | part2 / background 3 | 2304×1296 |
| `bg_v07.png` | part2 / background 4 | 2304×1296 |
| `bg_v08.png` | part3 / background 2 | 2304×1296 |
| `bg_v09.png` | part4 / background 1 | 2304×1296 |
| `bg_v10.png` | part4 / background 2 | 2304×1296 |

**本项目的改动**：无。直接使用原包里的 `orig_big.png`（用户指定用 `orig_big` 版本），
只做了改名。10 张都是 2304×1296（16:9），与 UI 画布 1920×1080 同比例，
`MainBackground.cs` 里拉伸铺满，**不会变形**。

---

## 补齐后要做的两步

1. 跑 `restore_variants.ps1`（复制 10 张图进本目录）。
2. 让 Godot 重新导入一次，生成 `.import` 与 `.godot/imported/*.ctex`：

   ```
   Godot_v4.7.1-stable_mono_win64_console.exe --headless --import --path <项目目录>
   ```

   或者直接用 Godot 编辑器打开一次项目，它会自动导入。
