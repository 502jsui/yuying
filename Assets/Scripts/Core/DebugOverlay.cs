using UnityEngine;

/// <summary>
/// GM 调试面板。按 F1 开关，不需要任何 Prefab / 场景配置。
/// </summary>
[DefaultExecutionOrder(1000)]
public class DebugOverlay : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F1;

    private bool visible;
    private GUIStyle labelStyle;
    private Rect windowRect = new Rect(12f, 12f, 340f, 560f);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible) return;
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 13;
            labelStyle.wordWrap = true;
        }

        windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "余音 · 调试面板 (F1)");
    }

    private void DrawWindow(int windowId)
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null || root.Flow == null)
        {
            GUILayout.Label("GameRoot 未初始化。", labelStyle);
            GUI.DragWindow();
            return;
        }

        GameSession session = root.Session;
        GameFlow flow = root.Flow;

        GUILayout.Label("状态：" + flow.State + "    阶段：" + flow.Phase, labelStyle);
        GUILayout.Label("轮次：" + session.round + " / " + root.Config.totalRounds + "    种子：" + session.seed, labelStyle);
        GUILayout.Label("本轮触碰：" + session.touchesThisRound + " / " + root.Config.touchesPerRound, labelStyle);
        GUILayout.Label("强碰警告已用：" + session.forceTouchWarningsUsed + " / " + root.Config.forceTouchWarnCount, labelStyle);
        if (session.hasAbsentResident)
        {
            GUILayout.Label("本轮不回家：" + session.absentResident.ToDisplayName(), labelStyle);
        }
        else
        {
            GUILayout.Label("本轮不回家：无", labelStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("住户：", labelStyle);
        for (int i = 0; i < session.residents.Count; i++)
        {
            ResidentData resident = session.residents[i];
            string awareness = resident.hasAwareness ? resident.awareness.ToString("0") : "无";
            GUILayout.Label("  " + resident.id.ToDisplayName() + "  暖意 " + resident.warmth.ToString("0") + "  知晓 " + awareness + "  " + resident.state, labelStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("物品（" + session.items.Count + "）：", labelStyle);
        for (int i = 0; i < session.items.Count; i++)
        {
            ItemData item = session.items[i];
            string where = item.isInPool
                ? "物品池"
                : (item.isReturned ? "已收回" : item.area.ToDisplayName() + " / " + item.placementId);
            string temperature = item.hasTemperature ? "有温度" : "无温度";
            GUILayout.Label("  " + item.itemId + "  " + item.type.ToDisplayName() + "  " + where + "  " + temperature, labelStyle);
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("重开一局（随机种子）")) root.StartNewGame();
        if (GUILayout.Button("重开一局（固定种子 12345）")) root.StartNewGame(12345);
        if (GUILayout.Button("强制进入下一阶段")) flow.ForceAdvancePhase();
        if (GUILayout.Button("把本区域物品全部标成有温度")) MarkAreaItemsWarm(session.playerArea);
        if (GUILayout.Button(flow.State == GameState.Paused ? "继续" : "暂停")) TogglePause(flow);
        if (GUILayout.Button("保存快照")) root.SaveDebugSnapshot();
        if (GUILayout.Button("读取快照")) root.LoadDebugSnapshot();

        GUI.DragWindow();
    }

    private void MarkAreaItemsWarm(AreaId area)
    {
        GameRoot root = GameRoot.Instance;
        if (root == null || root.Session == null) return;

        for (int i = 0; i < root.Session.items.Count; i++)
        {
            ItemData item = root.Session.items[i];
            if (!item.IsInScene || item.isReturned || item.hasTemperature || item.area != area) continue;
            item.hasTemperature = true;
            EventBus.Publish(new ItemTouchedEvent
            {
                itemId = item.itemId,
                owner = item.owner,
                hadTemperature = false
            });
        }
    }

    private void TogglePause(GameFlow flow)
    {
        if (flow.State == GameState.Paused) flow.Resume();
        else flow.Pause();
    }
}
