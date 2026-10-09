using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public class ScenePortal2D : MonoBehaviour
{
    [Header("连接")]
    public Transform player;
    public string sceneName;
    public float radius = 1.45f;
    public bool enabledPortal = true;

    [Header("入口身份")]
    public string portalId;
    public string arrivalPortalId;

    [Header("到达位置")]
    public float arrivalOffset = 2.6f;
    public float arrivalDirection = 1f;

    [Header("防回弹")]
    public float reactivateBuffer = 0.9f;
    public float minMoveSpeed = 0.05f;

    private bool waitingToRearm = true;
    private Vector3 previousPlayerPosition;
    private bool hasPreviousPlayerPosition;

    private void Start()
    {
        enabledPortal = false;
        waitingToRearm = true;

        if (SceneTransitionState.ConsumeArrival(portalId) && player != null)
        {
            Vector3 position = transform.position;
            position.x += arrivalOffset * arrivalDirection;
            player.position = position;
        }

        if (player != null)
        {
            previousPlayerPosition = player.position;
            hasPreviousPlayerPosition = true;
        }
    }

    private void Update()
    {
        if (player == null || string.IsNullOrWhiteSpace(sceneName)) return;

        if (!hasPreviousPlayerPosition)
        {
            previousPlayerPosition = player.position;
            hasPreviousPlayerPosition = true;
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float velocityX = (player.position.x - previousPlayerPosition.x) / deltaTime;
        previousPlayerPosition = player.position;

        Vector2 delta = player.position - transform.position;

        if (waitingToRearm)
        {
            float safeDistance = radius + Mathf.Max(0.1f, reactivateBuffer);
            if (delta.sqrMagnitude > safeDistance * safeDistance)
            {
                waitingToRearm = false;
                enabledPortal = true;
            }
            return;
        }

        if (!enabledPortal) return;
        if (delta.sqrMagnitude > radius * radius) return;

        Vector2 toPortal = (Vector2)transform.position - (Vector2)player.position;
        bool movingTowardPortal = Mathf.Abs(velocityX) >= minMoveSpeed && velocityX * toPortal.x > 0f;
        if (!movingTowardPortal) return;

        enabledPortal = false;
        waitingToRearm = true;
        SceneTransitionState.SetArrival(arrivalPortalId);
        SceneManager.LoadScene(sceneName);
    }
}

public static class SceneTransitionState
{
    public static string ArrivalPortalId { get; private set; }
    public static bool HasArrival { get; private set; }

    public static void SetArrival(string portalId)
    {
        ArrivalPortalId = portalId;
        HasArrival = !string.IsNullOrWhiteSpace(portalId);
    }

    public static bool ConsumeArrival(string portalId)
    {
        if (!HasArrival || string.IsNullOrWhiteSpace(portalId) || ArrivalPortalId != portalId) return false;
        HasArrival = false;
        ArrivalPortalId = null;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ArrivalPortalId = null;
        HasArrival = false;
    }
}
