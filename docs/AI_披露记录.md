# AI 生成内容披露记录

> 这份文件有三个用途：
> 1. **上架披露** —— Valve / Epic 要求明确披露游戏内使用了哪些 AI 生成内容
> 2. **权利主张基础** —— 记录人类创作的部分，这是主张著作权的依据
> 3. **自查清单** —— 防止漏披露；漏披露比披露了更麻烦
>
> **最后更新：2026-09-16**（本次为全项目重新审计，不是增量补充）

---

## 一、结论速览

| 类别 | 数量 | 是否需要上架披露 |
|---|---|---|
| **游戏内可见的 AI 生成美术** | **2 项** | ✅ **必须披露** |
| **疑似 AI 生成，待确认** | 1 项 | ⚠️ 确认后再定 |
| **AI 生成音乐** | **0 项**（已全部移除）| — |
| 程序化生成美术（非 AI） | 3 项 | ❌ 无需披露 |
| CC0 / CC PD 外部素材 | 5 首音乐 + 天空底图 | ❌ 但需署名 |

> **重要说明**：本项目的**代码**大量由 AI 编码助手协作编写，
> 这属于"工具辅助开发"，与"生成式 AI **内容**"是两个不同的披露项。
> 详见 §四。

---

## 二、✅ 必须披露：游戏内可见的 AI 生成美术

### 2.1 大橘（Boss）精灵图 —— **就是原先的「哈基米」**

| 项目 | 内容 |
|---|---|
| 游戏内文件 | `resource/sprite/role/daju/DajuIdle.png`（256×256）<br>`DajuWalk.png`（256×256）<br>`DajuAttack.png`（256×256）<br>`DajuBossAttacks.png`（896×512）<br>`prefab/role/enemy/Daju.tscn` |
| **AI 源图** | `可用素材_待接入/猫角色_原叫哈基米_必须改名/` 下的 `Hakimi*.png` |
| **工具** | 用户自有 AI 生成（**具体模型/日期待补**）|
| 日期 | 2026 年内 |
| **人类二次创作** | ① 真像素化（NEAREST 降采样到统一网格）<br>② 限色量化（多档调色板测试后定档）<br>③ 切帧、按 128×128 单元重排<br>④ 改名 Hakimi → Daju<br>⑤ 受击框按新尺寸重设<br>⑥ 编写 `DajuSpriteFrames.cs` 注册动画 |
| 处理脚本 | `_tools/pixelate_daju.py` / `pixelate_daju2.py` / `pixelate_daju3.py` |
| 披露状态 | ✅ 已记录 |

> **审计结论（重要）**：`_tools/pixelate_daju*.py` 里明确写着
> `SRC = "...猫角色_原叫哈基米_必须改名"`、`FILES = ["Hakimi*.png"]`、
> `base = f.replace("Hakimi", "Daju")`。
> **所以大橘和哈基米是同一批美术，只是改了名字。**
> 之前分开记录会漏掉大橘这一项。

### 2.2 游戏启动画面（像素化 Godot logo）

| 项目 | 内容 |
|---|---|
| 文件 | `GunfireDungeon_Godot/pixel-art-1789376270800.png`（1672×941）|
| 已生效 | ✅ **已接入**：`project.godot` 的 `boot_splash/image="uid://7wr4wvchxt5o"` 指向它 |
| 内容 | 像素化的 Godot 机器人标志 + "GODOT Game engine" 字标 |
| 工具 | OpenAI `gpt-image-2`（ChatGPT Images 2.0）|
| 日期 | 2026-09-14 |
| 合法性依据 | Godot 官方《商标政策与许可》明确允许：**"Feel free to also make a derivative of the logo that fits your game for the splash screen, 'Sega Genesis' style."** |
| 人类二次创作 | 尺寸裁剪、在 Godot 编辑器中设为启动图 |
| 披露状态 | ✅ 已记录 |
| 待办 | 建议移到 `resource/sprite/ui/bootSplash/boot_splash.png` 并改名（目前躺在仓库根目录）|

---

## 三、⚠️ 待确认：`pixel-art-1789380797663.ico`

| 项目 | 内容 |
|---|---|
| 文件 | `GunfireDungeon_Godot/pixel-art-1789380797663.ico`（256×256，单尺寸）|
| 状态 | **未纳入版本管理**（工作区未跟踪文件）|
| 疑点 | 文件名 `pixel-art-<时间戳>` 与 §2.2 的 AI 启动图**同一命名规则**，时间戳也相邻（1789376270800 → 1789380797663，相差约 75 分钟），**高度疑似同一批 AI 产物** |
| **与旧记录冲突** | 旧版本文档写"图标未使用 AI，无需披露"，但那指的是 `_icon/final/icon.ico`（程序化绘制，7 种尺寸）。**这个 `pixel-art-*.ico` 是另一个文件**，不能沿用那条结论 |
| 从未提交 | `git log --all -- "*pixel-art*"` 无该文件记录 |

**待你确认**：这个 ico 是 AI 生成的吗？
- 如果是 → 移到 §二 并披露
- 如果不是（比如是程序化脚本产出）→ 记录它的生成脚本，移到"无需披露"
- 如果不用了 → 直接删掉

> **不要**把它和 `_icon/final/icon.ico` 混为一谈。后者确实不是 AI。

---

## 四、⭐ 代码部分：AI 编码助手协作

这是本文件原先**完全没有记录**的一项，但它对上架披露有实际影响。

| 项目 | 内容 |
|---|---|
| 范围 | 2026-09-14 ~ 2026-09-16 期间的多个提交，涉及 BGM 系统、商店修复、魔王模式、翻滚操作、地图守卫等 |
| 方式 | 人类提出需求与决策 → AI 编码助手实现 → 人类实机验证并反馈 |
| **人类贡献** | 全部需求定义、玩法设计、数值决策、实机测试、错误反馈、取舍判断 |
| 代码权利 | 代码著作权归本项目（人类主导的软件创作），**不需要**作为"AI 生成内容"向商店披露 |
| 为什么要记这笔 | 若将来有人质疑某段代码的来源，这份记录能说明协作方式 |

> **与生成式 AI 内容的区别**：Steam/Epic 的 AI 披露项针对的是
> **游戏内呈现给玩家的内容**（美术、音乐、文字、语音）。
> 代码属于开发工具使用，不在该披露项内。
> 但如实记录更稳妥。

---

## 五、❌ 不需要披露：程序化生成（无 AI 环节）

| 项目 | 文件 | 生成方式 |
|---|---|---|
| **游戏图标** | `_icon/final/icon.ico` + `icon_*.png` + `steam_community_184.png` | **程序化绘制**，脚本 `_icon/build_icon_v3.py`。文字由方舟像素字体渲染（SIL OFL 1.1），施工图标注（尺寸线、箭头、界线、三角板、中心标记）全部代码绘制。每个尺寸单独设计，不是缩放。 |
| **主菜单背景** | `resource/sprite/ui/mainBackground/bg_wall.png`、`bg_ridge.png` | **程序化生成**，脚本 `generate_background.py`。砖墙、拱窗、壁柱、券石、远山全部代码绘制。 |
| **主菜单背景的天空层** | `bg_sky.png` | 基于 **CC0** 素材调色板替换（见 §六），非 AI |

> 这三项都**可主张著作权**（人类创作），且不受"纯 AI 产出不可版权"的限制。
> 这比把整张图交给图像模型生成更有利。

---

## 六、外部素材（非 AI，但需署名）

| 素材 | 位置 | 许可 | 署名要求 |
|---|---|---|---|
| 主菜单天空底图 | `bg_sky.png` 来源 | **CC0 1.0**（Sunny Land 2D Pixel Art Pack · Luis Zuno / ansimuz）| 不要求（仍主动致谢）|
| 主菜单曲 Menu.ogg | `resource/sound/bgm/` | **CC PD** | 不要求（本项目仍记录）|
| 大厅曲 Hall_Nocturne72.ogg | 同上 | **CC PD** | 不要求 |
| 普通关卡曲 Battle_Pathetique.ogg | 同上 | **CC PD** | 不要求 |
| Boss 曲 Scherzo.ogg | 同上 | **CC PD** | 不要求 |
| 备选曲 Menu_Heavy.ogg | 同上 | **CC PD** | 不要求 |

**逐首的作曲/演奏者/来源/处理链见 [`音乐授权清单.md`](音乐授权清单.md)。**

---

## 七、✅ AI 生成音乐：0 项（已全部移除）

2026-09-16 决定不再使用 AI 生成音乐，5 首 ACE-Step 生成的曲子已从仓库删除：

`Hall.ogg` / `Battle_v1.ogg` / `Battle_v2.ogg` / `Boss.ogg` / `Boss_Full.ogg`

释放 8.22 MB，删除前备份在 `_tools/backup/AI音乐_删除前备份/`。
详见 [`音乐授权清单.md`](音乐授权清单.md) 第 3 节。

**所以现在游戏内 BGM 全部是 CC PD / 公有领域，无 AI 生成音乐需要披露。**

---

## 八、游戏外（不进发行包，无需披露）

| 项目 | 位置 | 说明 |
|---|---|---|
| `_acestep/` | 仓库外 | ACE-Step 音乐生成脚本与提示词（已不再使用）|
| `_music/` | 仓库外 | 音乐处理脚本与母版 |
| `_tools/` | 仓库外 | 各类处理脚本 |
| `_隔离区_侵权风险_请勿使用/` | 仓库外 | 已排除的素材（含 `.gdignore` 防止被 Godot 扫描）|
| `_pixelcat/` | 仓库外 | 早期生图测试（**5 个文件，测试 usa-gpt-image 技能用的**）|
| `可用素材_待接入/` | 仓库外 | 待接入素材（含 `.gdignore`）|

> `_pixelcat/` 与 `可用素材_待接入/` 都在**游戏资源目录之外**，
> 且有 `.gdignore`，**不会被打包进发行版**。

---

## 九、OpenAI 输出归属条款（图像部分依据）

本项目使用的 AI 图像生成服务为 OpenAI 的 `gpt-image-2`。

> **"As between you and OpenAI, and to the extent permitted by applicable law, you own all Input and Output."**
>
> —— OpenAI Terms of Use

免费版、Plus、企业版、API 各档均允许商业使用。
OpenAI 还为生成图片写入 **C2PA 溯源元数据**，文件本身即带"AI 生成"标记，
**披露是被动可验证的**，不依赖本文件的自我声明。

### 版权的现实边界（必须如实记录）

| | 结论 |
|---|---|
| **可以商用** | ✅ 这是**合同权利**，条款明确授予 |
| **能否阻止他人复制** | ❌ 纯 AI 生成内容**不受著作权保护** |
| **人类参与的作用** | ⚠️ 实质性编辑、指导或转化，人类创作部分**可能**获得保护 |
| **地域差异** | ⚠️ 中国等辖区已开始承认"人类充分主导的 AI 产出"可获著作权 |

**本项目的做法：让每一项 AI 产出都经过充分的二次创作**（像素化、限色量化、
切帧、手工重排等），并在 §二 逐项记录这些人类处理步骤。

---

## 十、上架前检查清单

- [ ] **确认 `pixel-art-1789380797663.ico` 的来源**（§三）
- [ ] 补上大橘/哈基米的 **AI 具体模型与生成日期**（§2.1）
- [ ] Steam 商店页的"AI 内容披露"栏按 §二 逐项填写
- [ ] 启动画面移到 `resource/sprite/ui/bootSplash/` 并改名
- [ ] 确认每项 AI 产出都有人类二次创作步骤（§二 已记录）
- [ ] 保留原始提示词文本（建议单独存 `提示词存档/`）
- [ ] 确认**没有**使用禁止内容：真人肖像、在世艺术家风格、深度伪造
- [ ] 复核 [`音乐授权清单.md`](音乐授权清单.md) 的署名是否已进游戏制作人员名单

---

## 十一、参考来源

- OpenAI Terms of Use（输出归属条款）
- [OpenAI Help：是否会对我生成的内容主张版权](https://help.openai.com/zh-hans-cn/articles/5008634-will-openai-claim-copyright-over-what-outputs-i-generate-with-the-api)
- [AI 输出权利分析（DALL-E 3 / 2026-03）](https://www.terms.law/ai-output-rights/dall-e/)
- [Godot 商标政策与许可（2025-07-21 版）](https://godot.foundation/policies-and-procedures/trademark-policy)

> **说明**：本文档是许可条款的解读记录，不是法律意见。正式商业发布前建议由专业人士复核。
