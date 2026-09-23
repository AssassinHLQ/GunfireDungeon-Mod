# 邪恶法师 Evil Wizard —— 素材授权 / 使用说明

本目录是主菜单之外**唯一一处第三方角色素材**，用于「邪恶法师」精英怪
（`wizard0001`，预制体 `prefab/role/enemy/EvilWizard.tscn`）。

---

## 授权

| 项目 | 内容 |
|---|---|
| 素材包 | **Evil Wizard Asset Pack** |
| 授权 | **CC0 1.0 Universal（公有领域）** |
| 原文 | *"This pack - Evil Wizard Asset Pack is Creative Commons Zero (CC-0). Can be used in commercial and non-commercial projects."* |
| 授权文件 | 同目录 `LICENSE.txt`（原包附带，原样保留） |
| 署名 | **不需要**（本项目仍在此主动记录来源） |
| 可否入库 | ✅ **可以**。CC0 不限制再分发 —— 和 CraftPix 那 10 套主菜单背景不同，这些图**正常提交进 git** |

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
