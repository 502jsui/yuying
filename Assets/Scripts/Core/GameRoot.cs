using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局服务根节点。它自己会在游戏启动时创建，并常驻（DontDestroyOnLoad），
/// 持有配置、随机源、本局数据、流程状态机和音频管理等基础设施。
///
/// 场景里只需要正常摆 View；场景绑定由 GameManager（每个场景一个）负责，
/// 切场景后 GameManager 会重新把场景 View 接到这份持久数据上。
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    public GameConfig Config { get; private set; }
    public GameText Text { get; private set; }
    public RandomService Random { get; private set; }
    public GameSession Session { get; private set; }
    public GameFlow Flow { get; private set; }
    public StatSettlement Stats { get; private set; }
    public ItemSystem Items { get; private set; }
    public NpcSystem Npc { get; private set; }
    public AudioManager Audio { get; private set; }
    public NarrationDirector Narration { get; private set; }

    /// <summary>当前场景的绑定器（GameManager）。</summary>
    public GameManager Scene { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject root = new GameObject("[GameRoot]");
        root.AddComponent<GameRoot>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Config = GameConfig.Load();
        Text = GameText.Load();
        Random = new RandomService(RandomService.NewSeed());
        Session = new GameSession();
        Session.Reset(Config, Random.Seed);

        Flow = gameObject.AddComponent<GameFlow>();
        Flow.Initialize(Config, Random, Session);
        Flow.SetState(GameState.MainMenu);

        Stats = new StatSettlement(Config, Session);
        Flow.AttachStats(Stats);
        Items = new ItemSystem(Config, Random, Session, Flow, Stats);
        Flow.AttachItemSystem(Items);

        Npc = new NpcSystem(Config, Random, Session, Items, Stats);
        gameObject.AddComponent<SceneStitcher>();
        NpcPhaseDriver npcDriver = gameObject.AddComponent<NpcPhaseDriver>();
        npcDriver.Initialize(Npc, Flow, Config);

        Audio = gameObject.AddComponent<AudioManager>();
        Audio.Initialize(Config, SfxLibrary.Load());

        gameObject.AddComponent<AudioDirector>();
        Narration = gameObject.AddComponent<NarrationDirector>();
        gameObject.AddComponent<PauseDirector>();
        gameObject.AddComponent<IntroNarration>();
        gameObject.AddComponent<DebugOverlay>();
        gameObject.AddComponent<PromptOverlay>();
        gameObject.AddComponent<SceneFadeOverlay>();
        gameObject.AddComponent<EndingDirector>();
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("[GameRoot] 基础设施初始化完成。");
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // GameManager 会自己注册；这里只记录一个兜底日志。
        Debug.Log("[GameRoot] 场景已加载：" + scene.name + "（" + mode + "）");
    }

    /// <summary>由场景里的 GameManager 在运行时调用。</summary>
    public void RegisterScene(GameManager scene)
    {
        if (scene == null) return;
        Scene = scene;
        scene.Bind();

        if (Config != null && Config.autoStartOnSceneLoad && !Flow.IsRunning && !Session.isGameOver)
        {
            StartNewGame();
        }
    }

    public void UnregisterScene(GameManager scene)
    {
        if (Scene == scene) Scene = null;
    }

    public void StartNewGame(int seed = -1)
    {
        if (seed < 0) seed = RandomService.NewSeed();
        Flow.StartNewGame(seed);
        if (Items != null) Items.InitializeFromConfig();
        if (Scene != null) Scene.Bind();
        Debug.Log("[GameRoot] 新开一局，seed=" + seed);
    }

    public bool SaveDebugSnapshot()
    {
        return SessionStore.Save(Session);
    }

    public bool LoadDebugSnapshot()
    {
        return SessionStore.Load(Session);
    }
}
