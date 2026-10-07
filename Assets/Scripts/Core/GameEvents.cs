/// <summary>
/// 框架层事件定义。玩法/表现系统只依赖这些结构，不互相直接引用。
/// </summary>

public struct GameStateChangedEvent
{
    public GameState previous;
    public GameState current;
}

public struct TurnPhaseChangedEvent
{
    public int round;
    public TurnPhase previous;
    public TurnPhase current;
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

/// <summary>场景状态阶段：本轮场上有哪些物品（会随机补几件）。</summary>
public struct SceneStatePhaseEvent
{
    public int round;
    public AreaId area;
    public int placedItems;
}

public struct GameOverEvent
{
    public GameSession session;
}

/// <summary>玩家输入阶段开始，UI 可以提示“去碰一样东西”。</summary>
public struct PlayerInputPhaseEvent
{
    public int round;
}

/// <summary>物品状态结算阶段开始（温度 / 状态已结算完），表现层可以在这里刷微光、门光。</summary>
public struct ItemSettlePhaseEvent
{
    public int round;
}

/// <summary>NPC 行动阶段开始，相机应拉远、玩家移动应被锁住。</summary>
public struct NpcActionPhaseEvent
{
    public int round;
}

/// <summary>玩家移动开关变化。</summary>
public struct PlayerMoveLockEvent
{
    public bool locked;
}

public struct WarmthChangedEvent
{
    public ResidentId id;
    public float previous;
    public float current;
}

public struct AwarenessChangedEvent
{
    public ResidentId id;
    public float previous;
    public float current;
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

public struct DebugLogEvent
{
    public string message;
}
