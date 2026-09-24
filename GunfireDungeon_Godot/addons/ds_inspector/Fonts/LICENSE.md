# 本目录已不再包含第三方字体

**2026-09-20 起，本目录不再放字体文件。**

原来这里有一份 **`WenQuanYiMicroHei.ttf`（文泉驿微米黑，4.41 MB）**，
是 DsInspector 插件作者附带的，只给调试面板用。它有两个问题：

1. **白占 4.41 MB** —— 玩家正常游玩看不到调试面板，却要为它多背一份中文字体；
2. **授权义务** —— 上游是 **Apache-2.0 或 GPL-3+ with Font exception**（双授权）。
   本项目选 Apache-2.0 就得按 §4(a)(c) 随附许可证全文 + 保留版权声明，
   而插件作者附带的 `License.url` 指向 `http://www.webfontfree.com/`
   （一个字体下载站，不是授权页），等于授权信息是缺失的。

**处理方式：不补授权，直接不用它。**

调试面板改成复用游戏自己的字体 ——
`addons/ds_inspector/DebugFont.tres` 现在指向
`resource/font/ArkPixel-12px-zh_cn.ttf`（主）+ `FusionPixel-12px-zh_hans.ttf`（回退），
和 `resource/font/GameFont.tres` 完全一样的组合。

**收益**：省 4.41 MB；**Apache-2.0 那条义务整个消失**；
调试面板的字形覆盖和游戏本体一致，不会出现缺字方框。
代价：调试面板的观感从"系统黑体"变成"像素字体" —— 对开发工具来说无所谓。

> 想看被删掉的那份字体的完整授权调研与版权声明，翻 git 历史：
> commit `ef4ab18`（当时补了 Apache-2.0 全文，后来换成这个方案又删掉了）。
