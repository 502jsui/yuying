public enum NpcState
{
    Normal,
    Sitting,
    GoingOut,
    StayOut,
    Confused,
    Startled
}

/// <summary>行动阶段的额外美术姿态；坐姿由 NpcState.Sitting 决定。</summary>
public enum NpcVisualPose
{
    Default,
    Walking,
    Talking
}
