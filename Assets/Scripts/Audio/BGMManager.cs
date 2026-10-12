using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [Header("BGM ≤•∑≈∆˜")]
    public AudioSource bgmSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("BGMManager Awake");
    }

    // ==== ∂‘Õ‚Ω”ø⁄ ====

    // ≤•∑≈±≥æ∞“Ù¿÷
    public void PlayBGM()
    {
        if (bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.Play();
        }
    }

    // ‘›Õ£±≥æ∞“Ù¿÷
    public void PauseBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    // ºÃ–¯±≥æ∞“Ù¿÷
    public void ResumeBGM()
    {
        if (bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.UnPause();
        }
    }

    // Õ£÷π±≥æ∞“Ù¿÷
    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }
}