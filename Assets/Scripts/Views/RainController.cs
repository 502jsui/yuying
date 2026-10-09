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
        if (rippleRoutine == null && rippleEnabled) rippleRoutine = StartCoroutine(RippleLoop());
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

        whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        ringTexture = CreateRingTexture(32);
        ringSprite = Sprite.Create(ringTexture, new Rect(0f, 0f, ringTexture.width, ringTexture.height), new Vector2(0.5f, 0.5f), 32f);

        Shader spriteShader = Shader.Find("Sprites/Default");
        particleMaterial = new Material(spriteShader);
        particleMaterial.mainTexture = Texture2D.whiteTexture;

        backgroundRain = CreateStreakLayer("Rain_Background", 200, new Vector2(5.6f, -22f), 0.018f, 0.45f, new Color(rainColor.r, rainColor.g, rainColor.b, rainColor.a * 0.75f), -12);
        foregroundRain = CreateStreakLayer("Rain_Foreground", 110, new Vector2(8f, -32f), 0.028f, 0.65f, rainColor, 30);
        splashRain = CreateSplashLayer();

        CreateGroundWetness();
        CreateCameraOverlays();
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
        shape.position = new Vector3(0f, groundY, 0f);

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
        float x = Random.Range(-width * 0.5f, width * 0.5f);
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

















