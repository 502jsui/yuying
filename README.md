# 余音 / yuying

Unity 单机叙事游戏。当前可玩的 Demo 在 `scenes-and-rain-demo` 分支。

## 开发环境

- Unity Editor：`2022.3.62f3`
- 版本控制：Git + Git LFS
- 远程仓库：<https://github.com/502jsui/yuying>
- 当前开发分支：`scenes-and-rain-demo`

## 首次获取项目

1. 接受仓库协作者邀请。
2. 安装 [Git LFS](https://git-lfs.com/)。
3. 克隆时直接切到开发分支：

   ```bash
   git lfs install
   git clone -b scenes-and-rain-demo https://github.com/502jsui/yuying.git
   ```

4. 用 Unity Hub 添加该目录，使用 Unity `2022.3.62f3` 打开，等待导入完成。
5. 打开 `Assets/Scenes/MainMenu.unity`，确认 Console 没有报错。

## 场景与操作

| 场景 | 用途 |
| --- | --- |
| `MainMenu` | 启动场景（Build Settings 第 0 个） |
| `FirstFloor` / `Backyard` | 游戏场景，可来回切换 |
| `Pause` | 暂停界面（背景是暂停瞬间的截图） |
| `Settings` / `Credits` | 设置 / 制作名单 |

| 操作 | 按键 |
| --- | --- |
| 移动 | `A` / `D` 或左右方向键 |
| 触碰物品 / 住户 | `E` |
| 场景门口切换 | `F`（站在门范围内才生效） |
| 独白逐句推进 / 跳过当前句 | `Space` |
| 暂停 | `Esc` |
| 调试面板 | `F1` |

## 代码地图

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Core/GameRoot.cs` | 常驻服务根：配置、本局数据、系统初始化 |
| `Assets/Scripts/Core/GameFlow.cs` | 顶层状态 + 每轮五阶段的时间轴 |
| `Assets/Scripts/Core/GameSession.cs` | 一局数据（住户 / 物品 / 轮次 / 次数） |
| `Assets/Scripts/Core/ItemSystem.cs` | 物品池、温度、玩家触碰、区域占用 |
| `Assets/Scripts/Core/NpcSystem.cs` | 住户行动决策、拿取 / 交流 / 移动、回家判定 |
| `Assets/Scripts/Core/StatSettlement.cs` | 暖意 / 知晓的唯一加减出口 |
| `Assets/Scripts/Views/` | 场景表现：相机、玩家、物品、住户、雨、提示条 |
| `Assets/Scripts/UI/` | 主菜单、设置、暂停 HUD 等 UI 转发 |
| `Assets/Scripts/Editor/` | 场景 / 美术 / 配置生成、自检、结局分布模拟 |

更细的说明看 `Docs/`：`数据流.md`、`框架说明.md`、`物品系统说明.md`、
`NPC系统说明.md`、`数值结算与玩家操作.md`、`暂停说明.md`、`UI界面说明.md`。

## 开发工具

- 游戏内 `F1`：查看数值 / 强制推进阶段 / 保存与读取调试快照。
- `Tools → 余音 → 自检`：只读检查 Build Settings、配置、预制体、场景接线。
- `Tools → 余音 → 结局分布模拟`：无画面跑多局，输出结局分布。

批处理示例（按需替换 Unity 路径）：

```powershell
# EditMode 测试
Unity.exe -batchmode -nographics -projectPath "E:\unity\咕咕嘎嘎" `
  -runTests -testPlatform EditMode -testResults "edit-tests.xml"

# PlayMode 测试
Unity.exe -batchmode -nographics -projectPath "E:\unity\咕咕嘎嘎" `
  -runTests -testPlatform PlayMode -testResults "play-tests.xml"

# 场景接线自检
Unity.exe -batchmode -nographics -quit -projectPath "E:\unity\咕咕嘎嘎" `
  -executeMethod YuyinSelfCheckTool.RunFromCommandLine
```

## 协作流程

- 功能分支从 `scenes-and-rain-demo` 切出（`feat/*`、`fix/*`、`art/*`、`docs/*`），完成后向该分支提 Pull Request。
- `main` 只作为历史稳定分支，不直接在上面开发。
- 提交前关闭 Unity，确认 Console 无报错，并运行受影响的场景 / 测试。
- 不要提交 `Library`、`Temp`、`Logs`、`UserSettings`、构建产物或 IDE 临时文件。
- `Assets/Unity.VisualScripting.Generated/` 是 Unity 生成物，已在 `.gitignore` 中忽略。
- 图片、音频、模型、视频和字体由 Git LFS 管理。
