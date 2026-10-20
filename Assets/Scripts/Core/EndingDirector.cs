using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 三档结局演出。它订阅 GameOverEvent，自动播放 PDF 中的结算脚本，
/// 并在结束时回到主菜单。已有 UI 只在运行时临时关闭，不改其脚本 / 预制件 / 场景。
/// </summary>
[DefaultExecutionOrder(-650)]
public class EndingDirector : MonoBehaviour
{
    public static EndingDirector Instance { get; private set; }

    [Header("播放")]
    [Tooltip("正常游戏结束时自动播放；测试可关掉后手动调用 Play。")]
    public bool autoPlayOnGameOver = true;
    [Tooltip("测试或调试时可加速；正常游戏保持 1。")]
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Tooltip("长按这个键跳到结束标题。")]
    public KeyCode skipKey = KeyCode.Space;
    [Tooltip("第二个跳键。")]
    public KeyCode alternateSkipKey = KeyCode.Escape;
    [Tooltip("需要按住多久才跳。")]
    [Min(0.1f)] public float skipHoldSeconds = 1.5f;
    [Tooltip("结束标题停留时间。")]
    [Min(0.1f)] public float finalTitleSeconds = 4f;

    public bool IsPlaying { get; private set; }
    public EndingId CurrentEnding { get; private set; }
    public string CurrentSubtitle { get; private set; }
    public bool IsFinalTitle { get; private set; }

    private EndingSubtitleOverlay overlay;
    private EndingResult currentResult;
    private Coroutine routine;
    private SceneCameraFollow2D cameraFraming;
    private Transform player;
    private SpriteRenderer playerBody;
    private bool playerWasVisible;
    private float skipHeld;
    private bool skipRequested;
    private int timeCardCount;

    private PromptOverlay promptOverlay;
    private DebugOverlay debugOverlay;
    private AudioDirector audioDirector;
    private PauseButtonHUD pauseHud;
    private GameObject pauseHudCanvas;
    private bool promptWasEnabled;
    private bool debugWasEnabled;
    private bool audioDirectorWasEnabled;
    private bool pauseHudWasEnabled;
    private bool pauseCanvasWasActive;
    private bool uiCached;

    private readonly List<ResidentView> residentViews = new List<ResidentView>();
    private readonly Dictionary<ResidentView, Vector3> bodyScales = new Dictionary<ResidentView, Vector3>();
    private readonly List<RainController> rainControllers = new List<RainController>();
    private readonly List<GameObject> hiddenItems = new List<GameObject>();
    private GameObject snowObject;
    private Sprite snowSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        overlay = gameObject.AddComponent<EndingSubtitleOverlay>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<GameOverEvent>(OnGameOver);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameOverEvent>(OnGameOver);
        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsPlaying = false;
        RestoreExistingUi();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        if (Input.GetKey(skipKey) || Input.GetKey(alternateSkipKey))
        {
            skipHeld += Time.unscaledDeltaTime;
            if (skipHeld >= skipHoldSeconds)
            {
                skipHeld = 0f;
                skipRequested = true;
            }
        }
        else
        {
            skipHeld = 0f;
        }
    }

    private void OnGameOver(GameOverEvent evt)
    {
        if (autoPlayOnGameOver && IsGameplayScene(SceneManager.GetActiveScene().name))
            Play(evt.ending);
    }

    /// <summary>开始一套结局演出。测试也可以直接调用。</summary>
    public void Play(EndingResult result)
    {
        if (IsPlaying) return;
        if (string.IsNullOrEmpty(result.title)) result.title = EndingRules.TitleOf(result.id);

        currentResult = result;
        CurrentEnding = result.id;
        routine = StartCoroutine(RunEnding(result));
    }

    /// <summary>测试入口：跳过全部等待，只让最终标题和返回逻辑走完。</summary>
    public void RequestSkipToFinalTitle()
    {
        skipRequested = true;
    }

    private IEnumerator RunEnding(EndingResult result)
    {
        IsPlaying = true;
        IsFinalTitle = false;
        CurrentSubtitle = null;
        skipHeld = 0f;
        skipRequested = false;
        timeCardCount = 0;

        overlay.visible = true;
        overlay.line = null;
        overlay.speaker = null;
        overlay.subtitleAlpha = 0f;
        overlay.timeCard = null;
        overlay.timeCardAlpha = 0f;
        overlay.finalTitle = null;
        overlay.finalTitleAlpha = 0f;
        overlay.darkness = 0.18f;
        overlay.blackAlpha = 1f;
        overlay.whiteAlpha = 0f;

        CacheAndDisableExistingUi();
        PrepareAudio(result.id);

        yield return PrepareStage();
        yield return FadeBlack(1f, 0f, 0.8f);

        GameText text = GameRoot.Instance != null ? GameRoot.Instance.Text : GameText.Load();
        EndingScript script = text != null ? text.EndingScriptFor(result.id) : null;

        if (script != null && script.lines != null)
        {
            for (int i = 0; i < script.lines.Count; i++)
            {
                if (skipRequested) break;
                EndingLine line = script.lines[i];
                if (line == null) continue;

                ApplyCue(line);

                if (line.cue == EndingCue.TimeCard)
                {
                    yield return ShowTimeCard(line.cueText, line.cueSeconds);
                }
                else if (line.HasDialogue)
                {
                    yield return ShowSubtitle(line);
                }

                if (line.pauseAfterSeconds > 0f)
                    yield return WaitScaled(line.pauseAfterSeconds);
            }
        }

        yield return PlayFinalTitle(result.title);
        yield return ReturnToMainMenu();
    }

    private IEnumerator PrepareStage()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene != "FirstFloor")
        {
            SceneTransitionState.BeginLoad();
            while (SceneTransitionState.FadeAlpha < 0.99f) yield return null;

            AsyncOperation load = SceneManager.LoadSceneAsync("FirstFloor", LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;
            SceneTransitionState.EndLoad();
            yield return null;
        }

        SceneStitcher stitcher = SceneStitcher.Instance;
        if (stitcher != null) yield return stitcher.Begin(AreaId.FirstFloor);

        CacheStageObjects();
        HideGameplayVisuals();
        SetPlayerPose(0f, true);
        SetCameraCorridor();
    }

    private void CacheStageObjects()
    {
        residentViews.Clear();
        bodyScales.Clear();
        rainControllers.Clear();

        Scene[] scenes = GetAllLoadedScenes();
        for (int i = 0; i < scenes.Length; i++)
        {
            GameObject[] roots = scenes[i].GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                ResidentView[] residents = roots[r].GetComponentsInChildren<ResidentView>(true);
                for (int j = 0; j < residents.Length; j++)
                {
                    ResidentView view = residents[j];
                    if (view == null) continue;
                    residentViews.Add(view);
                    if (view.bodyRenderer != null)
                        bodyScales[view] = view.bodyRenderer.transform.localScale;
                }

                RainController[] rains = roots[r].GetComponentsInChildren<RainController>(true);
                for (int rainIndex = 0; rainIndex < rains.Length; rainIndex++)
                {
                    if (rains[rainIndex] != null && rains[rainIndex].isActiveAndEnabled)
                        rainControllers.Add(rains[rainIndex]);
                }

                NpcPatrol2D[] patrols = roots[r].GetComponentsInChildren<NpcPatrol2D>(true);
                for (int p = 0; p < patrols.Length; p++)
                    if (patrols[p] != null) patrols[p].enabled = false;

                MotherDoorView[] motherDoors = roots[r].GetComponentsInChildren<MotherDoorView>(true);
                for (int m = 0; m < motherDoors.Length; m++)
                    if (motherDoors[m] != null) motherDoors[m].enabled = false;
            }
        }

        Camera camera = Camera.main;
        cameraFraming = camera != null ? camera.GetComponent<SceneCameraFollow2D>() : null;
        GameManager manager = GameManager.Instance;
        player = manager != null && manager.player != null ? manager.player.transform : FindPlayerTransform();
        playerBody = manager != null && manager.player != null
            ? manager.player.bodyRenderer
            : FindPlayerBody();
        playerWasVisible = playerBody == null || playerBody.enabled;
    }

    private void HideGameplayVisuals()
    {
        for (int i = 0; i < residentViews.Count; i++) SetResidentVisible(residentViews[i], false);

        hiddenItems.Clear();
        Scene[] scenes = GetAllLoadedScenes();
        for (int i = 0; i < scenes.Length; i++)
        {
            GameObject[] roots = scenes[i].GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                ItemView[] items = roots[r].GetComponentsInChildren<ItemView>(true);
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j] == null || !items[j].gameObject.activeSelf) continue;
                    hiddenItems.Add(items[j].gameObject);
                    items[j].gameObject.SetActive(false);
                }
            }
        }
    }

    private void ApplyCue(EndingLine line)
    {
        if (line == null || line.cue == EndingCue.None) return;

        switch (line.cue)
        {
            case EndingCue.RainHeavy:
                SetRainVisible(true);
                SetRainAudio(1f, 0.5f);
                break;

            case EndingCue.LightsOut:
                StartCoroutine(DimFirstFloorLights(8f));
                StartCoroutine(PlayDoorSequence(SfxId.DoorClose, 4));
                break;

            case EndingCue.FocusMotherDoor:
                FocusMotherDoor();
                break;

            case EndingCue.TimeCard:
                timeCardCount++;
                if (CurrentEnding == EndingId.WhenSheWasLittle)
                    SetRainAudio(Mathf.Max(0.08f, 0.28f - timeCardCount * 0.05f), 3f);
                break;

            case EndingCue.AllAtDoors:
                ShowAllAtDoors();
                SetCameraCorridor();
                break;

            case EndingCue.GirlOlder:
                ShowGirlOnly(2.2f, 1.22f);
                FocusGirl();
                break;

            case EndingCue.GirlLeaving:
                ShowGirlOnly(6.6f, 1.32f);
                SetCameraCorridor();
                break;

            case EndingCue.GirlAlone:
                HideAllResidentBodies();
                break;

            case EndingCue.DoorsOpen:
                ShowAllAtDoors();
                SetAllDoorsOpen();
                SetCameraWide();
                StartCoroutine(PlayDoorSequence(SfxId.DoorOpen, 4));
                break;

            case EndingCue.RainFadeOut:
                SetRainAudio(0f, 8f);
                break;

            case EndingCue.SnowStart:
                SetRainVisible(false);
                StartSnow();
                break;

            case EndingCue.WalkToExit:
                StartCoroutine(WalkPlayerTo(8.6f, 14f));
                break;

            case EndingCue.FadeToWhite:
                StartCoroutine(FadeWhiteOverlay(5f));
                break;
        }
    }

    private IEnumerator ShowSubtitle(EndingLine line)
    {
        CurrentSubtitle = line.text;
        overlay.speaker = line.speaker;
        overlay.line = line.text;
        overlay.subtitleAlpha = 0f;

        float fade = 0.22f;
        yield return FadeSubtitle(to: 1f, seconds: fade);
        yield return WaitScaled(ResolveHoldSeconds(line));
        yield return FadeSubtitle(to: 0f, seconds: fade);

        overlay.line = null;
        overlay.speaker = null;
        CurrentSubtitle = null;
    }

    private IEnumerator ShowTimeCard(string text, float seconds)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        overlay.timeCard = text;
        overlay.timeCardAlpha = 0f;
        yield return FadeTimeCard(1f, 0.35f);
        yield return WaitScaled(seconds > 0f ? seconds : 2.4f);
        yield return FadeTimeCard(0f, 0.35f);
        overlay.timeCard = null;
    }

    private IEnumerator PlayFinalTitle(string title)
    {
        if (skipRequested)
        {
            overlay.blackAlpha = 1f;
            overlay.darkness = 0f;
        }

        IsFinalTitle = true;
        CurrentSubtitle = null;
        overlay.speaker = null;
        overlay.line = null;
        overlay.subtitleAlpha = 0f;
        overlay.timeCard = null;
        overlay.timeCardAlpha = 0f;
        overlay.finalTitle = string.IsNullOrEmpty(title) ? EndingRules.TitleOf(currentResult.id) : title;
        overlay.finalTitleAlpha = 0f;

        yield return FadeFinalTitle(1f, 0.8f);
        yield return WaitScaled(finalTitleSeconds, ignoreSkip: true);
        yield return FadeFinalTitle(0f, 0.5f);
    }

    private IEnumerator ReturnToMainMenu()
    {
        if (!skipRequested) yield return FadeBlack(0f, 1f, 0.6f);
        else overlay.blackAlpha = 1f;

        RestoreAudio();
        RestoreExistingUi();
        CleanupStage();
        overlay.visible = false;

        PauseDirector pause = PauseDirector.Instance;
        if (pause != null)
        {
            pause.ReturnToMainMenu();
        }
        else
        {
            GameRoot root = GameRoot.Instance;
            if (root != null && root.Session != null)
            {
                root.Session.isRunning = false;
                root.Session.isGameOver = false;
            }
            if (root != null && root.Flow != null) root.Flow.SetState(GameState.MainMenu);
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }

        IsPlaying = false;
        IsFinalTitle = false;
        routine = null;
    }

    private float ResolveHoldSeconds(EndingLine line)
    {
        if (line.holdSeconds > 0f) return line.holdSeconds;
        int count = string.IsNullOrEmpty(line.text) ? 0 : line.text.Length;
        return Mathf.Clamp(2.2f + count * 0.06f, 2.2f, 4.8f);
    }

    private IEnumerator WaitScaled(float seconds, bool ignoreSkip = false)
    {
        float elapsed = 0f;
        float scale = Mathf.Max(0.01f, playbackSpeed);
        while (elapsed < Mathf.Max(0f, seconds))
        {
            if (!ignoreSkip && skipRequested) yield break;
            elapsed += Time.unscaledDeltaTime * scale;
            yield return null;
        }
    }

    private IEnumerator FadeSubtitle(float to, float seconds)
    {
        float from = overlay.subtitleAlpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skipRequested) { overlay.subtitleAlpha = to; yield break; }
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            overlay.subtitleAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.subtitleAlpha = to;
    }

    private IEnumerator FadeTimeCard(float to, float seconds)
    {
        float from = overlay.timeCardAlpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skipRequested) { overlay.timeCardAlpha = to; yield break; }
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            overlay.timeCardAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.timeCardAlpha = to;
    }

    private IEnumerator FadeFinalTitle(float to, float seconds)
    {
        float from = overlay.finalTitleAlpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            overlay.finalTitleAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.finalTitleAlpha = to;
    }

    private IEnumerator FadeBlack(float from, float to, float seconds)
    {
        overlay.blackAlpha = from;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skipRequested && to > from) { overlay.blackAlpha = to; yield break; }
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            overlay.blackAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.blackAlpha = to;
    }

    private IEnumerator FadeWhiteOverlay(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            overlay.whiteAlpha = Mathf.Lerp(0f, 0.82f, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        overlay.whiteAlpha = 0.82f;
    }

    private IEnumerator DimFirstFloorLights(float seconds)
    {
        List<SpriteRenderer> lights = new List<SpriteRenderer>();
        for (int i = 0; i < residentViews.Count; i++)
        {
            ResidentView view = residentViews[i];
            if (view == null || view.area != AreaId.FirstFloor || view.doorLight == null) continue;
            if (view.doorLight.lightRenderer != null) lights.Add(view.doorLight.lightRenderer);
        }

        Color[] startColors = new Color[lights.Count];
        for (int i = 0; i < lights.Count; i++) startColors[i] = lights[i].color;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            float t = Mathf.Clamp01(elapsed / seconds);
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] == null) continue;
                lights[i].color = Color.Lerp(startColors[i], new Color(0.08f, 0.075f, 0.065f, 0.35f), t);
            }
            yield return null;
        }
    }

    private IEnumerator PlayDoorSequence(SfxId id, int count)
    {
        AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
        if (audio == null) yield break;

        for (int i = 0; i < count; i++)
        {
            if (skipRequested) yield break;
            audio.PlaySfx(id, 0.72f);
            yield return WaitScaled(1.15f);
        }
    }

    private void FocusMotherDoor()
    {
        if (cameraFraming == null) return;
        Transform door = FindTransform("Door_Mother");
        if (door == null)
        {
            SetCameraCorridor();
            return;
        }

        Vector3 center = door.position + new Vector3(0f, 0.8f, 0f);
        cameraFraming.AnimateTo(center, 2.8f, 2f);

        DoorLightView light = door.GetComponentInChildren<DoorLightView>(true);
        if (light == null) light = FindDoorLightInScene("Door_Mother");
        if (light != null)
        {
            light.Apply(61f, 63f, true);
            if (light.doorLeaf != null) light.doorLeaf.localScale = new Vector3(0.82f, 1f, 1f);
        }
    }

    private void ShowAllAtDoors()
    {
        for (int i = 0; i < residentViews.Count; i++)
        {
            ResidentView view = residentViews[i];
            if (view == null) continue;

            if (view.id == ResidentId.Mother)
            {
                SetResidentVisible(view, false);
                continue;
            }

            SetResidentAtHome(view);
            SetResidentVisible(view, true);
            SetBodyScale(view, 1f);
        }
    }

    private void ShowGirlOnly(float worldX, float scale)
    {
        HideAllResidentBodies();
        ResidentView girl = FindResident(ResidentId.Girl, AreaId.FirstFloor);
        if (girl == null) return;

        SetResidentVisible(girl, true);
        SetBodyScale(girl, scale);
        if (girl.transform != null)
        {
            Vector3 position = girl.transform.position;
            position.x = worldX;
            girl.transform.position = position;
        }
    }

    private void FocusGirl()
    {
        ResidentView girl = FindResident(ResidentId.Girl, AreaId.FirstFloor);
        if (girl == null || cameraFraming == null) return;
        cameraFraming.AnimateTo(girl.transform.position + Vector3.up * 1f, 3.2f, 1.4f);
    }

    private void HideAllResidentBodies()
    {
        for (int i = 0; i < residentViews.Count; i++) SetResidentVisible(residentViews[i], false);
    }

    private void SetAllDoorsOpen()
    {
        for (int i = 0; i < residentViews.Count; i++)
        {
            ResidentView view = residentViews[i];
            if (view == null || view.doorLight == null) continue;
            view.doorLight.Apply(61f, 63f, true);
            if (view.doorLight.doorLeaf != null)
                view.doorLight.doorLeaf.localScale = new Vector3(0.15f, 1f, 1f);
        }

        DoorLightView mother = FindDoorLightInScene("Door_Mother");
        if (mother != null)
        {
            mother.Apply(61f, 63f, true);
            if (mother.doorLeaf != null) mother.doorLeaf.localScale = new Vector3(0.15f, 1f, 1f);
        }
    }

    private void SetCameraCorridor()
    {
        if (cameraFraming == null) return;
        cameraFraming.AnimateToBounds(-8.8f, 8.8f, -3.8f, 3.2f, 1.2f);
    }

    private void SetCameraWide()
    {
        if (cameraFraming == null) return;
        float minX, maxX, minY, maxY;
        SceneStitcher.WorldBounds(out minX, out maxX, out minY, out maxY);
        cameraFraming.AnimateToBounds(minX, maxX, minY, maxY, 1.2f);
    }

    private IEnumerator WalkPlayerTo(float targetX, float seconds)
    {
        if (player == null) yield break;
        float startX = player.position.x;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (skipRequested) break;
            elapsed += Time.unscaledDeltaTime * Mathf.Max(0.01f, playbackSpeed);
            Vector3 position = player.position;
            position.x = Mathf.Lerp(startX, targetX, Mathf.Clamp01(elapsed / seconds));
            player.position = position;
            yield return null;
        }
    }

    private void SetPlayerPose(float x, bool visible)
    {
        if (player != null)
        {
            Vector3 position = player.position;
            position.x = x;
            player.position = position;
        }
        if (playerBody != null) playerBody.enabled = visible;
    }

    private void SetResidentAtHome(ResidentView view)
    {
        if (view == null || view.transform == null) return;
        Vector3 position = view.transform.position;
        Vector3 home = view.HomePosition;
        position.x = SceneStitcher.WorldX(view.area, home.x);
        position.y = home.y + SceneStitcher.OffsetYFor(view.area);
        view.transform.position = position;
    }

    private void SetResidentVisible(ResidentView view, bool visible)
    {
        if (view == null || view.bodyRenderer == null) return;
        view.bodyRenderer.enabled = visible;
    }

    private void SetBodyScale(ResidentView view, float scale)
    {
        if (view == null || view.bodyRenderer == null) return;
        Vector3 original;
        if (!bodyScales.TryGetValue(view, out original)) original = view.bodyRenderer.transform.localScale;
        view.bodyRenderer.transform.localScale = original * Mathf.Max(0.1f, scale);
    }

    private ResidentView FindResident(ResidentId id, AreaId area)
    {
        for (int i = 0; i < residentViews.Count; i++)
        {
            ResidentView view = residentViews[i];
            if (view != null && view.id == id && view.area == area) return view;
        }
        return null;
    }

    private DoorLightView FindDoorLightInScene(string rootName)
    {
        Transform root = FindTransform(rootName);
        return root != null ? root.GetComponentInChildren<DoorLightView>(true) : null;
    }

    private Transform FindTransform(string objectName)
    {
        Scene[] scenes = GetAllLoadedScenes();
        for (int i = 0; i < scenes.Length; i++)
        {
            GameObject[] roots = scenes[i].GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null && transforms[t].name == objectName) return transforms[t];
                }
            }
        }
        return null;
    }

    private Transform FindPlayerTransform()
    {
        PlayerController controller = FindObjectOfType<PlayerController>();
        return controller != null ? controller.transform : null;
    }

    private SpriteRenderer FindPlayerBody()
    {
        PlayerController controller = FindObjectOfType<PlayerController>();
        if (controller == null) return null;
        if (controller.bodyRenderer != null) return controller.bodyRenderer;
        Transform body = controller.transform.Find("Body");
        return body != null ? body.GetComponent<SpriteRenderer>() : null;
    }

    private static Scene[] GetAllLoadedScenes()
    {
        Scene[] scenes = new Scene[SceneManager.sceneCount];
        for (int i = 0; i < scenes.Length; i++) scenes[i] = SceneManager.GetSceneAt(i);
        return scenes;
    }

    private static bool IsGameplayScene(string sceneName)
    {
        return sceneName == "FirstFloor" || sceneName == "SecondFloor" || sceneName == "Backyard";
    }

    private void PrepareAudio(EndingId id)
    {
        AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
        if (audio == null) return;

        audio.StopMusicWithFade(1f);
        if (id == EndingId.RainInTheCorridor) audio.FadeRainTo(1f, 0.5f);
        else if (id == EndingId.WhenSheWasLittle) audio.FadeRainTo(0.28f, 1.5f);
        else audio.FadeRainTo(0.4f, 1f);
    }

    private void SetRainAudio(float intensity, float seconds)
    {
        AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
        if (audio != null) audio.FadeRainTo(intensity, seconds);
    }

    private void SetRainVisible(bool visible)
    {
        for (int i = 0; i < rainControllers.Count; i++)
        {
            if (rainControllers[i] != null) rainControllers[i].gameObject.SetActive(visible);
        }
    }

    private void StartSnow()
    {
        if (snowObject != null) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        float minX, maxX, minY, maxY;
        SceneStitcher.WorldBounds(out minX, out maxX, out minY, out maxY);
        float width = Mathf.Max(20f, maxX - minX);
        float height = Mathf.Max(12f, maxY - minY);

        snowSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        snowObject = new GameObject("Ending_Snow");
        snowObject.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);

        RainStreakField2D snow = snowObject.AddComponent<RainStreakField2D>();
        snow.blockBelowGround = false;
        snow.Configure(
            snowSprite,
            220,
            width,
            height,
            -100f,
            new Vector2(0.35f, -2.2f),
            0.055f,
            0.055f,
            new Color(0.96f, 0.98f, 1f, 0.82f),
            26,
            0f);
    }

    private void CacheAndDisableExistingUi()
    {
        if (!uiCached)
        {
            promptOverlay = GetComponent<PromptOverlay>();
            debugOverlay = GetComponent<DebugOverlay>();
            audioDirector = FindObjectOfType<AudioDirector>();
            pauseHud = FindObjectOfType<PauseButtonHUD>();
            pauseHudCanvas = GameObject.Find("PauseButtonHUD");
            promptWasEnabled = promptOverlay != null && promptOverlay.enabled;
            debugWasEnabled = debugOverlay != null && debugOverlay.enabled;
            audioDirectorWasEnabled = audioDirector != null && audioDirector.enabled;
            pauseHudWasEnabled = pauseHud != null && pauseHud.enabled;
            pauseCanvasWasActive = pauseHudCanvas != null && pauseHudCanvas.activeSelf;
            uiCached = true;
        }

        if (promptOverlay != null) promptOverlay.enabled = false;
        if (debugOverlay != null) debugOverlay.enabled = false;
        if (audioDirector != null) audioDirector.enabled = false;
        if (pauseHud != null) pauseHud.enabled = false;
        if (pauseHudCanvas != null) pauseHudCanvas.SetActive(false);
    }

    private void RestoreExistingUi()
    {
        if (!uiCached) return;
        if (promptOverlay != null) promptOverlay.enabled = promptWasEnabled;
        if (debugOverlay != null) debugOverlay.enabled = debugWasEnabled;
        if (audioDirector != null) audioDirector.enabled = audioDirectorWasEnabled;
        if (pauseHudCanvas != null) pauseHudCanvas.SetActive(pauseCanvasWasActive);
        if (pauseHud != null) pauseHud.enabled = pauseHudWasEnabled;
        uiCached = false;
    }

    private void RestoreAudio()
    {
        AudioManager audio = GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
        if (audio == null) return;

        audio.SetRainIntensity(0f);
        GameConfig config = GameRoot.Instance != null ? GameRoot.Instance.Config : null;
        if (config != null && config.autoPlayMusicOnStart && config.musicLoop != null)
            audio.PlayMusicIfDifferent(config.musicLoop, true, 0.6f);
    }

    private void CleanupStage()
    {
        for (int i = 0; i < hiddenItems.Count; i++)
        {
            if (hiddenItems[i] != null) hiddenItems[i].SetActive(true);
        }
        hiddenItems.Clear();

        if (snowObject != null) Destroy(snowObject);
        snowObject = null;
        if (snowSprite != null) Destroy(snowSprite);
        snowSprite = null;

        if (playerBody != null) playerBody.enabled = playerWasVisible;
        player = null;
        playerBody = null;
        cameraFraming = null;
    }
}
