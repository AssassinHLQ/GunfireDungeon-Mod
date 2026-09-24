# 邪恶法师 Evil Wizard —— 素材授权 / 使用说明

本目录是主菜单之外**唯一一处第三方角色素材**，用于「邪恶法师」精英怪
（`wizard0001`，预制体 `prefab/role/enemy/EvilWizard.tscn`）。

---

## 授权

| 项目 | 内容 |
|---|---|
| 素材包 | **Evil Wizard Asset Pack** |
| 作者 | **Luiz Melo**（LuizMelo）· <https://luizmelo.artstation.com> |
| 授权 | **CC0 1.0 Universal（公有领域）** |
| 原文 | *"This pack - Evil Wizard Asset Pack is Creative Commons Zero (CC-0). Can be used in commercial and non-commercial projects."* |
| 授权文件 | 同目录 `LICENSE.txt`（原包附带，原样保留） |
| 署名 | **CC0 不要求** —— 但**本项目主动署名**，见下 |
| 可否入库 | ✅ **可以**。CC0 不限制再分发 —— 和 CraftPix 那 9 套主菜单背景不同，这些图**正常提交进 git** |

### 本项目主动使用的署名文字

CC0 不要求署名，但署名是对作者的尊重，也让上架材料的来源链更完整。
**游戏内「修改说明」浮层里已经有这一行**（`src/game/ui/game/main/ChangelogOverlay.cs`）：

```
Evil Wizard Asset Pack — Luiz Melo (LuizMelo), CC0 1.0
```

### 作者名是怎么确认的（留个证据链）

素材包自带的 `License.txt` **只写了 CC0，没有写作者**。作者名是从
**Luiz Melo 自己的 Unity Asset Store 发行者页面**确认的 —— 同系列的
*Evil Wizard 2* / *Evil Wizard 3* 都挂在这个发行者名下：

- 发行者页面：<https://assetstore.unity.com/publishers/34852>
- 同系列条目：<https://assetstore.unity.com/packages/2d/characters/evil-wizard-2-284501>

（构建这台机器访问不了 itch.io，所以没能直接引 itch 页面。补证据的话，
建议翻自己的 itch 下载记录，把 **页面标题 + 作者 + License 一栏** 截图存档。）

> ⚠️ **注意区分**：本仓库里这份素材的授权依据是**包内 `LICENSE.txt` 的 CC0**，
> 不是 Unity Asset Store 的 Standard EULA。同一套美术在两个平台上架、两套授权，
> 别混用。

---

## 文件对照表

原包只有 5 张横向图集（`Idle/Move/Attack/Death/Take Hit`），每帧 150×150。

| 本项目文件 | 原文件 | 帧数 | 用在哪 |
|---|---|---|---|
| `WizardIdle.png` | `Idle.png` | 8 | `idle` |
| `WizardMove.png` | `Move.png` | 8 | `run` + `reverseRun`（帧序倒过来）|
| `WizardAttack.png` | `Attack.png` | 8 | `attack`（施法）|
| `WizardDeath.png` | `Death.png` | 5 | `die` |
| `Wizard_Icon.png` | 由 `Idle.png` 第 0 帧裁切缩放而来 | — | 图鉴 / 商店图标 |
| ~~`WizardTakeHit.png`~~ | `Take Hit.png` | 4 | ❌ **未使用** |

**本项目的改动**：只改了文件名，图本身没动。

### 为什么 `Take Hit` 没接

游戏的 AI 状态机（`AiAttackState.MoveHandler`）**每一帧**都会
`AnimatedSprite.Play(idle/run)`，受击动画会被立刻覆盖掉。
要接就得引入"受击期间锁住动画"的机制，收益不值这个复杂度，所以先不做。
将来要做：把 `Take Hit.png` 一并拷进来，在 `WizardSpriteFrames` 里加一条 `hit`，
再处理状态机覆盖问题。

---

## 一个坑：格子是 150，角色只有 32×55

素材每帧是 150×150 的格子，但角色内容只占 **32×55**，四周全是空白。
所以必须靠 `WizardSpriteFrames.SpriteOffset`（`(-71, -101)`，量的"身体中心 + 脚底"）
把角色挪到节点原点上，否则它会整体偏到格子的右下角。

另外 `AiTemplate.tscn` 里 `AnimatedSprite` 自带 `position = (0, -8)`（给 16×24 小怪留的），
`WizardEnemy.OnInit()` 会把它清零再自己对齐。
