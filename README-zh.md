[**English**](./README.md)  | [**中文简体**](./README-zh.md)

> ## ⚠️ 本仓库为修改版
>
> 本仓库是 [**xlljc/GunfireDungeon**](https://github.com/xlljc/GunfireDungeon) 的**修改版**。
> 原项目与本修改版**均以 [AGPL v3](LICENSE) 协议发布**，原项目版权归原作者所有。
>
> - **原作者：** 小李xlxl — https://space.bilibili.com/259437820
> - **修改者：** klhuyjnvbnvbnb（哔哩哔哩）/ [AssassinHLQ](https://github.com/AssassinHLQ)（GitHub）
> - **完整改动列表见下方 [修改说明](#修改说明)。**

主分支长时间不更新？请切换到[develop](https://github.com/xlljc/GunfireDungeon/tree/develop)分支查看最新代码



## 一款由Godot开发的地牢射击类型的游戏

**Godot版本：** `4.4 mono`

**.Net版本：** `9.0`

---
### 游戏定义

**游戏名称：**《枪火地牢》

**英文名称：**《Gunfire Dungeon》

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
### 启动项目

git仓库的目录结构如下
> ├ GunfireDungeon_Document (更新日志相关的目录) 
>
> └ GunfireDungeon_Godot (Godot工程目录)



请确保安装了`.net9`和`godot mono4.4`

godot下载地址：[https：//godotengine.org/download](GunfireDungeon_Document/文档资源/setting.png)

.net9下载地址：[https：//dotnet.microsoft.com/zh-cn/download/dotnet/9.0](GunfireDungeon_Document/文档资源/setting.png)



使用GodotMono版打开`GunfireDungeon_Godot/project.godot`

并且第一次打开请启用这两个插件：

![setting.png](GunfireDungeon_Document/文档资源/setting.png)



---
### 其他

**开发日志：** [开发日志.md](GunfireDungeon_Document/开发日志.md) 

**哔哩哔哩：** https://space.bilibili.com/259437820

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
* 键位保存在存档中，重新启动后也会保留
* 支持一键**恢复默认键位**

### 三、字体

* **移除原项目内附带的商业试用版点阵字体**（`DinkieBitmap-*Demo`、`VonwaonBitmap-*`）
* 替换为 **[方舟像素字体](https://github.com/TakWolf/ark-pixel-font)**（`ArkPixel-12px-zh_cn.ttf`），采用 **SIL OFL 1.1** 协议 —— 授权文件见 `resource/font/ArkPixel-LICENSE.md`
* 调整像素字体导入参数（抗锯齿 / hinting / 次像素定位），保证放大后字形锐利
* 字号统一调整为**基准字号 12 的整数倍**

### 四、问题修复

* 修复设置面板超出屏幕、内容溢出到面板外的问题
* 修复物品图册中部分条目缺少图标导致的报错

### 五、其他

* 清理无用的导入缓存与失效常量

---

> 本说明仅为改动记录。**原项目版权归原作者所有。**

