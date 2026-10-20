using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 游戏主流程状态机：负责“顶层状态 + 每轮五个阶段”的推进。
///
/// 每轮顺序（对应文档）：
///   RoundStart → SceneState（不回家判定）→ PlayerInput → ItemSettle → NpcAction → RoundEnd
///
/// 它只负责“什么时候轮到谁”，不负责具体玩法：
///   - 玩家碰了东西，调用 RegisterPlayerTouch；
///   - 物品/数值系统结算完，可以不管（该阶段自动过）；
///   - NPC 行为系统演完，调用 NotifyNpcActionDone。
///
/// 谁在用：GameRoot 持有并 StartNewGame；GameManager 上报玩家触碰；NpcPhaseDriver 上报行动演完；
/// PauseDirector 调 Pause / Resume；DebugOverlay 调 ForceAdvancePhase。
/// `Phase` 属性是外部查询阶段的唯一入口（不再广播阶段事件）。
/// </summary>
[DefaultExecutionOrder(-900)]
public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance { get; private set; }

    private GameConfig config;
    private RandomService random;
    private GameSession session;

    private Coroutine loop;
    private bool playerInputDone;
    private bool npcActionDone;
    private bool npcActionWaiting;
    private float npcActionLastProgressAt;
    private float previousTimeScale = 1f;

    public GameState State { get; private set; } = GameState.Boot;
    public TurnPhase Phase { get; private set; } = TurnPhase.None;
    public GameSession Session { get { return session; } }
    public GameConfig Config { get { return config; } }
    public bool IsRunning { get { return session != null && session.isRunning; } }

    /// <summary>场景状态阶段要往场上补物品，由 GameRoot 注入。</summary>
    public ItemSystem Items { get; private set; }

    /// <summary>结算母亲开门用，由 GameRoot 注入。</summary>
    public StatSettlement Stats { get; private set; }

    public void AttachItemSystem(ItemSystem itemSystem)
    {
        Items = itemSystem;
    }

    public void AttachStats(StatSettlement stats)
    {
        Stats = stats;
    }

    /// <summary>玩家现在能不能移动：游戏中、没暂停、且不在 NPC 行动/收尾阶段。</summary>
    public bool CanPlayerMove
    {
        get
        {
            if (State != GameState.Playing) return false;
            switch (Phase)
            {
                case TurnPhase.None:
                case TurnPhase.RoundStart:
                case TurnPhase.SceneState:
                case TurnPhase.PlayerInput:
                case TurnPhase.ItemSettle:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>玩家现在能不能触碰物品（只在玩家输入阶段允许）。</summary>
    public bool CanPlayerInteract
    {
        get { return State == GameState.Playing && Phase == TurnPhase.PlayerInput; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Initialize(GameConfig gameConfig, RandomService randomService, GameSession gameSession)
    {
        config = gameConfig;
        random = randomService;
        session = gameSession;
        SetState(GameState.Boot);
        SetPhase(TurnPhase.None);
    }

    public void StartNewGame(int seed = -1)
    {
        if (config == null || random == null || session == null)
        {
            Debug.LogError("GameFlow 还没初始化，无法开局。");
            return;
        }

        if (seed < 0) seed = RandomService.NewSeed();
        random.Reseed(seed);
        session.Reset(config, seed);
        session.isRunning = true;
        session.isGameOver = false;
        Time.timeScale = 1f;
        NarrationState.Reset();

        SetState(GameState.Playing);
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(RunGame());
    }

    private IEnumerator RunGame()
    {
        while (session != null && session.isRunning)
        {
            yield return RunRound();

            if (session.round > config.totalRounds)
            {
                EndGame();
                yield break;
            }
        }
        loop = null;
    }

    private IEnumerator RunRound()
    {
        // ① 场景状态：住户默认都在门里（回家），沿用上一轮结束时定下的「谁不回家」。
        SetPhase(TurnPhase.RoundStart);
        session.BeginRound(session.hasAbsentResident, session.absentResident);
        EventBus.Publish(new RoundStartedEvent
        {
            round = session.round,
            absentResident = session.absentResident,
            hasAbsentResident = session.hasAbsentResident
        });
        yield return PhaseBeat();

        // ② 场景状态：场上随机摆几件物品，保证玩家有东西可碰。
        SetPhase(TurnPhase.SceneState);
        if (Items != null && config != null)
        {
            Items.EnsureSceneItems(session.playerArea, config.sceneItemCount);
        }
        yield return PhaseBeat();

        // ② 玩家输入：只允许碰一次，等玩家碰完。
        SetPhase(TurnPhase.PlayerInput);
        playerInputDone = false;
        yield return WaitForFlag(delegate { return playerInputDone; }, 0f, "PlayerInput");

        // ③ 物品结算：温度在②触摸时就标好了，这一步只是节奏点。
        SetPhase(TurnPhase.ItemSettle);
        yield return PhaseBeat();

        // ④ 住户行为：玩家锁移动，相机拉远，等 NPC 系统演完。
        SetPhase(TurnPhase.NpcAction);
        npcActionDone = false;
        npcActionWaiting = true;
        npcActionLastProgressAt = Time.unscaledTime;
        EventBus.Publish(new NpcActionPhaseEvent { round = session.round });
        yield return WaitForNpcAction(config.npcPhaseTimeout);
        if (npcActionWaiting && npcActionDone == false)
        {
            Debug.LogWarning("NPC 行动阶段超时，已强制进入下一阶段。");
        }
        npcActionWaiting = false;

        // ⑤ 轮次 +1。不回家的判定已经在住户行动结束时做好（见 NpcSystem.ResolveEndOfRound）。
        SetPhase(TurnPhase.RoundEnd);
        EventBus.Publish(new RoundEndedEvent
        {
            round = session.round,
            absentResident = session.absentResident,
            hasAbsentResident = session.hasAbsentResident
        });
        yield return PhaseBeat();
        session.round++;
    }

    /// <summary>阶段之间的节奏点：给表现层留一点时间跟上（时长在 GameConfig.phaseStepDelay 调）。</summary>
    private IEnumerator PhaseBeat()
    {
        float delay = config != null ? Mathf.Max(0f, config.phaseStepDelay) : 0f;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        else yield return null;
    }

    private IEnumerator WaitForFlag(Func<bool> done, float timeout, string label)
    {
        float start = Time.unscaledTime;
        while (done != null && !done())
        {
            if (timeout > 0f && Time.unscaledTime - start >= timeout) break;
            yield return null;
        }
    }

    /// <summary>NPC 行动有进展就续期，超时只用于兜底真正卡死的协程。</summary>
    private IEnumerator WaitForNpcAction(float timeout)
    {
        while (!npcActionDone)
        {
            if (timeout > 0f && Time.unscaledTime - npcActionLastProgressAt >= timeout) break;
            yield return null;
        }
    }

    // ------------------------------------------------------------------
    // 给玩法系统调用的接口
    // ------------------------------------------------------------------

    /// <summary>
    /// 玩家触碰了一件物品。返回 true 表示这次触碰被接受（计入本轮次数）。
    /// 是否允许触碰（温度、二次触碰等）由物品系统判断后，再调用这里。
    /// </summary>
    public bool RegisterPlayerTouch(string itemId, ItemOwner owner = ItemOwner.None, bool hadTemperature = false)
    {
        if (!CanPlayerInteract) return false;
        if (session.touchesThisRound >= config.touchesPerRound) return false;

        session.touchesThisRound++;
        EventBus.Publish(new ItemTouchedEvent
        {
            itemId = itemId,
            owner = owner,
            hadTemperature = hadTemperature
        });

        if (session.touchesThisRound >= config.touchesPerRound) playerInputDone = true;
        return true;
    }

    /// <summary>玩家阶段提前结束（例如没有可碰的物品）。</summary>
    public void NotifyPlayerInputDone()
    {
        playerInputDone = true;
    }

    /// <summary>NPC 行动阶段结束（由 NPC 行为系统在演出完成后调用）。</summary>
    public void NotifyNpcActionDone()
    {
        npcActionDone = true;
    }

    /// <summary>NPC 行动每完成一小段就汇报一次，避免长动作链被总时限误杀。</summary>
    public void NotifyNpcActionProgress()
    {
        if (npcActionWaiting) npcActionLastProgressAt = Time.unscaledTime;
    }

    /// <summary>调试/兜底：强制结束当前等待。</summary>
    public void ForceAdvancePhase()
    {
        playerInputDone = true;
        npcActionDone = true;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        SetState(GameState.Playing);
    }

    public void EndGame()
    {
        if (session != null)
        {
            session.isRunning = false;
            session.isGameOver = true;
        }
        SetPhase(TurnPhase.GameOver);

        // 结算顺序：先判母亲开门，再判结局（文档 5.5）。
        if (session != null && config != null)
        {
            if (Stats != null && Stats.ResolveMotherDoorAtEnd())
            {
                int collected = Items != null ? Items.CollectMotherItems() : 0;
                Debug.Log("[母亲开门] 知晓 > 60：暖意跳到 61，一次性收走自己的东西 " + collected + " 件。");
            }
        }

        EndingResult ending = EndingRules.Resolve(session, config);
        Debug.Log("[结局判定] " + ending.title + "（" + ending.reason + "）");

        SetState(GameState.Ending);
        EventBus.Publish(new GameOverEvent
        {
            session = session,
            ending = ending
        });
    }

    public void SetState(GameState next)
    {
        if (State == next) return;
        GameState previous = State;
        State = next;
        EventBus.Publish(new GameStateChangedEvent { previous = previous, current = next });
    }

    public void SetPhase(TurnPhase next)
    {
        if (Phase == next) return;
        Phase = next;
    }
}
