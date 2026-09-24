# AK47 武器贴图说明 / AK47 Weapon Sprite

本目录只有 **1 个 PNG**：`AK47.png`（38×15）。

它是武器 **AK47**（`weapon0047`）的贴图，**同时兼作游戏内 UI 图标**
（`ActivityBase.weapon0047.Icon` 和 prefab 里的 `SpriteFrames` 都指向它）——
这一点和原项目里其它 14 把武器完全一致：

| 用途 | 取的路径 |
|---|---|
| 手持 / 掉落时显示 | `prefab/weapon/Weapon0047.tscn` → `AnimatedSprite.SpriteFrames` |
| 拾取提示 / 商店 / 图鉴 / 武器轮盘图标 | `ActivityBase.weapon0047.Icon` |

---

## ⚠️ 这张图**不在 git 仓库里**

CraftPix 免费素材条款 **§2.2.1 禁止再分发美术源文件或其修改版**，
而本仓库是**公开仓库** —— 所以 `AK47.png` 被 `.gitignore` 排除，
**只在打包发行时从本机磁盘打进 pck**。

| | 仓库里 | 打包发行版里 |
|---|---|---|
| `AK47.png` | ❌ 没有 | ✅ 有 |
| `LICENSE.md`（本文件）| ✅ 有 | ✅ 有 |
| `restore_ak47.ps1`（重建脚本）| ✅ 有 | — |

**别人 `git clone` 下来会怎样**：`weapon0047` 的配置行仍然是完整的
（`WeaponBase` / `ActivityBase` / `PartBase` / `BulletBase` 都在 JSON 里），
但贴图缺失 —— 这把枪**会出现在随机武器池里却画不出来**。
所以**从源码构建前请先跑一次 `restore_ak47.ps1`**。

> 这和主菜单那 9 套 CraftPix 背景的处理方式一样
> （见 `../../ui/mainBackground/variants/LICENSE.md`），
> 区别是背景代码里有兜底（加载不到就退回视差大厅），
> 而武器贴图没有兜底，必须补齐才能正常显示。

---

## 素材来源

| 项目 | 内容 |
|---|---|
| 素材包 | **Free Guns Icon 32x32 Pixel Pack**（CraftPix 免费素材）|
| 原始文件 | `1 Icons\Icon29_37.png`（32×32）|
| 来源站 | [CraftPix.net](https://craftpix.net/freebies/) 免费区 |
| 发布方 | CraftPix / 其 itch.io 账号 [Free Game Assets](https://free-game-assets.itch.io/) |
| 授权页 | <https://craftpix.net/file-licenses/> （随包附带的 `License.txt` 即指向该页）|
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

> **本项目的处理**：第 2.2.1 条 → 这张图**不入公开仓库**，只随打包发行。
> 随包附带的 **PSD 源文件未使用**，也未纳入本仓库。
> 另外，本包里的字体（`Font.txt` 指向的 *Future Millennium*）**未使用**。

---

## 本项目的改动

原始素材是 **3/4 俯视角度的 UI 图标**（32×32，枪身沿对角线摆放），
而本游戏的武器贴图统一是**侧视、枪口朝右、水平摆放**，在代码里按瞄准角度整体旋转
（见 `src/game/activity/weapon/Weapon.cs` 里 `GlobalRotation = master.MountPoint.GlobalRotation`）。
所以不能直接用，必须转正：

| | 值 |
|---|---|
| 旋转 | **顺时针 39.4°**（枪身长轴原本与水平线成 39.4°）|
| 取样 | 最近邻（Nearest Neighbor），无插值 |
| 裁剪 | 按不透明像素裁掉四周透明边 |
| 结果 | 32×32 → **38×15**，枪口在右，枪托在左 |
| 调色 | **未改动**，保留原图颜色 |

旋转角度是量出来的：对不透明像素做 PCA 得主轴 135.07°，
再取距离最远的一对像素（枪托 (1,25) → 枪口 (29,2)）得 140.6°，
两者一致指向 **39.4°**。

**重建方式**（不需要 Python，只用 .NET 的 System.Drawing）：

```powershell
powershell -ExecutionPolicy Bypass -File restore_ak47.ps1
```

脚本默认从本机的素材目录读 `Icon29_37.png`；如果那个路径变了，
用 `-SourceIcon "<路径>"` 指定。

> 注：脚本用"逆映射 + 最近邻取样"实现旋转，和 Python PIL
> `Image.rotate(-39.4, resample=NEAREST, expand=True)` 的结果**视觉等价**
> （受半像素相位影响，边缘个别像素的取样点不同，不影响观感）。

---

## prefab 锚点是怎么定的

`prefab/weapon/Weapon0047.tscn` 里几个关键坐标，都是照着这张图的像素量出来的：

| 节点 | 值 | 依据 |
|---|---|---|
| `AnimatedSprite.position` / `Collision.position` | `(8, 1)` | `_gripPoint`（握把）。贴图 38 宽、中心在 19，握把在距左边 11 像素处 → `19 - 11 = 8`。和步枪 `(5,1)`（32 宽、中心 16、握把距左 11）同一套算法 |
| `FirePoint` | `(18, -3)` | 枪口。最右一列不透明像素在 x=37，`37 - 19 = 18`；枪管中线在 y≈4.5，`4.5 - 7.5 = -3` |
| `ShellPoint` | `(-3, -5)` | 抛壳口（机匣上方靠后）|
| `Collision` 形状 | `RectangleShape2D(26, 7)` | 枪身大致 30×7，取略小一圈 |

对照参考：步枪 `Weapon0001` 是 32×16 / `position(5,1)` / `FirePoint(15,-2)`，
规律一致（`FirePoint.x ≈ 半宽 - 1`）。
