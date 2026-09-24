# 刽子手的幽灵 精灵图说明 / Executioner Spirit Sprites

本目录是「不死刽子手」召唤出来的小怪 **「刽子手的幽灵」**（`spirit0001`）的精灵图。

**它和 `../executioner0001/` 是同一份素材、同一个页面、同一份授权** ——
完整授权记录（含页面原文）在 **`../executioner0001/LICENSE.md`**，本文件不重复。

| 本项目文件 | 原作文件 | 尺寸 | 用途 |
|---|---|---|---|
| `SpiritIdle.png` | `summonIdle.png` | 200×50（4 格 50×50）| 待机 / 移动（幽灵没有走路动画）|
| `SpiritAppear.png` | `summonAppear.png` | 150×100（6 格）| 被召唤时出场 |
| `SpiritDeath.png` | `summonDeath.png` | 150×100（6 格，用前 5 帧）| 死亡 |
| `Spirit_Icon.png` | — | 64×64 | 图册/地图编辑器图标（本机生成）|

> 格子实测 **50×50**（cell=50 时 0 处跨界）。脚底固定在格子 y=37，身体水平中心 x=23.5。

## ⚠️ 同样不在 git 仓库里

授权 **禁止再分发**，而本仓库是公开仓库 —— 所以 PNG 被 `.gitignore` 排除，
只随打包发行进 pck。**从源码构建前先跑 `../executioner0001/restore_sprites.ps1`**
（那个脚本会把本目录的 3 张图一起还原）。
