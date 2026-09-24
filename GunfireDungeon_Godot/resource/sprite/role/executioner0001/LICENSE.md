# 不死刽子手 精灵图说明 / Undead Executioner Sprites

本目录是精英怪 **「不死刽子手」**（`executioner0001`）的精灵图。
同素材里那三张召唤物图集放在隔壁 `../spirit0001/`（它属于同一个包、同一份授权）。

| 本项目文件 | 原作文件 | 尺寸 | 用途 |
|---|---|---|---|
| `ExecutionerIdle.png` | `idle.png` | 500×100（5 格 100×100，用 4 帧）| 待机 |
| `ExecutionerMove.png` | `idle2.png` | 400×200（8 格）| 移动（素材没有走路动画，用这个"飘"）|
| `ExecutionerAttack.png` | `attacking.png` | 600×300（18 格，用前 13 帧）| 二连挥砍 |
| `ExecutionerSweep.png` | `skill1.png` | 600×200（12 格）| 大范围横扫 |
| `ExecutionerSummon.png` | `summon.png` | 400×200（8 格，用前 5 帧）| 召唤幽灵 |
| `ExecutionerDeath.png` | `death.png` | 1000×200（20 格，用前 18 帧）| 死亡 |
| `Executioner_Icon.png` | — | 64×64 | 图册/地图编辑器图标（本机生成）|

> **格子实测**：用"格子边界那一列/行是否全透明"验过 —— `cell=100` 时 0 处跨界，
> `cell=50`/`cell=150` 都有跨界，所以主角色图集的格子就是 **100×100**。
> 空白帧：`idle` 第 4 帧、`attacking` 第 13~17 帧、`summon` 第 5~7 帧、`death` 第 18~19 帧
> —— 这正好对上原包 Readme 写的帧数（Idle 4 / Attacking 13 / Summoning 5）。

---

## ⚠️ 这些图**不在 git 仓库里**

页面授权原文（见下）：*"you can use it for commercial and non-commercial use, credits are not
required ... **redistributing and reselling the sprite are restricted**"* ——
**禁止再分发**。而本仓库是**公开仓库**，所以所有 PNG 被 `.gitignore` 排除，
**只在打包发行时从本机磁盘打进 pck**。

| | 仓库里 | 打包发行版里 |
|---|---|---|
| 9 个精灵图 PNG | ❌ 没有 | ✅ 有 |
| `LICENSE.md`（本文件）| ✅ 有 | ✅ 有 |
| `restore_sprites.ps1`（还原脚本）| ✅ 有 | — |

**别人 `git clone` 下来会怎样**：配置行是完整的（`ActivityBase` / `RoleBase` / `AiRoleAttr` 都在 JSON 里），
但贴图缺失 —— 这只精英怪**会进随机池却画不出来**。
**从源码构建前请先跑一次 `restore_sprites.ps1`。**

> 处理方式和 CraftPix 那批（主菜单背景 / AK47 贴图）一致，见
> `docs/素材授权清单.md` §三。

**本机怎么补回来**：

```powershell
powershell -ExecutionPolicy Bypass -File restore_sprites.ps1
```

脚本默认从本机的素材目录读原始 PNG；路径变了就用 `-SourceDir "<路径>"` 指定。
它**不需要 Python**，只是复制 + 改名（本项目对素材本身没有做任何像素改动）。

---

## 素材来源

| 项目 | 内容 |
|---|---|
| 素材包 | **Boss: Undead Executioner [FREE]** |
| 作者 | **Kronovi-**（itch 账号 `darkpixel-kronovi`）|
| 页面 | <https://darkpixel-kronovi.itch.io/undead-executioner> |
| 授权类型 | **作者自定义**（页面上写的，不是标准 CC 协议）|
| 署名 | **不要求**（原文：*credits are not required but always deeply appreciated*）→ **本项目主动署名** |

### 页面授权原文（2026-09-24 抓取存档）

> the zipfile contains layered .ase files for easier editing and animation .png files so you can use
> it directly.
> The pack contains dozen of keyframe which you can edit to have more animations. This character will
> also fit with a puppet master if you like. **You are free to edit the sprite once you downloaded it
> and you can use it for commercial and non-commercial use, credits are not required but always
> deeply appreciated.**
> **redistributing and reselling the sprite are restricted.**

### 逐条对照

| 条款 | 本项目 |
|---|---|
| ✅ 可商用（含 Steam/Epic 商业发行）| 采用 |
| ✅ 可修改 | 采用（只改了文件命名，像素未动）|
| ✅ 署名不要求 | **仍然会署名**（游戏内「修改说明」浮层 + `docs/素材授权清单.md`）|
| ❌ 禁止再分发 / 转售 | → **不入公开 git 仓库**，只随 pck 发行 |

> **关于那句"检索显示它出现在 Unity 资源商店"**：早期核查时怀疑这个包不是 itch 来源。
> 2026-09-24 打开 itch 页面确认后**排除了这个怀疑** —— 页面标题就是
> `Boss: Undead Executioner [FREE] by Kronovi-`，授权白纸黑字写在同一页上。
