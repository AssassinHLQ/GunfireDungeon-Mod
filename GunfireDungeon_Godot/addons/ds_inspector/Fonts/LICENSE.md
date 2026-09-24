# 本目录字体 LT 授权说明 / Font License

本目录里的 **`WenQuanYiMicroHei.ttf`（文泉驿微米黑，4.41 MB）** 是第三方字体，
不是本项目创作的。本文档按 **Apache License 2.0 第 4 条** 的要求提供许可证全文与版权声明。

> ⚠️ **这份字体只服务于开发用的节点检查器（DsInspector）**，玩家正常游玩时看不到它。
> 但它**确实会被打进发行包**（`DebugFont.tres` 引用它，导出预设是 `all_resources`）。
> 想彻底去掉它：在 `export_presets.cfg` 的 `exclude_filter` 里加 `addons/*`
> —— 那样顺带也能干掉那个"秒杀全部敌人"的开发作弊入口，一举两得。

---

## 1. 字体与用途

| 项目 | 内容 |
|---|---|
| 文件 | `WenQuanYiMicroHei.ttf` |
| 名称 | 文泉驿微米黑 / WenQuanYi Micro Hei |
| 版本 | 0.2.0-beta（Debian 包 `fonts-wqy-microhei` 的 0.2.0-beta-3.1）|
| 上游 | <https://sourceforge.net/projects/wqy/files/wqy-microhei/> |
| 项目主页 | <http://wenq.org/> |
| 本仓库内的用途 | `addons/ds_inspector/DebugFont.tres` → DsInspector 调试面板（**开发工具，非游戏内容**）|

---

## 2. 授权：Apache-2.0 或 GPL-3+ with Font exception（双授权，任选其一）

上游对这份字体给了**两条可选授权**，使用者可任选一条遵守。
**本项目选择 Apache License 2.0。**

- ✅ **Apache License 2.0** —— <https://www.apache.org/licenses/LICENSE-2.0>
  全文见同目录 **`LICENSE-APACHE-2.0.txt`**（Apache-2.0 §4(a) 要求的"给接收者一份许可证副本"）
- 另一条（本项目**未**选用，仅记录）：GPL-3+ with Font exception

### 版权声明（Apache-2.0 §4(c) 要求保留）

```
WenQuanYi Micro Hei (文泉驿微米黑)
Copyright 2008-2009, The WenQuanYi Project Board of Trustees
Copyright 2008-2009, Qianqian Fang [FangQ] <fangq@nmr.mgh.harvard.edu>
Copyright 2008-2009, mozbug <mozbugbox@yahoo.com.au>

Based on Google Droid font family shipped in Android SDK (1.0),
including Droid Sans Fallback, Droid Sans and Droid Sans Mono:
  Copyright 2006-2008, Google Corporation
  Copyright 2006-2008, Steve Matteson, Ascender Corp.

Some CJK punctuations were imported from Ume-Font:
  Copyright 2003-2004, Electronic Font Open Laboratory (/efont/)
  Copyright 1990-2003, Wada Laboratory, the University of Tokyo
```

> 版权信息取自 Debian 官方打包元数据（`fonts-wqy-microhei` 的 `debian/copyright`）：
> <https://metadata.ftp-master.debian.org/changelogs//main/f/fonts-wqy-microhei/fonts-wqy-microhei_0.2.0-beta-3.1_copyright>

### 本项目做的修改

**没有修改。** 字体文件按上游原样使用，未做任何子集化、改名或字形改动；
`WenQuanYiMicroHei.ttf` 的字节与原包一致。（Apache-2.0 §4(b) 只在"修改过"时才要求标注改动。）

---

## 3. 关于原来那个 `License.url`

本目录里原本有一个 **`License.url`，指向 `http://www.webfontfree.com/`** ——
那是 DsInspector 插件作者附带的，**那是个字体下载站，不是授权页面**，
按它拿不到任何可用的授权信息。**已删除**，换成这份说明 + Apache-2.0 全文。

> 这也是之前审计里发现的问题：**一份 4.41 MB 的第三方字体随包发布，
> 却没有任何许可证文本或版权声明** —— Apache-2.0 §4 明确要求这两样。
