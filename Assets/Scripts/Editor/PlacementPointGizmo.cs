using UnityEditor;
using UnityEngine;

/// <summary>
/// 在 Scene 视图里给物品放置点画一个小球 + 名称，方便直接拖动改位置。
/// 颜色按放置面区分：蓝=晾衣架，绿=地面+可叠放，黄=地面，粉=只能叠放。
/// </summary>
public static class PlacementPointGizmo
{
    private static GUIStyle labelStyle;

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    private static void Draw(PlacementPoint point, GizmoType gizmoType)
    {
        Vector3 position = point.AnchorPosition;
        Color color = ColorFor(point.surface);

        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, 0.35f);
        Gizmos.DrawSphere(position, 0.08f);

        if (point.anchor != null)
        {
            Gizmos.DrawLine(position, point.anchor.position);
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle();
            labelStyle.fontSize = 11;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontStyle = FontStyle.Bold;
        }
        labelStyle.normal.textColor = color;
        Handles.Label(position + Vector3.up * 0.5f, point.placementId + "\n[" + point.surface + "]", labelStyle);
    }

    private static Color ColorFor(PlacementSurface surface)
    {
        bool ground = (surface & PlacementSurface.Ground) != 0;
        bool onItem = (surface & PlacementSurface.OnItem) != 0;
        bool rack = (surface & PlacementSurface.HangingRack) != 0;

        if (rack && !ground && !onItem) return new Color(0.35f, 0.75f, 1f);
        if (ground && onItem) return new Color(0.45f, 1f, 0.55f);
        if (ground) return new Color(1f, 0.85f, 0.35f);
        if (onItem) return new Color(1f, 0.55f, 0.85f);
        return new Color(0.8f, 0.8f, 0.8f);
    }
}
