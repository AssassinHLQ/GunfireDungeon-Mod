# UI 音效素材说明 / UI Sound Assets

本目录的 3 个音频文件用于界面交互音效，由 `src/framework/ui/UiSound.cs` 统一播放。

---

## 来源

| 项目 | 内容 |
|---|---|
| 素材包 | **Interface Sounds (1.0)** |
| 作者 | **Kenney**（www.kenney.nl） |
| 来源 | <https://kenney.nl/assets/interface-sounds> |
| 许可证 | **CC0 1.0 Universal**（公有领域） · <https://creativecommons.org/publicdomain/zero/1.0/> |
| 作者原话 | *"This content is free to use in personal, educational and commercial projects. Support us by crediting Kenney or www.kenney.nl (this is not mandatory)."* |

> CC0 **不要求署名**。本项目仍在此主动致谢 Kenney。

**建议的署名文字**：

```
UI sound effects by Kenney (CC0 1.0) — https://kenney.nl
```

---

## ⚠️ 已替换为柔和版本（重要）

原版三个音效**高频能量过重、听感刺耳**，已换成同素材包里更柔和的文件。

**文件名保留原名，是为了不改动任何代码引用**，实际内容按下表对应：

| 文件（文件名不变） | **实际内容来自** | 用途 | 频谱质心 | 4kHz 以上能量 |
|---|---|---|---|---|
| `click_001.ogg` | **`click_002.ogg`** | 按钮按下 | 1016 Hz | **0.2%** |
| `select_008.ogg` | **`drop_004.ogg`** | 鼠标悬停 | 680 Hz | **0.0%** |
| `toggle_001.ogg` | **`click_003.ogg`** | 勾选框开关 | 1707 Hz | **0.2%** |

### 替换前后的客观对比

| 文件 | 原频谱质心 | 原 4kHz 以上能量 | 现频谱质心 | 现 4kHz 以上能量 |
|---|---|---|---|---|
| `click_001` | 4105 Hz | **38.4%** | **1016 Hz** | **0.2%** |
| `select_008` | 11379 Hz | **89.7%** | **680 Hz** | **0.0%** |
| `toggle_001` | 3891 Hz | 32.3% | **1707 Hz** | **0.2%** |

> `select_008` 原本 **89.7% 的能量在 4kHz 以上** —— 几乎是个纯高频音，难怪刺耳。

### 判断"刺耳"的客观方法（可复现）

```python
import numpy as np, soundfile as sf
a, sr = sf.read(path, always_2d=True); a = a.mean(axis=1)
w = min(1024, len(a))
S = np.abs(np.fft.rfft(a[:w] * np.hanning(w)))
f = np.fft.rfftfreq(w, 1/sr)
centroid = (S * f).sum() / S.sum()                 # 频谱质心，越高越刺耳
high_pct = S[f > 4000].sum() / S.sum() * 100       # 4kHz 以上能量占比
```

**经验值**：
- 质心 < 1500 Hz 且高频 < 2% → 柔和不刺耳
- 质心 > 4000 Hz 或高频 > 12% → 明显刺耳

---

## 文件清单与用途

| 文件 | 用途 | 时长 | 播放音量 |
|---|---|---|---|
| `click_001.ogg` | 按钮按下 | 0.01 s | 0.65 |
| `select_008.ogg` | 鼠标悬停 | 0.29 s | 0.35 |
| `toggle_001.ogg` | 勾选框开关 | 0.01 s | 0.60 |

挑选依据：时长要短（点击类不超过 0.15 s，否则会拖沓）、电平要低（界面音效不能盖过枪声），
**且频谱质心要低**（这是"不刺耳"的客观判据）。

---

## 未使用但可选的同包素材

素材包共 100 个文件，已下载在仓库外的 `GODOT/UI素材/Kenney_Interface-Sounds/`。
若日后需要，可直接补充（**优先选质心低的**）：

| 用途 | 可用文件 |
|---|---|
| 返回 | `back_001` ~ `back_004` |
| 关闭面板 | `close_001` ~ `close_004` |
| 确认 | `confirmation_001` ~ `confirmation_004` |
| 错误 | `error_001` ~ `error_008` |
| 滚动 | `scroll_001` ~ `scroll_005` |
| **最柔和的一组** | **`bong_001`(230Hz) / `drop_002~004`(680~751Hz) / `click_002`(1016Hz)** |

---

## 音量与总线的说明

音效通过 `SoundManager.PlaySoundEffect()` 播放，**走 `SFX` 音频总线**，
因此设置面板里的「音效音量」滑块会统一控制它，无需单独做音量选项。
`default_bus_layout.tres` 中：bus 1 = `BGM`，bus 2 = `SFX`。
