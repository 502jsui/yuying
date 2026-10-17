using System;
using UnityEngine;

public class DoorLightView : MonoBehaviour
{
    public SpriteRenderer lightRenderer;

    [Header("开合程度")]
    public Transform doorLeaf;
    public float closedWarmth = 30f;
    public float halfOpenWarmth = 55f;

    [Header("光色")]
    public float whiteAwareness = 30f;
    public float yellowAwareness = 54f;
    public Color whiteLight = new Color(0.86f, 0.90f, 1f);
    public Color paleYellowLight = new Color(1f, 0.93f, 0.72f);
    public Color orangeLight = new Color(1f, 0.72f, 0.36f);

    /// <summary>门当前的三种开合状态。</summary>
    public enum OpenState
    {
        Closed,
        Seam,
        HalfOpen
    }

    public OpenState CurrentOpenState { get; private set; } = OpenState.Closed;

    /// <summary>
    /// 按文档规则更新门：
    ///   暖意 &lt;30 全关；[30,55) 开一条缝；≥55 开一半。
    ///   知晓度 &lt;30 白光；[30,54] 浅黄；&gt;54 黄橙。
    /// </summary>
    public void Apply(float warmth, float awareness, bool hasAwareness = true)
    {
        CurrentOpenState = ResolveOpenState(warmth);
        ApplyDoorLeaf(CurrentOpenState);

        if (lightRenderer == null) return;
        lightRenderer.color = ResolveLightColor(hasAwareness ? awareness : 0f, hasAwareness);
    }

    public OpenState ResolveOpenState(float warmth)
    {
        if (warmth < closedWarmth) return OpenState.Closed;
        if (warmth < halfOpenWarmth) return OpenState.Seam;
        return OpenState.HalfOpen;
    }

    public Color ResolveLightColor(float awareness, bool hasAwareness)
    {
        if (!hasAwareness) return whiteLight;
        if (awareness < whiteAwareness) return whiteLight;
        if (awareness <= yellowAwareness) return paleYellowLight;
        return orangeLight;
    }

    private void ApplyDoorLeaf(OpenState state)
    {
        if (doorLeaf == null) return;
        switch (state)
        {
            case OpenState.Closed:
                doorLeaf.localScale = new Vector3(1f, 1f, 1f);
                break;
            case OpenState.Seam:
                doorLeaf.localScale = new Vector3(0.82f, 1f, 1f);
                break;
            case OpenState.HalfOpen:
                doorLeaf.localScale = new Vector3(0.5f, 1f, 1f);
                break;
        }
    }
}
