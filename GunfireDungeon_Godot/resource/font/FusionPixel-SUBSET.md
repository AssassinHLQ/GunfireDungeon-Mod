# FusionPixel 子集字体说明

`FusionPixel-12px-zh_hans.ttf` 已被**子集化**：从 **6.68 MB 压到 277 KB（−95.9%）**。

---

## 为什么可以子集化

`GameFont.tres` 的组合是 **主字体 + 回退字体**：

```
GameFont.tres
  ├─ base_font  = ArkPixel-12px-zh_cn.ttf      (24471 字, 主字体)
  └─ fallbacks  = FusionPixel-12px-zh_hans.ttf (原 36558 字, 回退)
```

回退字体**只在主字体没有某个字的时候才会被查询**。所以它根本不需要带 36558 个字 ——
只需要覆盖「**游戏里会出现、而且方舟像素没有**」的那部分。

实测数据（2026-09-20）：

| | |
|---|---|
| ArkPixel 字符数 | 24471 |
| FusionPixel 字符数 | 36558 |
| ArkPixel 有但 FusionPixel 没有 | **0** —— 即 FusionPixel ⊇ ArkPixel |
| 游戏一共用到几个不同字符 | **1468** |
| 其中方舟像素缺的 | **59** |

方舟缺的那 59 个是：`券势即垫奖姿嵌廊弊恐惑执拖拳搬摇撼擎斜施旋既杀梭楔概毁浆溯滚漩热然燃瓷瞬窗筑紧紫纵缘聚药蔽警辨遥避鉴隙雾`

只留这 59 个字，字体只有 **13 KB**。但那样太脆 —— 以后改文案碰到一个新字就会出方框。
所以**保守地保留全部 1468 个字 + 一份常用字余量，共 1623 字，277 KB**。
多花 264 KB，换来的是「现有文案怎么改都不会缺字」。

---

## 改完文案之后怎么办

**重新跑一次子集脚本**（在项目根目录 `GunfireDungeon_Godot/` 下）：

```powershell
uv run --with fonttools python resource/font/subset_fusion.py
```

脚本会：
1. 扫描项目里所有 `.json / .tscn / .tres / .cs / .gd / project.godot`，
   把出现过的字符全收集起来（**连注释也算**，宁可多留不能漏）；
2. 加上一份常用字余量；
3. 从**原始完整字体备份**重新切一次，覆盖 `FusionPixel-12px-zh_hans.ttf`；
4. 写出字表 `FusionPixel-subset-chars.md`。

> ⚠️ **原始完整字体的备份在项目外**：
> `C:\Users\WY157\Desktop\GODOT\_art\fonttest\FusionPixel-full-backup.ttf`
>
> **千万别把备份放进项目目录** —— Godot 会扫描到它、导入它、并把它一起打进 pck，
> 那这 6.68 MB 就白省了。（第一次就踩过这个坑。）

---

## 风险与边界

- **只影响"方舟像素没有的字"**：方舟有的字仍然由主字体渲染，排版度量完全没变。
- **新文案如果用到方舟没有、且没进子集的字**，会退到系统字体（显示效果不统一）或方框。
  跑一次上面的脚本即可解决。
- **别去改 `FusionPixel-12px-zh_hans.ttf` 本身**，改了下次重跑脚本就没了；要改就改脚本。

---

## 授权

子集化属于 OFL 允许的「修改」，且**未改字体名称**，所以仍沿用 **SIL OFL 1.1**，
原始授权文件 `FusionPixel-OFL.txt` 原样保留在旁。

> OFL 说明：用字体渲染文字输出不受限制；OFL 只约束字体文件本身的再分发
> （不得单独出售字体、衍生字体须沿用 OFL）。子集字体属于衍生，已沿用 OFL。
