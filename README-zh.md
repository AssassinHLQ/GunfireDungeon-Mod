[**English**](./README.md)  | [**中文简体**](./README-zh.md)

---

# 先读这里

### 本仓库是魔改版，不是原版

**想找原版请去 [xlljc/GunfireDungeon](https://github.com/xlljc/GunfireDungeon)。**

| | 原版 | 本仓库 |
|---|---|---|
| **仓库** | [xlljc/GunfireDungeon](https://github.com/xlljc/GunfireDungeon) | [AssassinHLQ/**GunfireDungeon-Mod**](https://github.com/AssassinHLQ/GunfireDungeon-Mod) |
| **作者** | 小李xlxl | 由 klhuyjnvbnvbnb 修改 |
| **性质** | 原作者官方版本 | **在原作者基础上魔改** |
| **地牢层数** | 单层 | **10 层循环，到出口进下一层** |
| **难度** | 固定 | **随层数递增** |
| **键位设置** | 无 | **有，17 个动作可改键** |
| **像素字体** | 内附试用版字体 | **方舟像素字体 + 缝合像素字体（均 SIL OFL 1.1）** |

> **两者均以 [AGPL v3](LICENSE) 协议发布**，原项目版权归原作者所有。
>
> - **原作者：** 小李xlxl — https://space.bilibili.com/259437820
> - **修改者：** [klhuyjnvbnvbnb](https://space.bilibili.com/1463614316)（哔哩哔哩）/ [AssassinHLQ](https://github.com/AssassinHLQ)（GitHub）
> - **完整改动列表：** 见下方 [修改说明](#修改说明)，或游戏内主菜单的「**修改说明**」按钮。

---

# 关于下载渠道（请务必先读）

## 本作者从未公开发布过任何打包版本

本仓库**只提供源代码**，**不发布任何可执行文件、压缩包或安装程序**。
没有 Release 附件，没有网盘链接，没有任何 `.exe`。

**因此：**

- 任何声称是本游戏"下载"的 **exe / 安装包 / 免安装版 / 网盘资源，都不是作者发布的**
- 这类文件**不受作者控制**，可能被植入**木马、挖矿程序、勒索软件或盗号代码**
- 作者**无法也从未**对第三方打包版本的内容做任何检查、担保或背书

**目前唯一可运行的方式是自行从源码编译**（见下方 [怎么运行](#怎么运行)）。

本项目**尚未上架任何平台**；如果将来上架，会先在本仓库说明。

> ### 免责声明
>
> 作者**仅提供源代码**。任何第三方自行打包、修改、再分发的版本，**均与作者无关**。
> **若因使用非官方渠道获取的可执行文件而造成任何损失，作者不承担责任。**
> 请自行核实来源，风险自负。
>
> 如果你发现有人以本游戏名义传播不明来源的可执行文件，欢迎在
> [Issues](https://github.com/AssassinHLQ/GunfireDungeon-Mod/issues) 里告知。

---

主分支长时间不更新？请切换到[develop](https://github.com/xlljc/GunfireDungeon/tree/develop)分支查看最新代码



## 一款由Godot开发的地牢射击类型的游戏

**Godot版本：** `4.7.1 mono`（由上游 `4.4 mono` 升级而来）

**.Net版本：** `9.0`

---
### 游戏定义

**游戏名称：**《建筑学院：1999》

**原名：**《枪火地牢 GunfireDungeon》 —— 本项目是它的修改版

**英文名称：**《Architecture School: 1999》

**美术风格：** 2D像素

**游戏标签：** Roguelite，俯视角，地牢探索，双摇杆射击

**参考游戏：** 《挺进地牢》，《Noita》

**核心简介：** 游戏整体流程由数层地牢组成，每层又由数个房间组成，每个房间有一堵门隔开，玩家每进入一个房间，需要清理房间内所有的敌人，方可离开和进入下一个房间，玩家需要在这些房间中探索，战斗，收集掉落的道具和被动，一步步成长，击败boss，进入下一层，如此往复，直到击败最后一层boss即可通关。

**游戏背景：** 构思中

**游戏内置了一个功能强大的地图编辑器，方便玩家自己制作地图和分享地图**

---
### 预览图

##### 游戏中

![gif](GunfireDungeon_Document/文档资源/preview0.png)
![png](GunfireDungeon_Document/文档资源/preview1.png)

##### 地图编辑器

房间管理器

![png](GunfireDungeon_Document/文档资源/preview2.png)

房间地形编辑

![png](GunfireDungeon_Document/文档资源/preview3_gif.gif)

房间装饰编辑

![png](GunfireDungeon_Document/文档资源/preview2_gif.gif)

房间预设编辑

![png](GunfireDungeon_Document/文档资源/preview3.png)

---
### 如何运行

本仓库**自包含** —— 只需要下载这个仓库，不需要再去下载原作者的仓库。

> 必须使用 **`Godot 4.7.1 .NET（Mono）版`**，**标准版无法运行**（本项目是 C# 工程，目标框架 `net9.0`）。

1. 安装 **Godot 4.7.1 .NET 版** —— [godotengine.org/download](https://godotengine.org/download)（注意要选 **.NET** 那个）
2. 安装 **.NET SDK 9.0 或更新版本** —— [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0)
3. 用 Godot 4.7.1 .NET 打开 `GunfireDungeon_Godot/project.godot`
4. 首次打开时编辑器会自动导入资源并编译 C# 工程（大约需要一两分钟）
   - 如果弹出启用插件的提示，启用后重启
   - **需要启用的插件：** `ds_inspector`、`ds_ui`、`game_plugin`、`node_presetting`
5. 按 **F5** 运行

**常见问题**

| 现象 | 原因与解决办法 |
|---|---|
| `Failed to create an autoload, script '.../InitUiManager.cs' is not compiling` | C# 还没编译。等编辑器编译完，或先执行一次 `dotnet build GunfireDungeon_Godot/GunfireDungeon.csproj`，再重新打开。 |
| `Index p_index = 2 is out of bounds` + `Cannot call method 'add_child' on a null value` | 上游自带的编辑器警告，来自 `node_presetting` 插件，**不影响游戏运行**。 |
| 提示升级工程格式 | 本工程已是 Godot 4.7 格式，无需升级。 |

---

### 目录结构

> ├ GunfireDungeon_Document (更新日志相关的目录) 
>
> └ GunfireDungeon_Godot (Godot工程目录)

**godot下载地址：** [https://godotengine.org/download](https://godotengine.org/download)

**.net9下载地址：** [https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)

---
### 其他

**开发日志：** [开发日志.md](GunfireDungeon_Document/开发日志.md) 

**原作者哔哩哔哩：** https://space.bilibili.com/259437820

**修改者哔哩哔哩：** https://space.bilibili.com/1463614316

**修改者 GitHub：** https://github.com/AssassinHLQ

**项目引用插件：**

* Ds_Ui：https://github.com/xlljc/Ds_Ui
* godot-node-presetting：https://github.com/DeerLuuu/godot-node-presetting

---

## 修改说明

本节列出本修改版相对原项目的全部改动，用于满足 **AGPL v3 第 5 条**「显著标注修改」的要求。

同样的内容在**游戏内**也有 —— 主菜单的「**修改说明**」按钮。

### 一、流程与玩法

* 新增**楼层循环系统**，地牢共 **10 层**
* 到达出口**进入下一层**，不再直接通关
* 通关条件改为**打通第 10 层**
* **跨层保留**玩家血量、护盾、武器、道具
* **敌人血量按层数递增**，每层提升 16%
* 进入新层时弹出提示
* 顶部新增**当前层数显示**

### 二、界面与显示

* 游戏改为**全屏显示**，等比缩放
* **主菜单字号放大至三倍**
* **设置面板字号放大至四倍**
* 新增**键位设置**，可自定义 17 个操作按键
* **键位设置支持鼠标按键**（左键 / 右键 / 中键 / 侧键）
* 键位保存在存档中，重新启动后也会保留
* 改键时按 **ESC 取消**，鼠标右键可以正常作为待绑定的键
* 支持一键**恢复默认键位**，马上生效，不需要重启
* 文字加入**深色描边**，在明亮的背景上也能看清
* **图册的物品名称与说明放大**，长说明自动换行
* **ESC 菜单新增石质底板**，与主菜单、设置菜单风格统一
* **新增界面音效**：按钮点击、鼠标悬停、勾选框开关、滑块松手
* 界面音效走 SFX 总线，音量由设置里的「音效音量」统一控制

### 三、字体与界面素材

* 替换为 **[方舟像素字体](https://github.com/TakWolf/ark-pixel-font)**（`ArkPixel-12px-zh_cn.ttf`），采用 **SIL OFL 1.1** 协议 —— 授权文件见 `resource/font/ArkPixel-LICENSE.md`
* 方舟像素字体 12px 只有 24471 个字形，**缺「筑 / 廊 / 恐 / 滚 / 奖」等一批常用字**（游戏标题的「筑」正好缺失），因此**追加[缝合像素字体](https://github.com/TakWolf/fusion-pixel-font)作为回退字体**（`FusionPixel-12px-zh_hans.ttf`，36558 个字形，同为 12px 设计、同一作者、**SIL OFL 1.1**）—— 授权文件见 `resource/font/FusionPixel-OFL.txt`
* 两者由 `resource/font/GameFont.tres` 组合成「主字体 + 回退字体」，**主字体的排版度量完全不变**，缺字自动用回退字体渲染，依然是像素风格而不会变成系统字体
* 调整像素字体导入参数（抗锯齿 / hinting / 次像素定位），保证放大后字形锐利
* 字号统一调整为**基准字号 12 的整数倍**
* **主菜单 / ESC 菜单 / 设置菜单改用新的界面素材**（边框、滑块）
* 界面素材来自 **[Kenney](https://kenney.nl)**（`Fantasy UI Borders`、`UI Pack`），采用 **CC0 1.0** 协议 —— 授权文件见 `resource/sprite/ui/fantasyBorder/LICENSE.md`
* **主菜单新增动态视差背景**：地牢石墙大厅上有三扇等大的拱形开口，透出黄昏天空与远山；分三层视差（天空最慢、远山其次、石墙静止）
* 背景的天空与云来自 **[ansimuz](https://ansimuz.com)**（`Sunny Land 2D Pixel Art Pack`），采用 **CC0 1.0** 协议
* 背景的远山与石墙为本修改版**程序化生成**，砖块配色取自原版自带的 `TileSet1` —— 授权说明见 `resource/sprite/ui/mainBackground/LICENSE.md`
* **界面音效来自 [Kenney](https://kenney.nl) 的 `Interface Sounds`**，采用 **CC0 1.0** 协议 —— 授权说明见 `resource/sound/ui/LICENSE.md`
* **全部游戏音效替换为 [Kenney](https://kenney.nl) 的 CC0 音效**（`Impact Sounds`、`Sci-Fi Sounds`、`RPG Audio`）—— 授权与完整映射表见 `resource/sound/LICENSE.md`
* 风格取舍：**Kenney 没有写实枪声**，枪声改用能量武器音（激光类）

### 四、问题修复

* 修复设置面板超出屏幕、内容溢出到面板外的问题
* 修复物品图册中部分条目缺少图标导致的报错
* 修复个别汉字在像素字体中缺字、显示为方框的问题；统一房间类型名称格式为「XX房间」
* 修复**开火、冲刺等鼠标按键显示为「未设置」**的问题
* 修复**改键时点击鼠标没有反应**的问题
* 修复**改键时按 ESC 取消，ESC 反而被绑成新键位**的问题（若已误绑，按一次「恢复默认键位」就能复原）

### 五、其他

* 清理无用的导入缓存与失效常量

---

> 本说明仅为改动记录。**原项目版权归原作者所有。**

