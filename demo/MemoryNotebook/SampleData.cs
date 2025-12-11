using DocUI.Demo.MemoryNotebook.Model;

namespace DocUI.Demo.MemoryNotebook;

/// <summary>
/// 示意数据工厂 - 填充一些示例内容来演示 LOD 机制
/// 
/// 这些数据模拟了 LLM Agent 在工作中可能积累的各种信息片段
/// </summary>
public static class SampleData
{
    public static Notebook CreateSampleNotebook()
    {
        var notebook = new Notebook();

        // === 1. 项目概览（典型的 Summary 状态）===
        notebook.Add(new ContentNode
        {
            Id = "pipemux-overview",
            Title = "PipeMux 项目概览",
            Source = ContentSource.Static,
            Tags = ["project", "pipemux"],
            CurrentLod = LodLevel.Summary,
            Content = new LodContent
            {
                Gist = "PipeMux: 本地进程编排框架",
                Summary = """
                    PipeMux 是一个本地进程编排框架，通过 Named Pipe 实现 CLI ↔ Broker ↔ App 的通信。
                    
                    核心组件：
                    - Broker: 后台服务，管理 App 生命周期
                    - CLI: 命令行入口，支持 `:list/:ps/:stop/:help` 管理命令
                    - SDK: App 开发框架，基于 System.CommandLine + StreamJsonRpc
                    
                    状态：Tier 1 核心稳定，E2E 测试通过
                    """,
                Full = """
                    # PipeMux 项目概览

                    ## 定位
                    PipeMux 是 ATELIA 计划中的进程编排层，为 LLM Agent 提供与本地应用交互的能力。

                    ## 架构
                    ```
                    CLI ──Named Pipe──▶ Broker ──stdin/stdout──▶ App
                         (JSON-RPC)              (JSON-RPC)
                    ```

                    ## 核心组件

                    ### Broker (PipeMux.Broker)
                    - 后台服务进程
                    - 监听 Named Pipe 接收 CLI 请求
                    - 管理 App 进程的生命周期（启动、复用、关闭）
                    - 支持配置文件定义 App 注册

                    ### CLI (PipeMux.CLI)
                    - 用户/Agent 的入口点
                    - 语法: `pmux <app> <cmd> [args]` 或 `pmux :<mgmt-cmd>`
                    - 管理命令: `:list`, `:ps`, `:stop`, `:help`

                    ### SDK (PipeMux.Sdk)
                    - App 开发框架
                    - 基于 System.CommandLine 定义命令
                    - 基于 StreamJsonRpc 处理通信
                    - 有状态服务支持

                    ## 配置
                    位置: `~/.config/pipemux/broker.toml`
                    
                    ```toml
                    [apps.calculator]
                    command = "dotnet run --project ..."
                    timeout = 30
                    ```

                    ## 测试状态
                    - E2E 脚本: 8/8 通过
                    - 管理命令: 7/7 通过
                    """
            }
        });

        // === 2. 代码片段（折叠到 Gist）===
        notebook.Add(new ContentNode
        {
            Id = "code-lod-enum",
            Title = "LodLevel 枚举定义",
            Source = ContentSource.Static,
            Tags = ["code", "docui"],
            CurrentLod = LodLevel.Gist,
            Content = new LodContent
            {
                Gist = "LodLevel 枚举: Gist/Summary/Full 三级",
                Summary = """
                    LodLevel 定义了信息的三个详略级别：
                    - Gist (0): 最小印象，一句话
                    - Summary (1): 摘要，保留关键信息
                    - Full (2): 完整内容
                    """,
                Full = """
                    ```csharp
                    public enum LodLevel
                    {
                        /// <summary>
                        /// 最小印象 - 一句话标识
                        /// </summary>
                        Gist = 0,

                        /// <summary>
                        /// 摘要级别 - 保留关键信息，日常工作状态
                        /// </summary>
                        Summary = 1,

                        /// <summary>
                        /// 完整内容 - 全部细节
                        /// </summary>
                        Full = 2
                    }
                    ```
                    """
            }
        });

        // === 3. 会话笔记（完全展开）===
        notebook.Add(new ContentNode
        {
            Id = "session-note-1209",
            Title = "2025-12-09 会话笔记",
            Source = ContentSource.UserInput,
            Tags = ["note", "session"],
            CurrentLod = LodLevel.Full,
            Content = new LodContent
            {
                Gist = "12/09: PipeMux 管理命令 + DocUI 概念原型",
                Summary = """
                    今日完成：
                    1. PipeMux 管理命令实现 (`:list/:ps/:stop/:help`)
                    2. pmux wrapper + Broker 自动启动
                    3. atelia-sdk 目录结构
                    4. DocUI LOD 机制调研（三模型采样）
                    
                    下一步：MemoryNotebook 概念原型
                    """,
                Full = """
                    # 2025-12-09 会话笔记

                    ## 完成任务

                    ### PipeMux 管理命令
                    - RFC 撰写 + 多模型采样决策
                    - 最终语法: `pmux :<cmd>` 前缀
                    - 实现: `:list`, `:ps`, `:stop`, `:help`
                    - QA 验证: 7/7 E2E 通过

                    ### 部署结构
                    - 创建 atelia-sdk 目录
                    - pmux wrapper 实现 Broker 自动启动
                    - 环境变量: ATELIA_HOME, PATH

                    ### DocUI LOD 调研
                    - 三模型并行调研: Investigator, CodexReviewer, GeminiAdvisor
                    - 关键洞察: 对 LLM，折叠必须是内容替换，不是视觉隐藏
                    - Ghost 锚点 + 微流程设计

                    ## 决策记录
                    - "快速胜利优先" 规则添加适用边界
                    - 确认 {Gist, Summary, Full} 三级 LOD

                    ## 下一步
                    - MemoryNotebook 概念原型
                    - TextEditor + ResourceMonitor 演示
                    """
            }
        });

        // === 4. 外部文件摘要（典型的静态源）===
        notebook.Add(new ContentNode
        {
            Id = "file-lead-metacognition",
            Title = "lead-metacognition.md 文件摘要",
            Source = ContentSource.Static,
            Tags = ["file", "agent-team"],
            CurrentLod = LodLevel.Summary,
            Content = new LodContent
            {
                Gist = "Team Leader 元认知文件，定义身份与工作方法",
                Summary = """
                    AI Team Leader 的元认知文件，包含：
                    - 身份定位：以外部记忆文件为本体的状态机
                    - 工作范围：focus 生态（PieceTreeSharp, PipeMux, DocUI, atelia）
                    - Specialist 体系：7 个专员，按模型×行为模式划分
                    - 记忆策略：头脑/认知核心/档案柜三层
                    
                    核心原则："我记故我在" + 行为主义认知框架
                    """,
                Full = "(完整文件内容约 15KB，包含详细的自我认知、工作方法、经验积累)"
            }
        });

        // === 5. 购物清单（生活杂事，平时折叠）===
        notebook.Add(new ContentNode
        {
            Id = "shopping-list",
            Title = "购物清单",
            Source = ContentSource.UserInput,
            Tags = ["life", "todo"],
            CurrentLod = LodLevel.Gist,
            Content = new LodContent
            {
                Gist = "该买鸡蛋了",
                Summary = """
                    待购物品：
                    - 🥚 鸡蛋 (紧急)
                    - 🥛 牛奶
                    - 🍞 面包
                    """,
                Full = """
                    # 购物清单

                    ## 紧急
                    - [ ] 鸡蛋 - 冰箱里只剩 2 个了

                    ## 本周
                    - [ ] 牛奶 - 周三到期
                    - [ ] 面包 - 全麦的
                    - [ ] 水果 - 苹果或橙子

                    ## 下次顺便
                    - [ ] 洗洁精
                    - [ ] 垃圾袋
                    """
            }
        });

        return notebook;
    }
}
