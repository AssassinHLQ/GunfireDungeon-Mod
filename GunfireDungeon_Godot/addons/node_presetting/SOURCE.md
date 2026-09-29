# node_presetting 来源与授权

- 插件名称：node_presetting
- 作者：DeerLuuu
- 来源仓库：https://github.com/DeerLuuu/Godot-node-presetting
- 许可证：MIT License
- 许可证文件：本目录 `LICENSE`
- 版权声明：Copyright (c) 2025 DeerLuuu
- 商用：允许
- 修改：允许
- 再分发：允许，但须保留 MIT 许可证和版权声明
- 当前状态：已保留许可证；因 Godot 4.7.1 Inspector 兼容性错误而禁用
- 错误位置：`node_presetting.gd:138`
- 核查日期：2026-09-29

## 功能说明

这是一个 Godot 编辑器插件，用于在 Inspector 中创建、删除和读取节点属性预设。它主要影响编辑器操作，不是游戏运行时的核心功能。

## 兼容性记录

Godot 4.7.1 中出现 `inspector.get_child(2).add_child(_menu_button)` 相关错误：Godot Inspector 的内部子节点结构与插件假设不一致。因此暂时禁用，保留插件文件和许可证，不在正式运行或发行流程中启用。
