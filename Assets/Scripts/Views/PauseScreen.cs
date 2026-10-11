using UnityEngine;

/// <summary>
/// 暂停场景里的表现：把 PauseSnapshot 里那张"暂停瞬间的画面"铺满屏幕，
/// 并提供三个按钮回调（返回游戏 / 设置 / 退出）给 UI 同学接。
///
/// 按钮还没做的时候，键盘也能走通：ESC 返回游戏、S 设置、Q 退出。
/// </summary>
public class PauseScreen : MonoBehaviour
{
    [Tooltip("铺背景图用的 SpriteRenderer；留空会自动找名为 Background 的子物体。")]
    public SpriteRenderer background;

    [Tooltip("没截到图时（无渲染设备 / 截图失败）铺的底色。")]
    public Color fallbackColor = new Color(0.03f, 0.04f, 0.06f, 1f);

    [Header("键盘兜底（UI 接上按钮后可以留着，也可以去掉）")]
    public KeyCode resumeKey = KeyCode.Escape;
    public KeyCode settingsKey = KeyCode.S;
    public KeyCode quitKey = KeyCode.Q;

    private Sprite runtimeSprite;

    private void Awake()
    {
        if (background == null)
        {
            Transform found = transform.Find("Background");
            if (found != null) background = found.GetComponent<SpriteRenderer>();
        }
        if (background == null) background = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        ApplySnapshot();
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);

        // 暂停场景卸载 = 这张截图没用了，释放掉（站位信息另留）。
        PauseSnapshot.ReleaseTexture();
    }

    private void Update()
    {
        if (Input.GetKeyDown(resumeKey)) OnResumeClicked();
        else if (Input.GetKeyDown(settingsKey)) OnSettingsClicked();
        else if (Input.GetKeyDown(quitKey)) OnQuitClicked();
    }

    // ------------------------------------------------------------------
    // UI 按钮回调（UI 同学在按钮上挂这三个）
    // ------------------------------------------------------------------

    public void OnResumeClicked()
    {
        if (PauseDirector.Instance != null) PauseDirector.Instance.ResumeGame();
    }

    public void OnSettingsClicked()
    {
        if (PauseDirector.Instance != null) PauseDirector.Instance.OpenSettings();
    }

    public void OnQuitClicked()
    {
        if (PauseDirector.Instance != null) PauseDirector.Instance.QuitGame();
    }

    // ------------------------------------------------------------------
    // 背景
    // ------------------------------------------------------------------

    private void ApplySnapshot()
    {
        if (background == null) return;

        Texture2D texture = PauseSnapshot.Texture;
        if (texture != null)
        {
            runtimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            background.sprite = runtimeSprite;
            background.color = Color.white;
        }
        else
        {
            // 没截到图：铺一块纯色，保证暂停界面不是透明的。
            runtimeSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            background.sprite = runtimeSprite;
            background.color = fallbackColor;
        }

        CoverCamera();
    }

    /// <summary>把背景按"铺满不开黑边"的比例对齐相机可见范围。</summary>
    private void CoverCamera()
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic || background == null || background.sprite == null) return;

        float viewHeight = camera.orthographicSize * 2f;
        float viewWidth = viewHeight * camera.aspect;

        Vector2 spriteSize = background.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

        float spriteAspect = spriteSize.x / spriteSize.y;
        float viewAspect = viewWidth / Mathf.Max(0.0001f, viewHeight);

        float scale = spriteAspect >= viewAspect
            ? viewHeight / spriteSize.y      // 图更宽：按高度铺满，左右裁掉
            : viewWidth / spriteSize.x;      // 图更高：按宽度铺满，上下裁掉

        background.transform.localScale = new Vector3(scale, scale, 1f);
        background.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
    }
}
