# 武器音效来源与署名

> **本套武器音效来自 CC0（公有领域）素材库，法律上无需署名。**
> **但本项目选择一律署名** —— 逐项记录来源，便于审核与致谢。

---

## 来源

### The Free Firearm Sound Library

| 项目 | 内容 |
|---|---|
| **原始页面** | https://opengameart.org/content/the-free-firearm-sound-library |
| **授权** | **CC0 1.0 Universal（公有领域奉献）** |
| **来源** | 2013 年 Kickstarter 众筹的开源枪械音效采集项目 |
| **整理上传** | bart（OpenGameArt） |
| **录音与整理** | **Ben Jaszczak、Brian Nelson、Kevin Heras、Matthew Nanney** 等 |
| **原始素材** | 6~30 秒的整段靶场录音，覆盖 22 种枪械型号 |

### 建议的署名文字（CC0 不强制，本项目主动署名）

```
Weapon sound effects from The Free Firearm Sound Library
(CC0 1.0, https://opengameart.org/content/the-free-firearm-sound-library)
Recorded and prepared by Ben Jaszczak, Brian Nelson, Kevin Heras,
Matthew Nanney, and contributors.
```

---

## 本项目做的处理

原素材**不能直接用**：
- 每个 wav 是 6~30 秒的整段录音（含多发 + 静音），RMS 只有 0.01~0.04
- 直接用会变成"一声枪响后面拖着十几秒空白"

处理步骤：

| 步骤 | 说明 |
|---|---|
| **1. 起音检测** | 用 2ms 窗算包络，找能量突增点 |
| **2. 单发切片** | 从起音切固定长度（手枪 350ms / 冲锋 300ms / 步枪 550ms / 栓动、霰弹 700ms）|
| **3. 剔除连发** | 丢弃与下一起音重叠的片段 —— 那是连发，不是单发 |
| **4. 质量筛选** | 只保留 **起音 < 8ms** 且 **100ms 处衰减 < 45%** 的片段 |
| **5. 归一化 + 淡出** | 每发归一到峰值 0.85，尾部 30ms 淡出避免爆音 |

> **质量筛选是必需的**：只按音量排序时，挑中过两个不合格片段 ——
> 一个起音 95ms（不是瞬时起爆），一个 100ms 后仍有 96% 能量（是连发）。

---

## 最终采用的 13 个

对应游戏内 `resource/sound/sfx/shooting/Shooting0001~0013.ogg`

| # | 枪械 | 类别 | 时长 | 频谱质心 | 起音 | 100ms 衰减 |
|---|---|---|---|---|---|---|
| 0001 | Bersa | 手枪 | 350ms | 3301 Hz | 0.8ms | 2% |
| 0002 | Walther PPQ | 手枪 | 350ms | 3879 Hz | 2.4ms | 0% |
| 0003 | Ruger Mark III | 手枪 | 350ms | 4108 Hz | 1.7ms | 2% |
| 0004 | M1911 | 手枪 | 350ms | 3368 Hz | 0.6ms | 1% |
| 0005 | M1917 | 转轮 | 400ms | 2006 Hz | 1.7ms | 1% |
| 0006 | Smith & Wesson 642 | 转轮 | 400ms | 2935 Hz | 0.9ms | 0% |
| 0007 | PPSh-41 | 冲锋 | 300ms | 4260 Hz | 1.5ms | 4% |
| 0008 | Carl Gustav M45 | 冲锋 | 300ms | 4511 Hz | 0.2ms | 0% |
| 0009 | Tikka | 步枪 | 550ms | 1946 Hz | 0.4ms | 4% |
| 0010 | AK-47 | 步枪 | 550ms | 2611 Hz | 0.7ms | 20% |
| 0011 | SKS | 步枪 | 550ms | 3451 Hz | 1.9ms | 1% |
| 0012 | 有坂（Arisaka）| 栓动 | 700ms | 2846 Hz | 1.9ms | 2% |
| 0013 | Mossberg | 霰弹 | 700ms | 2677 Hz | 0.9ms | 18% |

**全部 13 个都通过质量门槛**（起音 < 8ms、衰减 < 45%），覆盖 6 个枪械类别。

---

## 替换掉的是什么

| | 原音效 | 现音效 |
|---|---|---|
| 来源 | Kenney `Impact Sounds` / `Sci-Fi Sounds`（CC0）| Free Firearm Sound Library（CC0）|
| 性质 | **激光/能量武器音**，不是枪声 | **真实枪械录音** |
| 问题 | 与"枪火地牢"的枪械设定不符 | — |

> **说明**：Kenney 的素材包里**没有写实枪声**（该系列偏卡通/科幻），
> 所以"不像枪械声音"是素材本身的局限，只能换库解决。

---

## 未标注的事项

- 原录音的**具体场地、日期、枪械批次**信息，上游未逐一提供
- 本项目只做了切片与归一化，**未改变录音的音色**
- 上游页面未声明任何使用限制（CC0 即为无限制）
