# SegmentSnapshotDev Log

## 2025-11-20T00:00Z 初始记录
- 阅读 `AGENTS.md`，确认当前阶段为架构设计与挖坑，任务聚焦文本快照实现。
- 目标：扩展 `SegmentSnapshot` 以支持多段行结构、零拷贝字符访问及 chunk 化构建流程。
- 计划：
  1. 梳理现有 `SegmentSnapshot` 结构与 `ITextSnapshotLine` 约束。
  2. 设计 `LineSegment`/`SegmentLegend` 数据模型与 legend 汇总逻辑。
  3. 实现 chunk 工厂与行级 segment 管理，再更新编辑 API。
  4. 补充单元/手动验证并运行 `dotnet build`。

## 2025-11-20T00:00Z 完成记录
- 引入 `LineSegment`/`SegmentLegend`/legend interner，通过 `SegmentSnapshot.Legends` 汇总去重，同时让 `SegmentSnapshotLine` 暴露 `Segments` 与零拷贝 `Characters` 视图（prefix-sum + binary search）。
- 重写 `SegmentSnapshot` 构建链路（文本/行/内存 chunk），新增 `FromMemoryChunks`，实现 chunk 级渐进解析并正确处理跨 chunk 的 `\r\n`。
- 编辑 API 迁移到 segment 基础（所有 With*/Replace* 方法 + 新的 `ReplaceLineSegments`），`ToString` 与 `Contains` 也基于 segments 拼接。
- 运行 `dotnet build src/DocUI.Text.Abstractions/DocUI.Text.Abstractions.csproj` 通过，产出位于 `src/DocUI.Text.Abstractions/bin/Debug/net9.0/DocUI.Text.Abstractions.dll`。
- 后续建议：1) 为 `FromMemoryChunks`/segment 编辑添加单元测试覆盖 CR/LF/CRLF 边界；2) 评估 legend/marker 元数据与渲染层（例如 MarkerPalette）的对接契约；3) 在 `SegmentSnapshotLine` 上提供针对 legend id 的查找辅助以便渲染器快速映射。
