using UnityEngine;

[DisallowMultipleComponent]
public class RainStreakField2D : MonoBehaviour
{
    public Sprite streakSprite;
    public int streakCount = 120;
    public float width = 60f;
    public float height = 14f;
    public float groundY = -3.4f;
    public Vector2 velocity = new Vector2(6f, -22f);
    public float streakWidth = 0.018f;
    public float streakLength = 0.45f;
    public Color color = new Color(0.62f, 0.78f, 1f, 0.25f);
    public int sortingOrder = -12;
    public float rotationJitter = 0.04f;
    public float respawnPadding = 1.5f;

    [Header("地板（雨丝落到 groundY 以下就不显示，避免穿过地板）")]
    public bool blockBelowGround = true;

    [Header("排除区（这个矩形下方不显示雨，用来做「屋顶挡住雨」）")]
    public bool useExclusion;
    public float exclusionMinX;
    public float exclusionMaxX;
    public float exclusionTopY;

    public int StreakCount { get { return streakCount; } }

    /// <summary>少于这个 Y 且在 X 范围内 → 不显示雨（雨落在盖子上）。</summary>
    public void SetExclusion(float minX, float maxX, float topY)
    {
        useExclusion = true;
        exclusionMinX = Mathf.Min(minX, maxX);
        exclusionMaxX = Mathf.Max(minX, maxX);
        exclusionTopY = topY;
    }

    public void ClearExclusion()
    {
        useExclusion = false;
    }

    /// <summary>拼接或恢复时重设雨场边界，并让雨丝按新范围重新分布。</summary>
    public void SetBounds(float fieldWidth, float fieldHeight, float fieldGroundY, int count)
    {
        width = Mathf.Max(0.1f, fieldWidth);
        height = Mathf.Max(0.1f, fieldHeight);
        groundY = fieldGroundY;
        streakCount = Mathf.Max(1, count);
        Build();
    }

    private Transform[] streaks;
    private SpriteRenderer[] renderers;
    private bool configured;

    public void Configure(Sprite sprite, int count, float fieldWidth, float fieldHeight, float fieldGroundY, Vector2 fieldVelocity, float visualWidth, float visualLength, Color visualColor, int order, float jitter)
    {
        streakSprite = sprite;
        streakCount = Mathf.Max(1, count);
        width = fieldWidth;
        height = fieldHeight;
        groundY = fieldGroundY;
        velocity = fieldVelocity;
        streakWidth = visualWidth;
        streakLength = visualLength;
        color = visualColor;
        sortingOrder = order;
        rotationJitter = jitter;
        configured = true;
        Build();
    }

    private void OnEnable()
    {
        if (configured && (streaks == null || streaks.Length != streakCount)) Build();
    }

    private void Update()
    {
        if (streaks == null || streaks.Length == 0) return;

        Vector3 origin = transform.position;
        float halfWidth = width * 0.5f;

        // 雨丝落到地板线以下就回收（没有地板时用雨场底部兜底）。
        float recycleY = blockBelowGround
            ? groundY - respawnPadding
            : origin.y - height * 0.5f - respawnPadding;

        float dt = Time.deltaTime;
        for (int i = 0; i < streaks.Length; i++)
        {
            Transform streak = streaks[i];
            if (streak == null) continue;

            streak.position += (Vector3)(velocity * dt);

            // 地板线以下不显示（雨落在地上就停住，不穿过地板）；
            // 屋盖下的排除区也不显示（雨打在盖子上）。
            bool belowFloor = blockBelowGround && streak.position.y < groundY;
            bool underRoof = useExclusion &&
                             streak.position.x > exclusionMinX &&
                             streak.position.x < exclusionMaxX &&
                             streak.position.y < exclusionTopY;

            if (renderers != null && renderers[i] != null)
            {
                bool hidden = belowFloor || underRoof;
                if (renderers[i].enabled == hidden) renderers[i].enabled = !hidden;
            }

            if (streak.position.y < recycleY ||
                streak.position.x < origin.x - halfWidth - respawnPadding ||
                streak.position.x > origin.x + halfWidth + respawnPadding)
            {
                Respawn(i, true);
            }
        }
    }

    private void Build()
    {
        if (streakSprite == null) return;

        if (streaks != null)
        {
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] != null) Destroy(streaks[i].gameObject);
            }
        }

        streaks = new Transform[streakCount];
        renderers = new SpriteRenderer[streakCount];

        for (int i = 0; i < streakCount; i++)
        {
            GameObject streak = new GameObject("Rain_Streak_" + i);
            streak.transform.SetParent(transform, false);
            SpriteRenderer renderer = streak.AddComponent<SpriteRenderer>();
            renderer.sprite = streakSprite;
            renderer.sortingOrder = sortingOrder;
            streaks[i] = streak.transform;
            renderers[i] = renderer;
            Respawn(i, false);
        }
    }

    private void Respawn(int index, bool fromTop)
    {
        Transform streak = streaks[index];
        if (streak == null) return;

        Vector3 origin = transform.position;
        float x = origin.x + Random.Range(-width * 0.5f, width * 0.5f);
        float y = fromTop
            ? origin.y + height * 0.5f + Random.Range(0f, 2f)
            : origin.y + Random.Range(-height * 0.5f, height * 0.5f);

        streak.position = new Vector3(x, y, 0f);
        streak.localScale = new Vector3(streakWidth * Random.Range(0.85f, 1.15f), streakLength * Random.Range(0.75f, 1.25f), 1f);

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        angle += Random.Range(-rotationJitter, rotationJitter) * Mathf.Rad2Deg;
        streak.localRotation = Quaternion.Euler(0f, 0f, angle);

        Color streakColor = color;
        streakColor.a *= Random.Range(0.72f, 1.08f);
        renderers[index].color = streakColor;
        renderers[index].enabled = !(blockBelowGround && streak.position.y < groundY);
    }
}
