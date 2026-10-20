using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏内「暂停时钟」HUD。
///
/// 不修改 FirstFloor / Backyard 场景，也不改主程序的代码：
/// 靠 RuntimeInitializeOnLoadMethod 在游戏启动时把预制件造出来并常驻。
/// （和主程序的 PromptOverlay / DebugOverlay 是同一套路）
///
/// ⚠️ 游戏场景里既没有 Canvas 也没有 EventSystem（游戏 HUD 是 IMGUI 画的），
///    所以 EventSystem 要在这里自己补。
/// </summary>
public class PauseButtonHUD : MonoBehaviour
{
    /// <summary>预制件放在 Assets/Resources/UI/ 下，填相对路径（不带扩展名）。</summary>
    private const string PrefabPath = "UI/PauseButtonHUD";

    /// <summary>只有这些场景显示暂停时钟。</summary>
    private static readonly string[] GameScenes = { "FirstFloor", "SecondFloor", "Backyard" };

    private GameObject instance;
    private Button pauseButton;
    private GameClockUI clock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject host = new GameObject("[PauseButtonHUD]");
        DontDestroyOnLoad(host);
        host.AddComponent<PauseButtonHUD>();
    }

    private void Start()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[暂停时钟] 找不到预制件：Assets/Resources/" + PrefabPath + ".prefab");
            return;
        }

        instance = Instantiate(prefab);
        instance.name = "PauseButtonHUD";
        DontDestroyOnLoad(instance);

        pauseButton = instance.GetComponentInChildren<Button>(true);
        if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
        else Debug.LogWarning("[暂停时钟] 预制件里没有 Button 组件。");

        clock = instance.GetComponentInChildren<GameClockUI>(true);
        if (clock == null) Debug.LogWarning("[暂停时钟] 预制件里没有 GameClockUI 组件。");
    }

    private void Update()
    {
        if (instance == null) return;

        bool inGame = IsGameScene(SceneManager.GetActiveScene().name);
        if (instance.activeSelf != inGame) instance.SetActive(inGame);

        // 时钟：只在「游戏场景里」且「没有暂停」时走
        if (clock != null) clock.Tick(inGame && Time.timeScale > 0f, Time.unscaledDeltaTime);

        if (!inGame) return;

        // 游戏场景没有 EventSystem —— 没有它按钮点不动。
        // 只在游戏场景里补，且不常驻（随场景销毁），
        // 这样主菜单 / 设置 / 暂停场景自带的 EventSystem 不会和它打架。
        if (EventSystem.current == null) CreateEventSystem();

        // 只在"玩家可操作"阶段能点
        if (pauseButton != null)
        {
            PauseDirector director = PauseDirector.Instance;
            pauseButton.interactable = director != null && director.CanPause;
        }
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("[PauseButtonHUD.EventSystem]");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();   // 本工程用旧版 Input Manager
    }

    private static bool IsGameScene(string sceneName)
    {
        for (int i = 0; i < GameScenes.Length; i++)
            if (GameScenes[i] == sceneName) return true;
        return false;
    }

    public void OnPauseClicked()
    {
        PauseDirector director = PauseDirector.Instance;
        if (director != null) director.PauseGame();
    }
}
