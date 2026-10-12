using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void OnStartClicked()
    {
        SceneManager.LoadScene("FirstFloor");
    }

    public void OnCreditsClicked()
    {
        SceneManager.LoadScene("Credits");
    }

    public void OnSettingsClicked()
    {
        SceneManager.LoadScene("Settings");
    }

    public void OnQuitClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        Debug.Log("退出游戏");
    }
}