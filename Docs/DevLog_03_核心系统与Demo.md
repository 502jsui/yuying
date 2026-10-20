# DEV LOG.03 - 核心玩法系统 + 第一份可玩 Demo

日期：2026-10-08 ~ 10-09
本期关键词：框架落地 / 物品系统 / NPC 行为 AI / 数值结算 / 场景拼接 / 雨与屋盖 / 打包 Demo

## 目标

把前两份文档里的规则**串成一条能玩的闭环**，并打出第一份可以在别人机器上跑的 Windows Demo：

> 玩家在一楼醒来 → 碰一件东西 → 住户出来收 / 拿出 / 移动 → 数值变化 → 门缝光与雨声变化 → 6 轮后结算

本期不改玩法方向，重点是**把规则写实、把系统拆干净、把坑填掉**。

## 一、整体分层

```
GameRoot（常驻，自动创建）
 ├─ GameConfig     配置（Resources/GameConfig.asset）
 ├─ RandomService  可复现随机（同 seed 同结果）
 ├─ GameSession    一局数据：住户 / 物品 / 轮次 / 强碰次数
 ├─ StatSettlement 数值结算（所有加减法只在这里）
 ├─ ItemSystem     物品池 / 放置规则 / 温度 / 玩家触碰
 ├─ NpcSystem      住户行为 AI + 状态机
 ├─ SceneStitcher  NPC 行动时拼接一楼 + 后院
 ├─ GameFlow       五阶段回合调度
 ├─ AudioManager / DebugOverlay / PromptOverlay / SceneFadeOverlay
 └─ NpcPhaseDriver 行动阶段演出节奏

场景
 └─ GameManager    场景绑定器：只把本场景 View 接到 GameSession
```

**原则**：数据不在场景里（切场景不丢），系统之间不互相引用（只走 `EventBus`），数值只走 `StatSettlement`。

## 二、逐模块实现

| 模块 | 关键脚本 | 说明 |
| --- | --- | --- |
| 回合调度器 | `GameFlow` `TurnRules` | 五阶段：场景状态 → 玩家输入 → 物品结算 → 住户行为 → 轮次+1；阶段间节奏 `phaseStepDelay` |
| 不回家判定 | `TurnRules` | **先判有没有，再判是谁**（80% 有 1 人 / 20% 全员在家），判定在**住户行动结束之后** |
| 物品系统 | `ItemSystem` `ItemData` `ItemPlacement` `PlacementPoint` | 物品池、放置规则（衣服只能挂 / 花盆可落地可叠 / 木箱只能落地…）、温度、二次触碰 |
| NPC 行为 AI | `NpcSystem` `NpcAction` | 权重 35/25/20/20、行动次数、交流对象、小女孩可在整个拼接场景移动物品 |
| NPC 状态机 | `NpcStateRules` | 正常 / 出门行动 / 区域持续 / 疑惑 / 惊吓，含区域可用性与行动约束 |
| 数值结算 | `StatSettlement` | 所有加减法与阈值集中一处（见下表） |
| 玩家操作 | `PlayerController` `PromptOverlay` | 左右移动、E 触碰、可交互提示、二次触碰提示 |
| 相机 / 场景 | `SceneCameraFollow2D` `ScenePortal2D` `SceneFadeOverlay` | 固定一屏、走向边界切场景、0.25 秒淡出 + 异步加载 |
| 场景拼接 | `SceneStitcher` | NPC 行动时把一楼 + 后院左右无缝拼一起，相机拉远看全两边 |
| 雨 | `RainController` `RainStreakField2D` | 无积水、拼接时铺满全场景、一楼加屋盖（雨落在盖子上） |
| 结局 | `EndingRules` | 8 档判定写成纯函数 + 单测，**本期不接界面**，只在结束时打一行日志 |
| 编辑器工具 | `YuyinSceneBuilder` `YuyinConfigTools` `YuyinItemSceneTool` `YuyinNpcSceneTool` | 重建场景 / 生成配置 / 补物品与放置点 / 补 NPC，全部幂等 |

## 三、数值与规则（当前口径）

| 项目 | 值 |
| --- | --- |
| 总轮数 | 6 |
| 每轮玩家触碰 | 1 次 |
| 每轮场上物品 | 3 件（不够就从池里随机补） |
| 行动权重 | 闲逛 35 / 交流 25 / 拿出 20 / 拿回 20（小女孩移动 20） |
| 行动次数 | 婆婆、大叔 2~3；小女孩 1~2；母亲不行动 |
| 交流对象 | 20% 母亲 / 80% 另一位大人 |
| 拿到东西 | 有温度 +18 暖 / +15 知晓；没温度 0 / +15 |
| 交流 | 普通 +4 / +8；交流母亲 0 / +11 |
| 小女孩移动 | 暖意 +18，并把物品温度清零 |
| 强行触碰 | 前 2 次只警告；第 3 次起 −8 暖并进入惊吓 |
| 强碰传染 | 下次交流时对方暖意 −5 |
| 阈值 | 门缝 30 / 半开 55；高 60；母亲开门 60 / 暖意 61 |
| 母亲 | 暖意整局固定 30；**第 6 轮结束后**判定：知晓 > 60 → 暖意 61 + 一次性收走自己的东西 |

## 四、本期踩到并修掉的坑

### 1. 大叔的模型是个方块
`Resident_Aunt.prefab`（后改名 `Resident_Uncle`）装配错了：子物体叫 `LightSprite`、`bodyRenderer` 是空的，
于是长条占位没生效、显示成 1×1 方块。
→ 修 prefab，并让工具在运行时**自动兜底**（`EnsureBody()` 抓子物体的 SpriteRenderer）+ 幂等修复已有实例。

### 2. NPC 一直"抬高 / 像在飞"
查到最后是：**一楼 NPC 的场景实例位置根本没生效**（用的是 prefab 的 y=0），而后院是 -1.6，两边还不一致。
→ 改成不依赖实例位置：`ResidentView` 用显式的 `groundY` 重建家门口位置，并在刷新时强制回位；
拼接与跨场景全程用同一个地面高度。

### 3. 从后院回一楼 NPC 位置又变了
拼接副本里 `Bind` 把"区域内的局部 X"当成了世界 X。
→ 改成 `SceneStitcher.WorldX(area, x)`，并把相机范围在拼接结束时**硬恢复**成单场景一屏。

### 4. 切场景黑屏、后院进不去
切场景写成了"传送门上的协程：淡出 → 异步加载 → 淡入收尾"，
但加载会销毁旧场景 → 协程宿主没了 → 收尾代码永远不执行 → `IsLoading` 卡在 true、画面全黑、玩家被锁。
→ 收尾改由常驻的 `GameRoot` 侧负责：`SceneFadeOverlay` 监听 `SceneManager.sceneLoaded` 来结束过渡。

### 5. 后院的雨糊到一楼
`RainController` 会把雨场重新对准相机，相机一拉远，雨就铺满整个拼接画面。
→ 拼接时把雨场**钉在场景范围内**；一楼那半边加一个"排除区"（`RainStreakField2D.SetExclusion`）：
雨丝落到屋盖线以下就隐藏，并画一条屋盖（sortingOrder 60）——看起来就是**雨落在盖子上**。

### 6. 轮次卡死，玩家找不到东西碰
按《异常情况》文档：
- **取消"物品不会被第二次拿出"的限制**：收回后回到物品池，可以再被拿出来
- 场上没被碰过的物品 ≤1 件时，**这一轮必有住户拿出物品**（`NpcSystem.ForceTakeOutNext`）

### 7. 触碰判定
从"圆形重叠 + 高度限制"改成：**只看左右距离，高度不限制**，并减去物品自身半宽（站旁边就能碰到）；
可交互提示与实际触碰用同一个判定函数。

### 8. 命名统一
第二位住户确认是**大叔**，`ResidentId.Aunt` / `ItemOwner.Aunt` 全部重命名为 `Uncle`，
prefab 文件也改名（GUID 不变，场景引用不受影响）。

## 五、验证

### 自动化测试

| 套件 | 数量 | 结果 |
| --- | ---: | --- |
| EditMode（框架 / 物品 / NPC / 结算 / 结局） | 108 | 全通过 |
| PlayMode（一轮流程 / NPC 驱动 / 切场景 / 拼接 / 雨） | 16 | 全通过 |

### Demo 构建

```
输出目录：E:\余音_Demo
主程序：  余音Demo.exe
大小：    约 77.4 MB
场景：    FirstFloor(level0) + Backyard(level1)
构建结果：Unity exit code 0
```

无画面启动冒烟测试通过：

```text
[GameRoot] 基础设施初始化完成。
[GameRoot] 新开一局，seed=-1933060245
[GameRoot] 场景已加载：FirstFloor
```

没有脚本异常（无画面模式下的 shader 报错属于正常现象）。

## 六、已知限制

- ✅ **正式 UI 已接**（队友 36ak）：主菜单 / 设置 / 创作团队 / 暂停面板 + 暂停时钟 HUD，音量统一走 `AudioManager`；**结束界面仍未做**
- ✅ **音效已接**：`AudioDirector` 订阅玩法事件统一播音效，`SfxLibrary` 已配齐；雨声断拍也接了
- ⏳ **门缝光仍未接**：`DoorLightView` 组件还在，但两个场景的门还是纯色块（0 引用）
- **美术是占位**：玩家 / 物品是方块，NPC 是长条
- **结局只有一个日志**：`EndingRules` 是纯函数 + 单测，但没接界面（按需求暂不做）
- **二楼没做**：`AreaId.SecondFloor` 已预留
- **母亲物品没进场景**：工具里 `IncludeMotherItems = false`

## 七、下一步

按难度可以这样分：

**适合新手（依赖少）**
1. 转向：朝向跟随左右移动
2. 音效触发：订阅已有事件 + `AudioManager.PlaySfx`（需要素材）
3. 门缝光接入：把 `DoorLightView` 挂到三扇门上并接暖意 / 知晓度
4. 打包 / 演示：分辨率、图标、构建脚本

**中等**
5. 行动"不可执行 → 重挑一次 → 仍不行就跳过并消耗次数"（程序分工 4.3）
6. 母亲物品进场景 + 补一扇母亲的门
7. 二楼场景（照着一楼 / 后院复制 + 传送门）
8. 正式存档 / 读档（`SessionStore` 已有 JSON 读写）

**留给主程**
9. 500 局命令行模拟器 + 结局出现率验收（每个结局都出现、最大 ≤60%、最小 ≥15%）
10. 正式 UI 架构（主界面 / 暂停 / 设置 / 结束 + 状态管理）
11. 性能与对象池（雨丝、涟漪、场景拼接的加载开销）
