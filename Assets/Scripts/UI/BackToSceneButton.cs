using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 通用「返回」按钮。挂到任意界面的返回按钮上，OnClick 调 OnBackClicked()。
/// 目标场景在 Inspector 里填，默认回主菜单。
/// </summary>
public class BackToSceneButton : MonoBehaviour
{
    [Tooltip("点了返回后要去的场景名，必须在 Build Settings 里。")]
    public string sceneName = "MainMenu";

    public void OnBackClicked()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[返回按钮] sceneName 没填。");
            return;
        }

        // LoadScene 传错场景名是「静默失败」，这里主动报出来，省得查半天
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[返回按钮] 场景没进 Build Settings：" + sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}