using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public class RainController : MonoBehaviour
{
    [Header("世界范围")]
    public float width = 60f;
    public float height = 14f;
    public float groundY = -3.4f;

    [Header("强度")]
    [Range(0f, 1.5f)] public float intensity = 1f;
    public float backgroundRate = 200f;
    public float foregroundRate = 110f;

    [Header("覆盖范围")]
    [Tooltip("雨场在相机可视范围外再往外扩多少世界单位，保证屏幕边缘也被雨覆盖。")]
    public float coverMargin = 3f;

    [Header("雨落地面")]
    [Tooltip("雨丝打在地面上的水花 + 涟漪。默认开启。")]
    public bool groundSplash = true;

    [Header("积水（需求：默认关闭）")]
    [Tooltip("地面水光 / 水洼。关掉后地面不留积水。")]
    public bool groundWater = false;

    public float backgroundSpeed = 22f;
    public float foregroundSpeed = 32f;
    public float windSpeed = 8.0f;

    [Header("颜色")]
    public Color rainColor = new Color(0.62f, 0.78f, 1f, 0.42f);
    public Color splashColor = new Color(0.74f, 0.88f, 1f, 0.46f);
    public Color tintColor = new Color(0.06f, 0.14f, 0.28f, 0.10f);
    public Color lightningColor = new Color(0.80f, 0.90f, 1f, 0.40f);

    [Header("水面涟漪")]
    public bool rippleEnabled = true;
    public float rippleInterval = 0.09f;
    public float rippleLife = 0.40f;
    public float rippleSize = 0.45f;

    [Header("闪电")]
    public bool lightningEnabled = true;
    public float minLightningInterval = 6f;
    public float maxLightningInterval = 14f;

    private bool built;
    private Sprite whiteSprite;
    private Sprite ringSprite;
    private Texture2D ringTexture;
    private Material particleMaterial;
    private RainStreakField2D backgroundRain;
    private RainStreakField2D foregroundRain;
    private ParticleSystem splashRain;
    private SpriteRenderer tintOverlay;
    private SpriteRenderer lightningOverlay;
    private Coroutine rippleRoutine;
    private Coroutine lightningRoutine;
    private int backgroundStreakCount = 200;
    private int foregroundStreakCount = 110;
    private const float BaseViewWidth = 19.2f;

    /// <summary>拼接阶段：地面溅水 / 涟漪只允许出现在露天的这半边（屋顶下不出现）。</summary>
    private bool splashRangeRestricted;
    private float splashMinX;
    private float splashMaxX;

    public bool IsBuilt { get { return built; } }
    public RainStreakField2D BackgroundRain { get { return backgroundRain; } }
    public RainStreakField2D ForegroundRain { get { return foregroundRain; } }
    public ParticleSystem SplashRain { get { return splashRain; } }

    private void Awake()
    {
        if (gameObject.scene.name == "FirstFloor")
        {
            gameObject.SetActive(false);
            return;
        }

        Build();
    }

    private void OnEnable()
    {
        if (!built) Build();
        if (rippleRoutine == null && rippleEnabled && groundSplash) rippleRoutine = StartCoroutine(RippleLoop());
        if (lightningRoutine == null && lightningEnabled) lightningRoutine = StartCoroutine(LightningLoop());
    }

    private void OnDisable()
    {
        if (rippleRoutine != null) { StopCoroutine(rippleRoutine); rippleRoutine = null; }
        if (lightningRoutine != null) { StopCoroutine(lightningRoutine); lightningRoutine = null; }
    }

    private void OnDestroy()
    {
        if (particleMaterial != null) Destroy(particleMaterial);
        if (ringSprite != null) Destroy(ringSprite);
        if (whiteSprite != null) Destroy(whiteSprite);
        if (ringTexture != null) Destroy(ringTexture);
    }

    private void Build()
    {
        if (built) return;
        built = true;

        // 先按当前相机可视范围把雨场放大到覆盖整个屏幕（含边缘）。
        FitFieldToCamera();

        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        ringTexture = CreateRingTexture(32);
        ringSprite = Sprite.Create(ringTexture, new Rect(0f, 0f, ringTexture.width, ringTexture.height), new Vector2(0.5f, 0.5f), 32f);

        Shader spriteShader = Shader.Find("Sprites/Default");
        particleMaterial = new Material(spriteShader);
        particleMaterial.mainTexture = Texture2D.whiteTexture;

        backgroundRain = CreateStreakLayer("Rain_Background", backgroundStreakCount, new Vector2(5.6f, -22f), 0.018f, 0.45f, new Color(rainColor.r, rainColor.g, rainColor.b, rainColor.a * 0.75f), -12);
        foregroundRain = CreateStreakLayer("Rain_Foreground", foregroundStreakCount, new Vector2(8f, -32f), 0.028f, 0.65f, rainColor, 30);

        // 雨落在地面上：溅起的水花（涟漪在上面 OnEnable 里一起开）。
        if (groundSplash) splashRain = CreateSplashLayer();

        // 积水：地面水光 / 水洼，需求默认关闭。
        if (groundWater) CreateGroundWetness();

        CreateCameraOverlays();
    }

    /// <summary>
    /// 按相机可视范围放大雨场，保证任意宽高比下整个屏幕都有雨。
    /// 找不到相机时退回场景里配的 width / height。
    /// </summary>
    private void FitFieldToCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) camera = FindObjectOfType<Camera>();
        if (camera == null) return;

        // 优先用场景矩形（SceneCameraFollow2D 上的范围），它不受执行顺序影响；
        // 再和相机实际可视范围取较大值，保证宽屏时屏幕两边也有雨。
        float sceneHalfWidth = 0f;
        float sceneHalfHeight = 0f;
        Vector3 center = camera.transform.position;

        SceneCameraFollow2D framing = camera.GetComponent<SceneCameraFollow2D>();
        if (framing != null)
        {
            sceneHalfWidth = framing.SceneWidth * 0.5f;
            sceneHalfHeight = framing.SceneHeight * 0.5f;
            center = new Vector3(
                (framing.sceneMinX + framing.sceneMaxX) * 0.5f,
                (framing.sceneMinY + framing.sceneMaxY) * 0.5f,
                transform.position.z);
        }

        float cameraHalfHeight = camera.orthographic ? camera.orthographicSize : sceneHalfHeight;
        float cameraHalfWidth = camera.orthographic
            ? cameraHalfHeight * Mathf.Max(0.1f, camera.aspect)
            : sceneHalfWidth;

        float halfWidth = Mathf.Max(sceneHalfWidth, cameraHalfWidth);
        float halfHeight = Mathf.Max(sceneHalfHeight, cameraHalfHeight);

        transform.position = new Vector3(center.x, center.y, transform.position.z);
        width = halfWidth * 2f + coverMargin * 2f;
        height = halfHeight * 2f + coverMargin * 2f;

        // 屏幕越宽，雨丝数量同比例增加，保持密度一致。
        float widthScale = Mathf.Clamp(width / Mathf.Max(1f, BaseViewWidth), 1f, 3f);
        backgroundStreakCount = Mathf.Max(1, Mathf.RoundToInt(backgroundRate * widthScale));
        foregroundStreakCount = Mathf.Max(1, Mathf.RoundToInt(foregroundRate * widthScale));
    }

    private RainStreakField2D CreateStreakLayer(string name, int count, Vector2 velocity, float streakWidth, float streakLength, Color color, int sortingOrder)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(transform, false);

        RainStreakField2D field = layer.AddComponent<RainStreakField2D>();
        field.Configure(
            whiteSprite,
            Mathf.Max(1, Mathf.RoundToInt(count * Mathf.Max(0.05f, intensity))),
            width,
            height,
            groundY,
            velocity,
            streakWidth,
            streakLength,
            color,
            sortingOrder,
            0.04f);

        return field;
    }
    private ParticleSystem CreateRainLayer(string name, float rate, float speed, float thickness, float length, Color color, int sortingOrder)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(transform, false);

        ParticleSystem particles = layer.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.70f, 1.20f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(thickness * 0.75f, thickness * 1.25f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(length * 0.75f, length * 1.35f);
        main.startSizeZ = 1f;
        main.maxParticles = Mathf.Max(10, Mathf.RoundToInt(rate * 5f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.startColor = new ParticleSystem.MinMaxGradient(color * 0.70f, color * 1.15f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate * Mathf.Max(0f, intensity);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, height, 1f);
        shape.position = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windSpeed);
        velocity.y = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = false;
        noise.strength = new ParticleSystem.MinMaxCurve(0.004f, 0.010f);
        noise.frequency = 0.25f;
        noise.damping = true;

        ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
        renderer.material = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.045f;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = sortingOrder;

        particles.Play(true);
        return particles;
    }

    private ParticleSystem CreateSplashLayer()
    {
        GameObject layer = new GameObject("Rain_Splash");
        layer.transform.SetParent(transform, false);

        ParticleSystem particles = layer.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = 0.12f;
        main.startSizeY = 0.025f;
        main.startSizeZ = 1f;
        main.maxParticles = Mathf.Max(10, Mathf.RoundToInt(90f * Mathf.Max(0.1f, intensity)));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.startColor = splashColor;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 90f * Mathf.Max(0f, intensity);

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.12f, 1f);
        shape.position = new Vector3(0f, groundY - transform.position.y, 0f);

        ParticleSystemRenderer renderer = layer.GetComponent<ParticleSystemRenderer>();
        renderer.material = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.sortingOrder = 22;

        particles.Play(true);
        return particles;
    }

    private void CreateGroundWetness()
    {
        CreateRuntimeSpriteObject(
            "Wet_Ground_Sheen",
            new Vector3(0f, groundY + 0.12f, 0f),
            new Vector3(width, 0.55f, 1f),
            new Color(0.35f, 0.55f, 0.82f, 0.04f + intensity * 0.05f),
            -18);

        for (int i = 0; i < 7; i++)
        {
            float x = -width * 0.42f + i * (width * 0.14f) + Random.Range(-0.6f, 0.6f);
            float y = groundY + Random.Range(0.02f, 0.16f);
            float scaleX = Random.Range(1.2f, 2.8f);
            float scaleY = Random.Range(0.06f, 0.14f);
            CreateRuntimeSpriteObject(
                "Puddle_" + i,
                new Vector3(x, y, 0f),
                new Vector3(scaleX, scaleY, 1f),
                new Color(0.40f, 0.65f, 0.90f, 0.035f + intensity * 0.04f),
                -17);
        }
    }

    private void CreateCameraOverlays()
    {
        Camera camera = Camera.main;
        if (camera == null) camera = FindObjectOfType<Camera>();
        if (camera == null) return;

        float overlayHeight = camera.orthographicSize * 2f + 1f;
        float overlayWidth = overlayHeight * camera.aspect + 1f;

        tintOverlay = CreateOverlay(camera.transform, "Rain_Tint", new Color(tintColor.r, tintColor.g, tintColor.b, tintColor.a), 10, overlayWidth, overlayHeight);
        lightningOverlay = CreateOverlay(camera.transform, "Rain_Lightning", new Color(lightningColor.r, lightningColor.g, lightningColor.b, 0f), 100, overlayWidth, overlayHeight);
    }

    /// <summary>
    /// 拼接进另一个场景时调用：把雨场钉在自己场景的中心、按场景尺寸铺，
    /// 不再跟着相机跑（否则相机会把雨铺满整个拼接画面、糊到另一个场景上）。
    /// </summary>
    public void PinToScene(Vector3 sceneCenter, float sceneWidth, float sceneHeight)
    {
        transform.position = new Vector3(sceneCenter.x, sceneCenter.y, transform.position.z);

        width = sceneWidth;
        height = sceneHeight;

        if (backgroundRain != null)
        {
            backgroundRain.width = width;
            backgroundRain.height = height;
        }
        if (foregroundRain != null)
        {
            foregroundRain.width = width;
            foregroundRain.height = height;
        }
        if (splashRain != null)
        {
            ParticleSystem.ShapeModule shape = splashRain.shape;
            shape.scale = new Vector3(width, height, 1f);
        }

        // 这两个叠加层是"铺满相机视野"的，拼接时会盖住另一个场景，关掉。
        if (tintOverlay != null) tintOverlay.gameObject.SetActive(false);
        if (lightningOverlay != null) lightningOverlay.gameObject.SetActive(false);
    }

    /// <summary>
    /// NPC 行动拼接阶段：雨铺满整个拼接场景（宽度变宽、雨量按比例加），
    /// 但在一楼那半边的「盖子」以下不显示雨 —— 看起来就是雨落在盖子上。
    /// 雷 / 色调叠加层挂在对准整个拼接场景的相机下，所以会覆盖全场景。
    /// </summary>
    public void ApplyStitchedLayout(Vector3 worldCenter, float totalWidth, float totalHeight,
                                    float roofMinX, float roofMaxX, float roofTopY)
    {
        transform.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z);

        width = totalWidth;
        height = totalHeight;

        float widthScale = Mathf.Clamp(width / Mathf.Max(1f, BaseViewWidth), 1f, 3f);
        int backgroundCount = Mathf.Max(1, Mathf.RoundToInt(backgroundRate * widthScale * Mathf.Max(0.05f, intensity)));
        int foregroundCount = Mathf.Max(1, Mathf.RoundToInt(foregroundRate * widthScale * Mathf.Max(0.05f, intensity)));

        ApplyFieldLayout(backgroundRain, backgroundCount, roofMinX, roofMaxX, roofTopY);
        ApplyFieldLayout(foregroundRain, foregroundCount, roofMinX, roofMaxX, roofTopY);

        // 一楼半边在屋顶下，地面溅水 / 涟漪只留后院那半边。
        if (roofMaxX - roofMinX > 0.01f)
        {
            float worldMinX = worldCenter.x - totalWidth * 0.5f;
            float worldMaxX = worldCenter.x + totalWidth * 0.5f;
            float outdoorMinX = roofMinX <= worldMinX + 0.01f ? roofMaxX : worldMinX;
            float outdoorMaxX = roofMinX <= worldMinX + 0.01f ? worldMaxX : roofMinX;
            ApplySplashRange(outdoorMinX, outdoorMaxX);
        }
        else
        {
            ClearSplashRange();
        }

        ResizeOverlays(totalWidth + 1f, totalHeight + 1f);
    }

    /// <summary>拼接结束：雨场恢复成单场景（按相机取景），并取消屋盖排除。</summary>
    public void RestoreToCameraFit()
    {
        FitFieldToCamera();

        ApplyFieldLayout(backgroundRain, backgroundStreakCount, 0f, 0f, 0f);
        ApplyFieldLayout(foregroundRain, foregroundStreakCount, 0f, 0f, 0f);

        ClearSplashRange();
        ResizeOverlays(width + 1f, height + 1f);
    }

    /// <summary>把地面溅水的发射范围限制在世界坐标 [minX, maxX]（屋顶下不溅）。</summary>
    private void ApplySplashRange(float minX, float maxX)
    {
        splashRangeRestricted = true;
        splashMinX = Mathf.Min(minX, maxX);
        splashMaxX = Mathf.Max(minX, maxX);

        if (splashRain == null) return;

        ParticleSystem.ShapeModule shape = splashRain.shape;
        shape.scale = new Vector3(Mathf.Max(0.1f, splashMaxX - splashMinX), 0.12f, 1f);
        shape.position = new Vector3(
            (splashMinX + splashMaxX) * 0.5f - transform.position.x,
            groundY - transform.position.y,
            0f);
    }

    private void ClearSplashRange()
    {
        splashRangeRestricted = false;
        if (splashRain == null) return;

        ParticleSystem.ShapeModule shape = splashRain.shape;
        shape.scale = new Vector3(width, 0.12f, 1f);
        shape.position = new Vector3(0f, groundY - transform.position.y, 0f);
    }

    private void ApplyFieldLayout(RainStreakField2D field, int count, float roofMinX, float roofMaxX, float roofTopY)
    {
        if (field == null) return;

        field.width = width;
        field.height = height;
        field.SetStreakCount(count);

        if (roofMaxX > roofMinX) field.SetExclusion(roofMinX, roofMaxX, roofTopY);
        else field.ClearExclusion();
    }

    private void ResizeOverlays(float overlayWidth, float overlayHeight)
    {
        if (tintOverlay != null) tintOverlay.transform.localScale = new Vector3(overlayWidth, overlayHeight, 1f);
        if (lightningOverlay != null) lightningOverlay.transform.localScale = new Vector3(overlayWidth, overlayHeight, 1f);
    }

    private SpriteRenderer CreateOverlay(Transform parent, string name, Color color, int sortingOrder, float widthUnits, float heightUnits)
    {
        GameObject overlay = new GameObject(name);
        overlay.transform.SetParent(parent, false);
        overlay.transform.localPosition = new Vector3(0f, 0f, 1f);
        overlay.transform.localScale = new Vector3(widthUnits, heightUnits, 1f);

        SpriteRenderer renderer = overlay.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private GameObject CreateRuntimeSpriteObject(string name, Vector3 position, Vector3 scale, Color color, int sortingOrder)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(transform, false);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private IEnumerator RippleLoop()
    {
        while (true)
        {
            if (rippleEnabled && intensity > 0.01f)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(intensity * 2f), 1, 3);
                for (int i = 0; i < count; i++) SpawnRipple();
            }

            float interval = rippleInterval / Mathf.Max(0.25f, intensity);
            yield return new WaitForSeconds(Mathf.Max(0.04f, interval));
        }
    }

    private void SpawnRipple()
    {
        // 拼接时只落在露天那半边；单场景时以雨场中心为基准铺满整个宽度。
        float x = splashRangeRestricted
            ? Random.Range(splashMinX, splashMaxX)
            : transform.position.x + Random.Range(-width * 0.5f, width * 0.5f);

        GameObject rippleObject = new GameObject("Rain_Ripple");
        rippleObject.transform.SetParent(transform, false);
        rippleObject.transform.position = new Vector3(x, groundY + 0.06f, 0f);

        SpriteRenderer renderer = rippleObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ringSprite;
        renderer.sortingOrder = 24;

        RainRipple2D ripple = rippleObject.AddComponent<RainRipple2D>();
        Color rippleColor = splashColor; rippleColor.a *= 0.72f; ripple.Initialize(renderer, rippleColor, rippleLife, 0.02f, rippleSize * Random.Range(0.55f, 0.85f));
    }

    private IEnumerator LightningLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minLightningInterval, maxLightningInterval));
            if (!lightningEnabled || lightningOverlay == null) continue;

            yield return FadeLightning(0f, lightningColor.a, 0.05f);
            yield return new WaitForSeconds(0.04f);
            yield return FadeLightning(lightningColor.a, 0f, 0.22f);
        }
    }

    private IEnumerator FadeLightning(float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetLightningAlpha(Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetLightningAlpha(toAlpha);
    }

    private void SetLightningAlpha(float alpha)
    {
        if (lightningOverlay == null) return;
        Color color = lightningColor;
        color.a = alpha;
        lightningOverlay.color = color;
    }

    private Texture2D CreateRingTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.5f;
        float innerRadius = outerRadius * 0.72f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= outerRadius && distance >= innerRadius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        return texture;
    }
}

















