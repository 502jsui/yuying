/// <summary>
/// 框架层事件定义。玩法/表现系统只依赖这些结构，不互相直接引用。
/// </summary>

public struct GameStateChangedEvent
{
    public GameState previous;
    public GameState current;
}

public struct RoundStartedEvent
{
    public int round;
    public ResidentId absentResident;
    public bool hasAbsentResident;
}

public struct RoundEndedEvent
{
    public int round;
    public ResidentId absentResident;
    public bool hasAbsentResident;
}

public struct GameOverEvent
{
    public GameSession session;
    public EndingResult ending;
}

/// <summary>NPC 行动阶段开始，相机应拉远、玩家移动应被锁住。</summary>
public struct NpcActionPhaseEvent
{
    public int round;
}

public struct ItemTouchedEvent
{
    public string itemId;
    public ItemOwner owner;
    public bool hadTemperature;
}

/// <summary>物品从物品池被放到场景里（「拿出东西」）。ItemView 收到后显示并吸附到放置点。</summary>
public struct ItemAppearedEvent
{
    public string itemId;
    public AreaId area;
    public string placementId;
}

public struct ItemTakenEvent
{
    public string itemId;
    public ResidentId by;
    public bool hadTemperature;
}

public struct ItemMovedEvent
{
    public string itemId;
    public ResidentId by;
    public bool hadTemperature;
}

/// <summary>物品系统发给 UI 的提示（「你已经触摸过了」「他还在那边」等）。</summary>
public struct ItemPromptEvent
{
    public string itemId;
    public string message;
    public ItemActionStatus status;
}

/// <summary>玩家身边有没有能碰的东西（可交互提示用）。</summary>
public struct InteractHintEvent
{
    public bool available;
    public string itemId;
    public string text;
}

/// <summary>玩家身边有没有可以按 F 切换的场景门（提示文案由发布方给）。</summary>
public struct PortalHintEvent
{
    public bool available;
    public string text;
}

/// <summary>强行触碰不可用区域，但还在警告阶段时发出。</summary>
public struct ForceTouchWarnedEvent
{
    public string itemId;
    public ResidentId blockingResident;
    public int warningsRemaining;
}

public struct NpcStateChangedEvent
{
    public ResidentId id;
    public NpcState previous;
    public NpcState current;
}

public struct NpcPoseChangedEvent
{
    public ResidentId id;
    public NpcVisualPose pose;
}

/// <summary>住户换区域（跨场景走位 / 传送）。表现层据此显示 / 隐藏各场景里的那一份。</summary>
public struct NpcMovedEvent
{
    public ResidentId id;
    public AreaId from;
    public AreaId to;
    public string placementId;
}

/// <summary>住户行动阶段里每执行一个行动就广播一次，表现层据此播开门 / 走动动画。</summary>
public struct NpcActionEvent
{
    public ResidentId actor;
    public NpcActionType action;
    public ResidentId target;
    public string itemId;
    public AreaId area;
    public string placementId;
    public int order;
    public bool executed;
    public bool contagion;
    public bool motherKnock;
}

/// <summary>住户开门 / 关门（现有的 NpcMovedEvent 分不出开还是关）。</summary>
public struct NpcDoorEvent
{
    public ResidentId id;
    public bool opening;
}

/// <summary>旁白 / 提示的优先级：数值越大越优先顶掉当前这条。</summary>
public enum NarrationPriority
{
    /// <summary>物品独白（最低）。</summary>
    Monologue = 0,

    /// <summary>玩法教学。</summary>
    Tutorial = 1,

    /// <summary>拒绝 / 警告类提示（最高）。</summary>
    Prompt = 2
}

/// <summary>发给统一提示通道的一句话。holdSeconds &lt;= 0 表示一直显示到被下一条替换。</summary>
public struct NarrationEvent
{
    public string text;
    public NarrationPriority priority;
    public float holdSeconds;
}

/// <summary>清掉当前提示条（跳过独白时用）；优先级相同或更低的那条会被清掉。</summary>
public struct NarrationClearEvent
{
    public NarrationPriority priority;
}
