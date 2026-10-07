using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 切场景用的黑屏淡入淡出。挂在 GameRoot 上，不需要 Canvas。
/// </summary>
[DefaultExecutionOrder(900)]
public class SceneFadeOverlay : MonoBehaviour
{
    private void OnEnable()
    {
        // 场景加载完成后由常驻的 GameRoot 收尾：
        // 触发切场景的那个对象在加载时会被销毁，不能靠它的协程收尾。
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneTransitionState.EndLoad();

        // 真正换场景（Single）时清掉可能残留的拼接状态。
        if (mode == LoadSceneMode.Single) SceneStitcher.ResetStatics();
    }

    private void Update()
    {
        SceneTransitionState.TickFade(Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        float alpha = SceneTransitionState.FadeAlpha;
        if (alpha <= 0.001f) return;

        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
