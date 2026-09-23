# 主菜单随机背景素材说明 / Main Menu Background Variants

本目录的 **39 个 PNG（9 套 × 3~5 层）** 是主菜单的随机背景池，由
`src/game/ui/game/main/MainBackground.cs` 在每次进入主菜单时随机挑**一套**，
把这一套的每一层铺满全屏、各按不同速度横向循环滚动（视差）。

主菜单一共有 **10 套**背景：本目录这 9 套 + 原来那张视差石墙大厅
（`../bg_sky.png` / `../bg_ridge.png` / `../bg_wall.png`，见 `../LICENSE.md`）。
**10 套全部都是动态循环的。**

---

## ⚠️ 这些图**不在 git 仓库里**

授权条款禁止再分发美术源文件，而本仓库是公开仓库 —— 所以它们被 `.gitignore` 排除，
**只在打包发行时从本机磁盘打进 pck**。

| | 仓库里 | 打包发行版里 |
|---|---|---|
| 39 个背景图层 PNG | ❌ 没有 | ✅ 有 |
| `LICENSE.md`（本文件）| ✅ 有 | ✅ 有 |
| 程序化生成的视差大厅（选项 0）| ✅ 有 | ✅ 有 |

**别人 `git clone` 下来会怎样**：`MainBackground.cs` 会照常尝试加载，
加载不到就**退回视差大厅背景**并打一条 warning（不会黑屏、不会崩）。
也就是源码构建版是 1 套背景，发行版是 10 套。

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

> **本项目的处理**：第 2.2.1 条 → 这些图**不入公开仓库**，只随打包发行。
> 玩家从发行包里解包取出图片属于**解包者的行为**，不是本项目的分发行为。
> 随包附带的 **PSD 源文件未使用**，也未纳入本仓库。

---

## 为什么用的是分层图，不是 `orig_big.png`

每个 `background N` 目录里有这么几个文件：

| 文件 | 尺寸 | 是什么 |
|---|---|---|
| `1.png … N.png` | 576×324 | **视差分层**。1 = 最远（星空/底色），编号越大越靠前 |
| `orig.png` | 576×324 | 把上面几层压平的结果 |
| `orig_big.png` | 2304×1296 | 上面那张的 4 倍放大版（正好 ×4）|

一开始按"一张静图"接了 `orig_big.png`，结果是 **11 套里 10 套不会动** ——
主菜单从"动态循环"退化成"大部分时候是张静止画"，这是不对的。
改成直接用 `1.png…N.png` 这几层之后，全部背景都是动态循环的，
也才是这个素材包（*Parallax Clouds*）的本来用法。

**"这些层能首尾相接循环吗"验证过**（用 Pillow + NumPy 逐像素比对）：

1. 按 `1→N` 的顺序做 alpha 叠加，结果和 `orig.png` **逐像素完全一致（平均差 0.00）**
   —— 确认 `1.png…N.png` 就是 `orig.png` 的构成成分，且顺序是 1 在最底层。
2. 把每层左右首尾接起来看接缝，接缝处的列差和普通相邻列的列差**同量级**
   —— 确认这些层本来就是为横向平铺设计的。
3. 把合成图首尾接成两轮渲染出来目视复核，接缝处看不出竖线。

**本项目的改动**：无。直接使用原包里的编号图层，只做了改名。

**显示方式**：每层 576×324 的贴图拉伸铺满 1920×1080 画布（×3.33，Nearest 过滤），
同比例 16:9 所以**不会变形**；每层放**两份首尾相接**一起左移，移满一个周期归零，
因为贴图左右无缝，归零那一刻画面看不出跳变。越靠前的层滚得越快（6 → 30 像素/秒）。

---

## 文件对照表

原包目录名 → 本项目文件名。命名改为连续编号是为了让 `MainBackground.cs` 里
可以按 `bg_vNN_L1.png`、`bg_vNN_L2.png`… 的规律直接拼路径。

| 本项目前缀 | 原始位置 | 层数 | 是否启用 |
|---|---|---|---|
| `bg_v01_L*.png` | part1 / background 1 | 4 | ✅ |
| `bg_v02_L*.png` | part1 / background 2 | 5 | ✅ |
| `bg_v03_L*.png` | part1 / background 3 | 4 | ✅ |
| `bg_v04_L*.png` | part2 / background 1 | 4 | ✅ |
| `bg_v05_L*.png` | part2 / background 2 | 5 | ✅ |
| `bg_v06_L*.png` | part2 / background 3 | 4 | ✅ |
| ~~`bg_v07_L*.png`~~ | part2 / background 4 | 3 | ❌ **已停用** |
| `bg_v08_L*.png` | part3 / background 2 | 3 | ✅ |
| `bg_v09_L*.png` | part4 / background 1 | 5 | ✅ |
| `bg_v10_L*.png` | part4 / background 2 | 5 | ✅ |

合计 **39 个文件**（启用中的 9 套），约 240 KB。
原包里每个目录的 `orig.png` / `orig_big.png` / PSD **未使用**。

### 为什么停用 v07（part2 / background 4）

那是唯一只有 3 层的两套之一，而且整体偏亮、中间一团乳白正好压在标题后面 ——
用户 2026-09-20 决定不用它（"忍痛割爱"）。

**加回来只要两步**：

1. 在 `restore_variants.ps1` 里取消 `# @{ V = "07"; ... }` 那行注释并跑一次；
2. 把 `7` 填回 `MainBackground.cs` 的 `VariantIds` 数组。

编号保持不连续（缺 07）是故意的 —— 这样"哪个编号对应哪个素材包"永远对得上，
不用重命名一堆文件。

---

## 补齐后要做的两步

1. 跑 `restore_variants.ps1`（复制在用的 39 个图层文件进本目录，已停用的不会复制）。
2. 让 Godot 重新导入一次，生成 `.import` 与 `.godot/imported/*.ctex`：

   ```
   Godot_v4.7.1-stable_mono_win64_console.exe --headless --import --path <项目目录>
   ```

   或者直接用 Godot 编辑器打开一次项目，它会自动导入。
