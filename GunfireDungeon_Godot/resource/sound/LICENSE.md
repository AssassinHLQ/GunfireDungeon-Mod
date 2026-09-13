# 游戏音效素材说明 / Game SFX Assets

本目录（`resource/sound/`，不含 `ui/`）下的**全部音效文件**都已在本次修改中
**替换为 Kenney 的 CC0 音效**，替换前的上游文件已全部移除。

界面音效（`ui/` 目录）另见 `ui/LICENSE.md`。

---

## 为什么要替换

用元数据检查上游自带的音频时发现，5 个 `.mp3` 文件里嵌着**商业音效库的标记**：

| 文件 | 内嵌元数据 | 含义 |
|---|---|---|
| `sfx/role/enemy/Enemydie.mp3` | `encoded_by=Pro Tools`、`originator_reference=...`、`date=2013-01-17` | 专业音频工作站 + 商业音效库特征 |
| `sfx/role/player/Rolling.mp3` | `date=2001-03-16`、`IENG=E-hwa Choi` | 工程师署名 + 2001 年 |
| `sfx/role/player/RoleHurt.mp3` | `date=1998-08-26`、`IENG=…` | 1998 年 |
| `sfx/role/player/RoleDie.mp3` | 仅编码器信息 | — |
| `sfx/role/player/PickupWeapon.mp3` | 仅编码器信息 | — |

其余 61 个 `.ogg` **没有任何元数据标签**（说明被重新编码过），既查不到出处、
也无法证明其来源合法。为了彻底消除这一类风险，**整套音效一次性替换**。

> 另：`sfx/bgm/Intro.ogg` 虽然路径被 `ResourcePath.cs` 声明过，但**游戏里从未播放**
> （`Sound.json` 里没有它，代码里也没有调用），属于孤儿文件，已删除。
> 也就是说**当前游戏没有任何背景音乐**。

---

## 替换来源

| 素材包 | 文件数 | 许可证 | 链接 |
|---|---|---|---|
| **Impact Sounds** | 130 | **CC0 1.0** | <https://kenney.nl/assets/impact-sounds> |
| **Sci-fi Sounds** | 70 | **CC0 1.0** | <https://kenney.nl/assets/sci-fi-sounds> |
| **RPG Audio** | 50 | **CC0 1.0** | <https://kenney.nl/assets/rpg-audio> |

作者 **Kenney**（www.kenney.nl）。作者原话：
*"This content is free to use in personal, educational and commercial projects."*

CC0 不要求署名，本项目仍在此主动致谢。

---

## 替换方式

**保留游戏原有的文件名，只替换文件内容。**
`Sound.json` 里 59 条音效全部按路径引用文件名，因此这样替换**不需要改动任何配置或代码**。

唯一的例外是那 5 个 `.mp3`：Godot 按**扩展名**选择导入器，把 OGG 数据写进 `.mp3`
会导致导入失败，所以它们被**改名为 `.ogg`**，并同步修改了 `Sound.json` 里的 5 条路径。
（这同时也把那 5 个带商业元数据的文件彻底删除。）

替换脚本：`_bg_build/replace_audio.py`（仓库外，可复现）。

---

## ⚠️ 风格上的取舍：枪声变成了能量武器音

**Kenney 的音效包里没有写实枪声。** 可用的只有激光类（`laserSmall` / `laserRetro` /
`laserLarge`）与撞击类（`impactMetal_*` / `impactPunch_*`）。

因此 13 个 `shooting` 音效里：
- **3 个激光武器**（`Shooting0009/0010/0012`）→ 用激光音，**本来就是对的**
- **榴弹发射器**（`Shooting0011`）→ `explosionCrunch`，合适
- **其余弹道武器**（手枪/步枪/冲锋枪/狙击/P90）→ 只能用激光音近似

**听感上会从"枪战"偏向"能量武器"**。如果要写实枪声，需要另找 CC0 来源
（Kenney 之外），这属于后续可替换项 —— 本文件的映射表就是为此准备的。

---

## 完整映射表（游戏文件 → Kenney 来源）

路径均相对 `res://resource/sound/`。

| 游戏文件 | 替换为 (Kenney) |
|---|---|
| `sfx/beLoaded/BeLoaded0001.ogg` | `impactTin_medium_000.ogg` |
| `sfx/beLoaded/BeLoaded0002.ogg` | `impactTin_medium_001.ogg` |
| `sfx/beLoaded/BeLoaded0003.ogg` | `impactTin_medium_002.ogg` |
| `sfx/beLoaded/BeLoaded0004.ogg` | `impactMetal_medium_004.ogg` |
| `sfx/beLoaded/BeLoaded0005.ogg` | `impactMetal_heavy_002.ogg` |
| `sfx/beLoaded/BeLoaded0006.ogg` | `impactPlate_light_000.ogg` |
| `sfx/beLoaded/BeLoaded0007.ogg` | `impactMetal_heavy_003.ogg` |
| `sfx/beLoaded/BeLoaded0008.ogg` | `impactMetal_heavy_004.ogg` |
| `sfx/beLoaded/BeLoaded0009.ogg` | `impactPlate_light_001.ogg` |
| `sfx/beLoaded/BeLoaded0010.ogg` | `impactTin_medium_003.ogg` |
| `sfx/beLoaded/BeLoaded0011.ogg` | `impactPlate_light_002.ogg` |
| `sfx/beLoaded/BeLoaded0012.ogg` | `impactPlate_light_003.ogg` |
| `sfx/beLoaded/BeLoaded0013.ogg` | `impactPlate_light_004.ogg` |
| `sfx/beLoaded/BeLoaded0014.ogg` | `impactMining_000.ogg` |
| `sfx/beLoaded/BeLoaded0015.ogg` | `impactMining_001.ogg` |
| `sfx/beLoaded/BeLoaded0016.ogg` | `impactMining_002.ogg` |
| `sfx/beLoaded/BeLoaded0017.ogg` | `impactMining_003.ogg` |
| `sfx/collision/Collision0001.ogg` | `impactGeneric_light_000.ogg` |
| `sfx/common/gold.ogg` | `handleCoins.ogg` |
| `sfx/explosion/Explosion0001.ogg` | `explosionCrunch_001.ogg` |
| `sfx/explosion/Explosion0002.ogg` | `explosionCrunch_002.ogg` |
| `sfx/explosion/Explosion0003.ogg` | `lowFrequency_explosion_000.ogg` |
| `sfx/reloading/Reloading0001.ogg` | `impactMetal_light_000.ogg` |
| `sfx/reloading/Reloading0002.ogg` | `impactMetal_light_001.ogg` |
| `sfx/reloading/Reloading0003.ogg` | `impactMetal_light_002.ogg` |
| `sfx/reloading/Reloading_begin0001.ogg` | `metalClick.ogg` |
| `sfx/reloading/Reloading_begin0002.ogg` | `impactMetal_medium_000.ogg` |
| `sfx/reloading/Reloading_begin0003.ogg` | `impactMetal_medium_001.ogg` |
| `sfx/reloading/Reloading_begin0004.ogg` | `metalLatch.ogg` |
| `sfx/reloading/Reloading_begin0005.ogg` | `impactMetal_light_003.ogg` |
| `sfx/reloading/Reloading_begin0006.ogg` | `impactMetal_medium_002.ogg` |
| `sfx/reloading/Reloading_begin0007.ogg` | `beltHandle1.ogg` |
| `sfx/reloading/Reloading_begin0008.ogg` | `beltHandle2.ogg` |
| `sfx/reloading/Reloading_begin0009.ogg` | `impactMetal_heavy_000.ogg` |
| `sfx/reloading/Reloading_begin0010.ogg` | `drawKnife1.ogg` |
| `sfx/reloading/Reloading_begin0011.ogg` | `impactMetal_light_004.ogg` |
| `sfx/reloading/Reloading_begin0012.ogg` | `metalPot1.ogg` |
| `sfx/reloading/Reloading_begin0013.ogg` | `metalPot2.ogg` |
| `sfx/reloading/Reloading_finish0001.ogg` | `metalLatch.ogg` |
| `sfx/reloading/Reloading_finish0002.ogg` | `impactMetal_heavy_001.ogg` |
| `sfx/reloading/Reloading_finish0003.ogg` | `impactMetal_medium_003.ogg` |
| `sfx/reloading/Reloading_finish0004.ogg` | `drawKnife2.ogg` |
| `sfx/reloading/Reloading_finish0005.ogg` | `metalPot3.ogg` |
| `sfx/role/enemy/EnemyHurt.ogg` | `impactPunch_medium_000.ogg` |
| `sfx/role/enemy/Enemydie.ogg` | `slime_000.ogg` |
| `sfx/role/player/PickupWeapon.ogg` | `handleSmallLeather.ogg` |
| `sfx/role/player/RoleDie.ogg` | `impactPunch_heavy_001.ogg` |
| `sfx/role/player/RoleHurt.ogg` | `impactPunch_heavy_000.ogg` |
| `sfx/role/player/Rolling.ogg` | `cloth1.ogg` |
| `sfx/shooting/Shooting0001.ogg` | `laserSmall_000.ogg` |
| `sfx/shooting/Shooting0002.ogg` | `laserSmall_001.ogg` |
| `sfx/shooting/Shooting0003.ogg` | `laserLarge_000.ogg` |
| `sfx/shooting/Shooting0004.ogg` | `laserSmall_002.ogg` |
| `sfx/shooting/Shooting0005.ogg` | `laserRetro_000.ogg` |
| `sfx/shooting/Shooting0006.ogg` | `laserSmall_003.ogg` |
| `sfx/shooting/Shooting0007.ogg` | `laserRetro_001.ogg` |
| `sfx/shooting/Shooting0008.ogg` | `laserLarge_001.ogg` |
| `sfx/shooting/Shooting0009.ogg` | `laserRetro_002.ogg` |
| `sfx/shooting/Shooting0010.ogg` | `laserRetro_003.ogg` |
| `sfx/shooting/Shooting0011.ogg` | `explosionCrunch_000.ogg` |
| `sfx/shooting/Shooting0012.ogg` | `laserRetro_004.ogg` |
| `sfx/shooting/Shooting0013.ogg` | `laserSmall_004.ogg` |
