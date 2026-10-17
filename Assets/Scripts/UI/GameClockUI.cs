using UnityEngine;

/// <summary>
/// 暂停键上的「游戏时钟」：一个圆盘 + 分针 + 秒针。
/// 两根针一起表示"从本局游戏开始到现在过了多久"。
/// 只累计「正在游戏里且未暂停」的时间。
/// </summary>
public class GameClockUI : MonoBehaviour
{
    [Header("两根指针")]
    public RectTransform minuteHand;
    public RectTransform secondHand;

    [Header("时间倍率：1 = 真实时间；2 = 快一倍")]
    [Range(0.1f, 60f)]
    public float timeScale = 1f;

    private float elapsedSeconds;

    /// <summary>由 PauseButtonHUD 每帧告知：现在算不算在计时。</summary>
    public void Tick(bool counting, float deltaSeconds)
    {
        if (counting) elapsedSeconds += deltaSeconds * timeScale;
        Apply();
    }

    private void Apply()
    {
        // Unity 里 Z 旋转正值是「逆时针」——钟表要顺时针，所以取负
        float seconds = elapsedSeconds % 60f;
        float minutes = elapsedSeconds % 3600f;

        if (secondHand != null)
            secondHand.localRotation = Quaternion.Euler(0f, 0f, -(seconds / 60f) * 360f);

        if (minuteHand != null)
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, -(minutes / 3600f) * 360f);
    }
}
