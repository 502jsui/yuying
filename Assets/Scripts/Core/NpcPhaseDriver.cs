using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// NPC 行动阶段的驱动：
///   订阅 RoundStartedEvent → 让 NpcSystem 决定本轮谁不回家、哪个区域不可用；
///   订阅 NpcActionPhaseEvent → 按顺序执行住户行动，播完后调 GameFlow.NotifyNpcActionDone()。
///
/// 逻辑都在 NpcSystem（纯 C#），这里只负责“什么时候演、隔多久演下一个”。
/// </summary>
[DefaultExecutionOrder(-800)]
public class NpcPhaseDriver : MonoBehaviour
{
    private NpcSystem npc;
    private GameFlow flow;
    private GameConfig config;
    private Coroutine running;
    private bool subscribed;

<<<<<<< HEAD
    /// <summary>本轮已经"从门里出来过"的住户：开门声每人每轮只响一次。</summary>
    private readonly HashSet<ResidentId> leftHomeThisRound = new HashSet<ResidentId>();

    /// <summary>行动阶段被临时关掉的巡逻，收尾时按原位恢复（范围锚到当前位置）。</summary>
    private readonly List<NpcPatrol2D> phasePatrols = new List<NpcPatrol2D>();
    private readonly List<bool> phasePatrolWasEnabled = new List<bool>();

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
    public void Initialize(NpcSystem npcSystem, GameFlow gameFlow, GameConfig gameConfig)
    {
        npc = npcSystem;
        flow = gameFlow;
        config = gameConfig;
        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        EventBus.Subscribe<RoundStartedEvent>(OnRoundStarted);
        EventBus.Subscribe<NpcActionPhaseEvent>(OnNpcActionPhase);
        subscribed = true;
    }

    private void OnDestroy()
    {
<<<<<<< HEAD
        RestorePatrolsAfterPhase();
=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        if (!subscribed) return;
        EventBus.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        EventBus.Unsubscribe<NpcActionPhaseEvent>(OnNpcActionPhase);
        subscribed = false;
    }

    private void OnRoundStarted(RoundStartedEvent evt)
    {
        if (npc == null) return;
        npc.BeginRound(evt.hasAbsentResident, evt.absentResident);
    }

    private void OnNpcActionPhase(NpcActionPhaseEvent evt)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(RunPhase());
    }

    private IEnumerator RunPhase()
    {
        if (npc == null || flow == null)
        {
            running = null;
            yield break;
        }

        float delay = config != null ? Mathf.Max(0f, config.npcActionStepDelay) : 0f;
        List<ResidentData> actors = npc.GetActors();

        // NPC 行动阶段：把一楼 + 后院拼成一个整场景，住户可以自由穿梭。
        SceneStitcher stitcher = SceneStitcher.Instance;
        if (stitcher != null) yield return stitcher.Begin(CurrentArea());

<<<<<<< HEAD
        // 行动阶段完全由驱动接管移动：先关掉所有巡逻，否则巡逻会把越界的角色钳回 minX/maxX（瞬移）。
        leftHomeThisRound.Clear();
        DisablePatrolsForPhase();

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        // 异常情况.pdf：场上没被碰过的物品 ≤1 件 → 这一轮必有住户拿出物品。
        npc.ForceTakeOutNext = npc.CountUntouchedItems(CurrentArea()) <= 1;

        for (int i = 0; i < actors.Count; i++)
        {
            ResidentData actor = actors[i];
            int count = npc.RollActionCount(actor);

            for (int order = 1; order <= count; order++)
            {
                NpcActionRecord record = npc.DecideNextAction(actor, order);

                // 先走位：拿出 → 目标放置点；拿回 → 物品所在点；
                // 移动物品 → 先去物品那儿，再去新的放置点。跨场景则传送过去。
                yield return MoveIntoPosition(record);

                NpcActionResult result = npc.Execute(record);
                Publish(record, result);

                // 每个行动之间留一点时间，让表现层（开门 / 走动 / 微光）有机会播。
                if (delay > 0f) yield return new WaitForSeconds(delay);
                else yield return null;
            }
        }

        // 行动结束：判定回家还是不回家（先判有没有，再判是谁）。
        // 没被留下的走回自己门口；被留下的保持「区域持续」，停在原地。
        NpcRoundEndResult roundEnd = npc.ResolveEndOfRound();
        yield return WalkHome(actors, roundEnd);

<<<<<<< HEAD
        // 没能走回家的人（还在另一个场景 / 不回家的那位除外）统一补一次关门
        CloseDoorsForLeftovers(roundEnd);

        // 行动演完，拆开两个场景、相机恢复。
        if (stitcher != null) yield return stitcher.End();

        RestorePatrolsAfterPhase();
=======
        // 行动演完，拆开两个场景、相机恢复。
        if (stitcher != null) yield return stitcher.End();

>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        running = null;
        flow.NotifyNpcActionDone();
    }

    private IEnumerator WalkHome(List<ResidentData> actors, NpcRoundEndResult roundEnd)
    {
        for (int i = 0; i < actors.Count; i++)
        {
            ResidentData actor = actors[i];
            if (actor == null || !actor.canAct) continue;

            // 不回家的人停在原地，不下班。
            if (roundEnd.hasAbsentResident && actor.id == roundEnd.absentResident) continue;

<<<<<<< HEAD
            ResidentView view = FindActorView(actor.id);
            if (view == null || view.area != CurrentArea()) continue;

            bool alreadyHome = Mathf.Abs(view.transform.position.x - view.HomePosition.x) < 0.05f;
            if (alreadyHome)
            {
                // 已经站在门口：不用走位，但还是要"关门"
                SetOutside(actor, false, leftHomeThisRound.Remove(actor.id));
                continue;
            }

            // 回家路上保持可见（这里不重复发开门声），走到门口再进「门里面」。
            SetOutside(actor, true, false);
            Vector3 home = new Vector3(
                SceneStitcher.WorldX(view.area, view.HomePosition.x),
                view.HomePosition.y,
                view.HomePosition.z);
            yield return TravelTo(actor.id, view.area, null, true, home);

            // 只有本轮真的开过门的人，才给一声关门
            SetOutside(actor, false, leftHomeThisRound.Remove(actor.id));
        }
    }

    /// <summary>移动结束后给没走回家的那些人补一次关门（不回家的人除外）。</summary>
    private void CloseDoorsForLeftovers(NpcRoundEndResult roundEnd)
    {
        if (leftHomeThisRound.Count == 0) return;

        List<ResidentId> ids = new List<ResidentId>(leftHomeThisRound);
        for (int i = 0; i < ids.Count; i++)
        {
            ResidentId id = ids[i];
            if (roundEnd.hasAbsentResident && id == roundEnd.absentResident) continue;

            ResidentData resident = npc.Session.GetResident(id);
            if (resident != null) SetOutside(resident, false, true);
        }
        leftHomeThisRound.Clear();
    }

    private void ComeOutside(ResidentId actor)
    {
        ResidentData resident = npc.Session.GetResident(actor);
        if (resident == null) return;

        // 开门声：每人每轮只在"第一次从门里出来"响一次
        bool firstThisRound = leftHomeThisRound.Add(actor);
        SetOutside(resident, true, firstThisRound);
    }

    private void SetOutside(ResidentData resident, bool outside, bool publishDoorEvent)
    {
        if (resident == null || resident.isOut == outside) return;
        resident.isOut = outside;

        if (publishDoorEvent)
        {
            EventBus.Publish(new NpcDoorEvent
            {
                id = resident.id,
                opening = outside
            });
        }

        {
=======
              ResidentView view = FindActorView(actor.id);
              if (view == null || view.area != CurrentArea()) continue;
              if (Mathf.Abs(view.transform.position.x - view.HomePosition.x) < 0.05f) continue;

              // 回家路上保持可见，走到门口再进「门里面」。
              SetOutside(actor, true);
              Vector3 home = new Vector3(
                  SceneStitcher.WorldX(view.area, view.HomePosition.x),
                  view.HomePosition.y,
                  view.HomePosition.z);
              yield return TravelTo(actor.id, view.area, null, true, home);
              SetOutside(actor, false);
          }
      }

      private void ComeOutside(ResidentId actor)
      {
          ResidentData resident = npc.Session.GetResident(actor);
          if (resident == null) return;
          SetOutside(resident, true);
      }

      private void SetOutside(ResidentData resident, bool outside)
      {
          if (resident == null || resident.isOut == outside) return;
          resident.isOut = outside;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
          EventBus.Publish(new NpcMovedEvent
          {
              id = resident.id,
              from = resident.currentArea,
              to = resident.currentArea,
              placementId = null
          });
<<<<<<< HEAD
        }
    }

    // ------------------------------------------------------------------
    // 巡逻接管：行动阶段全部关掉，收尾再按原位恢复
    // ------------------------------------------------------------------

    /// <summary>
    /// 行动阶段由驱动完全接管移动。巡逻必须关掉：
    /// 它一旦发现角色越出自己的 minX/maxX，就会把人直接钳回去 —— 那就是"瞬移"。
    /// </summary>
    private void DisablePatrolsForPhase()
    {
        phasePatrols.Clear();
        phasePatrolWasEnabled.Clear();

        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        for (int i = 0; i < views.Length; i++)
        {
            ResidentView view = views[i];
            if (view == null) continue;

            NpcPatrol2D patrol = view.GetComponent<NpcPatrol2D>();
            if (patrol == null) continue;

            phasePatrols.Add(patrol);
            phasePatrolWasEnabled.Add(patrol.enabled);
            patrol.enabled = false;
        }
    }

    /// <summary>相位结束：恢复原本开着的巡逻，并把范围锚到角色当前位置（不会弹回旧范围）。</summary>
    private void RestorePatrolsAfterPhase()
    {
        for (int i = 0; i < phasePatrols.Count; i++)
        {
            NpcPatrol2D patrol = phasePatrols[i];
            if (patrol == null) continue;
            if (i >= phasePatrolWasEnabled.Count || !phasePatrolWasEnabled[i]) continue;

            float x = patrol.transform.position.x;
            patrol.minX = x - 0.5f;
            patrol.maxX = x + 0.5f;
            patrol.enabled = true;
        }

        phasePatrols.Clear();
        phasePatrolWasEnabled.Clear();
    }
=======
      }
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0

    // ------------------------------------------------------------------
    // 走位
    // ------------------------------------------------------------------

      private IEnumerator MoveIntoPosition(NpcActionRecord record)
      {
          // 出门行动：先让他从门里出来（可见），再走位。
          ComeOutside(record.actor);

          switch (record.action)
        {
            case NpcActionType.TakeOutItem:
                yield return TravelToPlacement(record.actor, record.area, record.placementId);
                break;

            case NpcActionType.TakeBackItem:
            {
                ItemData item = npc.Session.GetItem(record.itemId);
                if (item != null) yield return TravelToPlacement(record.actor, item.area, item.placementId);
                break;
            }

            case NpcActionType.MoveItem:
            {
                // 小女孩：先去拿，再去放。
                ItemData item = npc.Session.GetItem(record.itemId);
                if (item != null) yield return TravelToPlacement(record.actor, item.area, item.placementId);
                yield return TravelToPlacement(record.actor, record.area, record.placementId);
                break;
            }

            case NpcActionType.Talk:
                yield return TravelToResident(record.actor, record.target);
                break;
        }
    }

    private IEnumerator TravelToPlacement(ResidentId actor, AreaId area, string placementId)
    {
        PlacementPoint point = PlacementPoint.Find(placementId);
        if (point == null)
        {
            // 放置点没了（比如换场景 / 被删）：至少保证人在正确的区域。
            yield return TravelTo(actor, area, placementId, false, Vector3.zero);
            yield break;
        }

        yield return TravelTo(actor, area, placementId, true, point.AnchorPosition);
    }

    private IEnumerator TravelToResident(ResidentId actor, ResidentId target)
    {
        ResidentData targetData = npc.Session.GetResident(target);
        if (targetData == null) yield break;

        AreaId targetArea = targetData.currentArea;
        if (targetArea != CurrentArea())
        {
            yield return TravelTo(actor, targetArea, null, false, Vector3.zero);
            yield break;
        }

        ResidentView targetView = FindActorView(target);
        if (targetView == null)
        {
            yield return TravelTo(actor, targetArea, null, false, Vector3.zero);
            yield break;
        }

        // 走到对方旁边一点的位置，别重叠。
        Vector3 offset = actor == ResidentId.Grandma ? new Vector3(-0.8f, 0f, 0f) : new Vector3(0.8f, 0f, 0f);
        yield return TravelTo(actor, targetArea, null, true, targetView.transform.position + offset);
    }

    /// <summary>需要的话先换区域（跨场景传送），然后在当前场景里走过去。</summary>
    private IEnumerator TravelTo(ResidentId actor, AreaId area, string placementId, bool hasPosition, Vector3 position)
    {
        ResidentData resident = npc.Session.GetResident(actor);
        if (resident == null || area == AreaId.None) yield break;

        // 拼接模式：两个场景左右相连，直接在世界坐标里横着走过去（可以跨接缝）。
        if (SceneStitcher.IsActive)
        {
            if (hasPosition) yield return MoveStitched(actor, area, position.x);
            yield break;
        }

        if (resident.currentArea != area)
        {
            // ① 先走到本场景通往目标区域的传送点（只沿地面左右走）。
            ResidentView walker = FindActorView(actor);
            ScenePortal2D portal = FindPortalTo(area);
            if (walker != null && portal != null)
            {
                yield return MoveViewTo(walker, portal.transform.position);
            }

            // ② 到传送点 → 自动传送（换区域）。
            npc.MoveResidentToArea(actor, area, placementId);
            yield return null; // 让本场景那份 View 隐藏

            // 目标在别的场景：这边看不到他，等玩家过去。
            if (area != CurrentArea()) yield break;

            // 目标就是当前场景（他之前在外地）：出现后继续走到目标点。
            ResidentView arrived = FindActorView(actor);
            if (arrived != null && hasPosition)
            {
                yield return MoveViewTo(arrived, position);
            }
            yield break;
        }

        if (area != CurrentArea()) yield break;
        if (!hasPosition) yield break;

        ResidentView view = FindActorView(actor);
        if (view == null) yield break;
        yield return MoveViewTo(view, position);
    }

    /// <summary>
    /// NPC 只沿地面左右移动：只改 X，保持自己的地面高度 Y。
    /// </summary>
    private IEnumerator MoveViewTo(ResidentView view, Vector3 target)
    {
        NpcPatrol2D patrol = view.GetComponent<NpcPatrol2D>();
<<<<<<< HEAD
=======
        bool patrolWasEnabled = patrol != null && patrol.enabled;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        if (patrol != null) patrol.enabled = false;

        Vector3 from = view.transform.position;
        float groundY = from.y;
        Vector3 destination = new Vector3(target.x, groundY, from.z);

        float distance = Mathf.Abs(destination.x - from.x);
        float speed = config != null ? Mathf.Max(0.1f, config.npcTravelSpeed) : 3.5f;
        float minDuration = config != null ? Mathf.Max(0.05f, config.npcTravelDuration) : 0.7f;
        float duration = Mathf.Max(minDuration, distance / speed);

        float elapsed = 0f;

        while (elapsed < duration)
        {
<<<<<<< HEAD
            // 卡帧保护：一帧最多推进 50ms 的位移，
            // 否则加载 / GC 造成的一次长帧会让角色一步走完整段路（看起来就是瞬移）。
            elapsed += Mathf.Min(Time.deltaTime, 0.05f);
=======
            elapsed += Time.deltaTime;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
            float x = Mathf.Lerp(from.x, destination.x, Mathf.Clamp01(elapsed / duration));
            view.transform.position = new Vector3(x, groundY, from.z);
            yield return null;
        }
          view.transform.position = destination;

          // 记回数据，切场景回来时能还原站位。
          if (view.data != null)
          {
              view.data.currentX = destination.x;
              view.data.hasCurrentX = true;
          }

<<<<<<< HEAD
        // 巡逻范围跟着挪到新位置，免得一恢复巡逻就弹回原地；
        // 是否重新启用在相位收尾统一处理（行动阶段巡逻必须一直关着）。
=======
          // 巡逻范围跟着挪到新位置，免得一恢复巡逻就弹回原地。
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        if (patrol != null)
        {
            patrol.minX = destination.x - 0.5f;
            patrol.maxX = destination.x + 0.5f;
<<<<<<< HEAD
=======
            patrol.enabled = patrolWasEnabled;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        }
    }

    /// <summary>找当前场景里通往目标区域的传送点（ScenePortal2D.sceneName）。</summary>
    private ScenePortal2D FindPortalTo(AreaId area)
    {
        Scene active = SceneManager.GetActiveScene();
        ScenePortal2D[] portals = Object.FindObjectsOfType<ScenePortal2D>();

        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal2D portal = portals[i];
            if (portal == null) continue;
            if (portal.gameObject.scene != active) continue;
            if (AreaIdUtil.FromSceneName(portal.sceneName) != area) continue;
            return portal;
        }

        return null;
    }

    private ResidentView FindActorView(ResidentId actor)
    {
        ResidentData resident = npc != null ? npc.Session.GetResident(actor) : null;
        AreaId area = resident != null ? resident.currentArea : CurrentArea();
        return FindActorView(actor, area);
    }

    /// <summary>在所有已加载场景里找某个住户、某个区域的 View（拼接时两个场景都加载着）。</summary>
    private ResidentView FindActorView(ResidentId actor, AreaId area)
    {
        ResidentView[] views = Object.FindObjectsOfType<ResidentView>();
        ResidentView fallback = null;

        for (int i = 0; i < views.Length; i++)
        {
            ResidentView view = views[i];
            if (view == null || view.id != actor) continue;
            if (area == AreaId.None || view.area == area) return view;
            if (fallback == null) fallback = view;
        }

        return fallback;
    }

    /// <summary>拼接模式下的走位：只改世界 X，跨过接缝时自动换到另一个场景的那份 View。</summary>
    private IEnumerator MoveStitched(ResidentId actor, AreaId targetArea, float targetWorldX)
    {
        ResidentData resident = npc.Session.GetResident(actor);
        if (resident == null) yield break;

        // 起点必须是「他现在实际站的位置」，否则第一次走位会瞬移过去。
        ResidentView currentView = FindActorView(resident.id, resident.currentArea);
        float startWorldX = currentView != null
            ? currentView.transform.position.x
            : SceneStitcher.WorldX(resident.currentArea, resident.hasCurrentX ? resident.currentX : targetWorldX);

        float distance = Mathf.Abs(targetWorldX - startWorldX);
        float speed = config != null ? Mathf.Max(0.1f, config.npcTravelSpeed) : 3.5f;
        float minDuration = config != null ? Mathf.Max(0.05f, config.npcTravelDuration) : 0.7f;
        float duration = Mathf.Max(minDuration, distance / speed);

        float elapsed = 0f;
        while (elapsed < duration)
        {
<<<<<<< HEAD
            // 同上：限制单帧推进量，避免长帧导致跨场景瞬移
            elapsed += Mathf.Min(Time.deltaTime, 0.05f);
=======
            elapsed += Time.deltaTime;
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
            ApplyStitchedX(resident, Mathf.Lerp(startWorldX, targetWorldX, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        ApplyStitchedX(resident, targetWorldX);
        resident.currentArea = targetArea;
        resident.currentX = SceneStitcher.LocalX(targetArea, targetWorldX);
        resident.hasCurrentX = true;
    }

    private void ApplyStitchedX(ResidentData resident, float worldX)
    {
        AreaId area = SceneStitcher.AreaAtWorldX(worldX);
        if (area != resident.currentArea)
        {
            AreaId from = resident.currentArea;
            resident.currentArea = area;
            EventBus.Publish(new NpcMovedEvent
            {
                id = resident.id,
                from = from,
                to = area,
                placementId = null
            });
        }

        resident.currentX = SceneStitcher.LocalX(area, worldX);
        resident.hasCurrentX = true;

        ResidentView view = FindActorView(resident.id, area);
        if (view == null) return;

<<<<<<< HEAD
        // 保险：拼接走位期间巡逻必须关着，否则它会把刚走出去的角色钳回 minX/maxX。
        NpcPatrol2D patrol = view.GetComponent<NpcPatrol2D>();
        if (patrol != null && patrol.enabled) patrol.enabled = false;

=======
>>>>>>> 77ae4b4f65e55a0104bba302c03a1db1c66dd2e0
        // 统一地面高度：两个场景的"地面线"不一样，不统一的话跨接缝会像在飞。
        if (!resident.hasGroundY)
        {
            resident.groundY = view.groundY;
            resident.hasGroundY = true;
        }

        view.transform.position = new Vector3(worldX, resident.groundY, view.transform.position.z);
    }

    private AreaId CurrentArea()
    {
        return GameManager.Instance != null ? GameManager.Instance.Area : AreaId.None;
    }

    /// <summary>当前场景里通往目标区域的传送点位置（NPC 会先走到这里再自动传送）。</summary>
    public bool TryGetPortalPosition(AreaId area, out Vector3 position)
    {
        ScenePortal2D portal = FindPortalTo(area);
        if (portal == null)
        {
            position = Vector3.zero;
            return false;
        }

        position = portal.transform.position;
        return true;
    }

    private void Publish(NpcActionRecord record, NpcActionResult result)
    {
        EventBus.Publish(new NpcActionEvent
        {
            actor = record.actor,
            action = record.action,
            target = record.target,
            itemId = record.itemId,
            area = record.area,
            placementId = record.placementId,
            order = record.order,
            executed = result.executed,
            contagion = record.contagion,
            motherKnock = record.motherKnock
        });

        Debug.Log(string.Format(
            "[NPC] {0} 第 {1} 个行动：{2}{3}{4}",
            record.actor.ToDisplayName(),
            record.order,
            record.action,
            string.IsNullOrEmpty(record.itemId) ? "" : " -> " + record.itemId,
            result.executed ? "" : "（未生效：" + result.message + "）"));
    }
}
