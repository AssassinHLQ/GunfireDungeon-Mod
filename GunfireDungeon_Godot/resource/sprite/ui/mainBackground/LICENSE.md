# 主菜单背景素材说明 / Main Menu Background Assets

本目录的三个 PNG 用于主菜单视差背景（`src/game/ui/game/main/MainBackground.cs`）。

---

## bg_sky.png — 黄昏天空与云

| 项目 | 内容 |
|---|---|
| 原始素材 | **Sunny Land 2D Pixel Art Pack** |
| 作者 | Luis Zuno（**ansimuz**）· <https://ansimuz.com> |
| 来源 | OpenGameArt · <https://opengameart.org/content/sunny-land-2d-pixel-art-pack> |
| 许可证 | **CC0 1.0 Universal**（公有领域）· <https://creativecommons.org/publicdomain/zero/1.0/> |
| 作者原话 | *"Public domain and free to use on whatever you want, personal or commercial. Credit is not required but appreciated."* |

**本项目的改动**：从原包 `environment/layers/back.png` 中截取海平线以上部分（384×112），
5 倍整数放大到 1920×560，调成黄昏紫蓝色调，镜像拼接成 3840 宽以保证左右无缝，
下方以霞光渐变延伸到 1080 高。

> CC0 不要求署名。本项目仍在此主动致谢 ansimuz。

---

## bg_ridge.png — 远山剪影

| 项目 | 内容 |
|---|---|
| 来源 | **本项目程序化生成**（`_bg_build/make_bg4.py`） |
| 许可证 | 与主项目一致（AGPL-3.0） |
| 说明 | 由圆润凸起叠加 + 多倍频噪声合成，无第三方素材成分 |

---

## bg_wall.png — 砌石墙与拱窗

| 项目 | 内容 |
|---|---|
| 来源 | **本项目程序化生成**（`_bg_build/make_bg4.py`） |
| 许可证 | 与主项目一致（AGPL-3.0） |
| 说明 | 砖块配色取自原版游戏自带的 `resource/map/tileSet/TileSet1/Main.png`，未使用任何外部素材 |

---

## 复现方式

同目录的 `generate_background.py` 就是生成这三个 PNG 的完整脚本
（需要 Python 3 + Pillow + NumPy）。其中只有读取 `bg_sky.png` 原始素材那一步依赖外部资源，
远山与石墙完全由脚本合成，可独立复现。

---

## 关于「不使用已提取的旧项目美术资源」

旧项目提取出来的美术资源经确认**存在侵权素材**，已整批隔离到
`GODOT/_隔离区_侵权风险_请勿使用/`，本目录**不含**其中任何文件。
已用哈希交叉比对确认本仓库从未引入过那些文件。
