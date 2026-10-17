# 余音 · 住户行为 AI + 状态机说明

本文档说明住户行为 AI 与 NPC 状态机的实现、参数和接入点。
对应设计文档：第五章「住户行为」、3.2「NPC 状态定义」、第六章「交流传播」、第七章「母亲开门」。

## 一、文件与分层

| 文件 | 职责 |
| --- | --- |
| `Data/NpcAction.cs` | `NpcActionType`（五种行动）+ `NpcActionRecord`（一条决定好的行动） |
| `Core/NpcStateRules.cs` | 状态机纯规则：区域是否可用、进入 / 退出、行动是否被强制 |
| `Core/NpcSystem.cs` | 行为 AI：权重、前提条件、目标挑选、效果结算 |
| `Core/NpcPhaseDriver.cs` | 行动阶段驱动：订阅事件、按节奏执行、播完通知流程 |
| `Core/PlacementRegistry.cs` | 放置点的逻辑视图（NPC 挑放置点用，不看场景物体） |

`NpcSystem` 是纯 C#，不挂场景；`NpcPhaseDriver` 挂在 `GameRoot` 上负责时序。

## 二、行动与权重（文档五）

| 行动 | 权重 | 前提条件 |
| --- | --- | --- |
| 闲逛 Wander | 35 | 永远可做（兜底） |
| 交流 Talk | 25 | 只有老婆婆、大叔能主动做；小女孩不交流，母亲不动 |
| 拿出东西 TakeOutItem | 20 | 物品池里还有他的物品，且场景里有能放下它的放置点 |
| 拿回东西 TakeBackItem | 20 | 场景里有他自己的物品 |
| 移动东西 MoveItem | `girlMoveWeight`（默认 20） | 只有小女孩；场景里有物品，且有能放下的放置点 |

权重只在**满足前提条件**的行动里归一化挑选；都不满足时退回闲逛。

行动次数（`ResidentData.actionMin / actionMax`，来自 `GameConfig.residents`）：

| 住户 | 次数 |
| --- | --- |
| 老婆婆 / 大叔 | 2~3 |
| 小女孩 | 1~2 |
| 母亲 | 0（不行动） |

## 三、交流（文档六）

- 交流对象：**20% 找母亲，80% 找另一位大人**（老婆婆 ↔ 大叔）。
- 谁能交流：老婆婆 ↔ 大叔 ↔ 母亲（三角形），**母亲只能被动**，**小女孩不在网络里**。
- 数值：普通交流 → 对方暖意 +4、知晓 +8；交流母亲 → 母亲暖意 +0、知晓 +11。
- 强碰传染：被强碰者（惊吓状态）下次交流时，**交流对象暖意 −5**；他自己那次交流不加暖意、知晓也不变。

母亲开门：母亲知晓 > 60 → 暖意至少 61、知晓至少 63（已更高不往下压；约 3 次交流）。

## 四、小女孩 / 母亲特殊规则（文档三、五）

- 小女孩：没有自己的物品，**只做「闲逛」和「移动东西」**；移动一次暖意 +18，并把物品温度清零；不交流、知晓度不参与结算。
- 母亲：不主动行动，只能被交流；玩家碰母亲物品会让 1~2 位住户进入「疑惑」，他们下一次行动会去敲母亲的门。

## 五、状态机（文档 3.2）

| 状态 | 含义 | 进入条件 | 退出条件 | 区域可用 | 行动约束 |
| --- | --- | --- | --- | --- | --- |
| 正常 Normal | 在家，常规行动 | 默认 / 每轮开始没被选为不回家 | 进入其他状态 | 可用 | 按权重随机 |
| 出门行动 GoingOut | 本轮外出做事 | 正常状态执行一个行动时 | 行动结束、回家 | 可用 | 执行完自动回正常 |
| 区域持续 StayOut | 行动后没回家，停在某个区域 | 住户行动**结束后**判定他回家还是不回家（先判有没有、再判是谁） | 玩家强行触碰 / 下一轮自行回家 | **不可用** | 仍然正常行动 |
| 疑惑 Confused | 下次行动必去敲母亲的门 | 玩家触碰母亲的物品后随机 1~2 位 | 执行完那次行动 | 可用 | 强制：交流母亲 |
| 惊吓 Startled | 下次行动必为交流，且让对方暖意减少 | 玩家强行触碰「区域持续」住户所在区域的物品 | 交流一次之后 | 可用 | 强制：带传染的交流 |

区域占用与强碰的联动：

```csharp
// 每轮开始（NpcSystem.BeginRound）
items.SetAreaBlocked(stayOutArea, residentId);   // 该区域本回合不可用

// 玩家强行触碰成功之后（ItemSystem）
ClearAreaBlocked(item.area);                     // 他不再占着区域，并进入惊吓
resident.state = NpcState.Startled;
```

强碰前两次只弹警告、不消耗本轮触碰；第 3 次起物品获得温度、该住户暖意 −8 并进入惊吓。
这两次警告额度**每轮重置**（`GameSession.BeginRound`），所以每一轮的节奏是一样的。

## 六、接入点

```csharp
NpcSystem npc = GameRoot.Instance.Npc;

npc.BeginRound(roundStartedEvent.hasAbsentResident, roundStartedEvent.absentResident);
List<ResidentData> actors = npc.GetActors();
int count = npc.RollActionCount(actor);
NpcActionRecord record = npc.DecideNextAction(actor, order);
NpcActionResult result = npc.Execute(record);
```

事件（`EventBus`）：

| 事件 | 何时发 | 用途 |
| --- | --- | --- |
| `NpcActionEvent` | 每执行一个行动 | 表现层播开门 / 走动 / 微光动画 |
| `NpcStateChangedEvent` | 状态切换 | 门光、表现层状态 |
| `ItemAppearedEvent` / `ItemMovedEvent` / `ItemTakenEvent` | 物品被拿出 / 移动 / 拿回 | 物品表现 |

行动阶段的时序：`GameFlow` 进入 `NpcAction` → 广播 `NpcActionPhaseEvent` →
`NpcPhaseDriver` 按 `GameConfig.npcActionStepDelay`（默认 0.35 秒）逐个执行 →
跑完调 `GameFlow.NotifyNpcActionDone()`。

## 七、测试

`Assets/Tests/EditMode/NpcSystemTests.cs` 覆盖：

- 行动权重 35/25/20/20、小女孩移动权重
- 行动次数 2~3 / 1~2 / 母亲 0
- 各行动前提条件（物品池空、没有自己的物品在场景）
- 小女孩只会有闲逛 / 移动，不会交流、拿出、拿回
- 交流对象 20% 母亲、80% 另一位大人；不会找自己
- 交流数值、母亲门在 3 次交流后跳到至少 61/63
- 拿出 / 拿回 / 小女孩移动的数值与物品状态
- 状态机：区域持续 + 区域不可用、下一轮回家、疑惑强制敲门、惊吓强制传染交流

PlayMode 里 `NpcPhaseDriver_RunsActions_AndFinishesPhase` 验证驱动能自己跑完行动阶段并推进轮次。

## 八、当前假设（需要确认）

1. **交流数值只加在对方身上**：文档「输出：目标住户的知晓度」，所以发起者本次不加暖意 / 知晓（也与「被强碰者自己那次交流不加暖意」一致）。
2. **区域持续落在哪个区域**：文档没写每个住户住哪，默认从 `GameConfig.stayOutAreas`（一楼 / 后院）里随机挑一个，住户的 `homeArea` 默认一楼。
3. **小女孩移动 = 拿回东西的权重位**：用单独的 `girlMoveWeight`（默认 20）配置。
4. **强碰警告不消耗本轮触碰**：与物品系统的实现一致；两次额度按轮重置，不跨轮累计。
5. 住户的走路 / 开门动画由表现层消费 `NpcActionEvent` 自己播，本文只保证逻辑顺序。

## 九、场景摆放（当前是长条形占位）

角色美术还没进，先用**长条形**占位。菜单：

```
Tools → 余音 → 在当前场景补齐 NPC
```

- 幂等：重复运行只补缺失的住户，不会重建或移动已有对象（和物品那套一致）。
- 生成在 `NPCs` 根物体下，用 `Assets/Prefabs/Resident_*.prefab` 实例化，把 `Body` 拉成竖长条。
- 占位尺寸 / 巡逻范围：

| 住户 | 长条宽×高 | 巡逻范围 | 速度 | 备注 |
| --- | --- | --- | --- | --- |
| 老婆婆 | 0.80 × 2.00 | -7 ~ -5（一楼） | 0.30 | 对应 -6 的门 |
| 大叔 | 0.90 × 2.30 | -1 ~ 1（一楼） | 0.30 | 对应 0 的门 |
| 小女孩 | 0.60 × 1.40 | 2.5 ~ 8（一楼） | 1.40 | 对应 6 的门，跑得快 |

- 一楼、后院各放一份：住户默认在 `homeArea`（一楼）显示；进入「区域持续」停在 `stayOutArea` 时，
  对应场景的那一份会出现、原来的那份自动隐藏。
- 母亲本次不做，暂不生成（`YuyinNpcSceneTool.IncludeMother` 改成 `true` 可启用）。
- `Tools → 余音 → 重建基础场景` 也会一起生成这些 NPC。
- 现在**还没有接**：开门动画、门缝光（`DoorLightView` 组件还没挂到场景的门上）。

### 走位 / 跨场景（已实现）

住户执行物品类行动时会**先走过去，再动手**：

| 行动 | 走位 |
| --- | --- |
| 拿出东西 | 走到目标放置点 → 放物品 |
| 拿回东西 | 走到物品当前所在的放置点 → 收走 |
| 移动东西（小女孩） | 先走到物品所在点 → 再走到新的放置点 → 放手 |
| 交流 | 走到交流对象旁边 |

- **只沿地面左右移动**：`MoveViewTo` 只改 X，保持住户自己的地面高度 Y，不会斜着飞过去。
- 目标区域和住户当前区域不同时：先**沿地面走到本场景通往目标区域的传送点**（`ScenePortal2D`），
  到点后**自动传送**（换 `currentArea` 并广播 `NpcMovedEvent`）——
  原场景那份 `ResidentView` 自动隐藏；如果目标区域就是当前加载的场景，目标那份会出现并继续走到目标点。
- 每轮开始 `NpcSystem.BeginRound` 会把住户放回 `homeArea`（「区域持续」的则留在 `stayOutArea`）。
- 走位时长 = `max(npcTravelDuration, 距离 / npcTravelSpeed)`（默认 0.7 秒 / 3.5 单位每秒）。
- **住户默认待在门里 / 门口**：`NpcPatrol2D` 默认关闭，不会自己乱走；只有玩家碰过物品、轮到 NPC 行动阶段时，
  他们才会被 `NpcPhaseDriver` 带着走到目标放置点。
- **在家 = 在门里面（不可见）**：`ResidentData.IsOutside` 决定显隐；只有「出门行动 / 区域持续」才可见，
  行动结束回家又消失回门里。
- **站位跨场景保持**：`ResidentData.currentX` 记着他当前的横向位置，切场景回来时按这个位置还原。
- **小女孩不跨区域**：只移动**本区域**里的物品，也只在本区域内换位置（程序分工 5.7）。
- **拿回东西随机挑**：不会再固定收同一件（对应「不一定是有温度的那件」）。
- 一轮的住户行动全部演完后，`NpcSystem.ResolveEndOfRound()` 判定回家 / 不回家：
  回家的人走回自己门口；不回家的人停在原地进入「区域持续」。

### 母亲（程序分工 5.x）

- 整局不主动行动；知晓度靠**被交流**累积（每次 +11，初始 30）。
- 暖意整局固定 30（不会被强碰传染扣）。
- **第 6 轮结束后**才判定开门：知晓 > 60 → 暖意跳到 61，并一次性收走自己所有东西；否则保持 30、门不开。

> 已知修复：`Resident_Aunt.prefab` 原本子物体叫 `LightSprite`、`bodyRenderer` 为空，
> 导致大叔没被拉成长条、显示成会动的方块。现在 prefab 已修好，
> `YuyinNpcSceneTool` 也会在运行时补 `bodyRenderer` 和长条尺寸（幂等，可反复跑）。
