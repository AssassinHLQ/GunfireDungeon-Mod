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

---

## 文件清单与用途

为便于日后溯源审计，这里**保留素材包里的原始文件名**，未做重命名。

| 文件 | 用途 | 时长 | 平均电平 | 播放音量 |
|---|---|---|---|---|
| `click_001.ogg` | 按钮按下 | 0.10 s | −26.4 dB | 0.65 |
| `select_008.ogg` | 鼠标悬停 | 0.05 s | −20.2 dB | 0.35 |
| `toggle_001.ogg` | 勾选框开关 | 0.14 s | −14.0 dB | 0.60 |

挑选依据：时长要短（点击类不超过 0.15 s，否则会拖沓）、电平要低（界面音效不能盖过枪声），
`click_001` 是包内 5 个 click 里唯一有完整衰减包络的（其余 4 个只有 0.01 s），
`select_008` 是 select 组里最轻的，适合做悬停。

---

## 未使用但可选的同包素材

素材包共 100 个文件，已下载在仓库外的 `GODOT/UI素材/Kenney_Interface-Sounds/`。
若日后需要，可直接补充：

| 用途 | 可用文件 |
|---|---|
| 返回 | `back_001` ~ `back_004` |
| 关闭面板 | `close_001` ~ `close_004` |
| 确认 | `confirmation_001` ~ `confirmation_004` |
| 错误 | `error_001` ~ `error_008` |
| 滚动 | `scroll_001` ~ `scroll_005` |

---

## 音量与总线的说明

音效通过 `SoundManager.PlaySoundEffect()` 播放，**走 `SFX` 音频总线**，
因此设置面板里的「音效音量」滑块会统一控制它，无需单独做音量选项。
`default_bus_layout.tres` 中：bus 1 = `BGM`，bus 2 = `SFX`。
