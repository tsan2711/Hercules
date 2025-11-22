using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Clips")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    public AudioClip backgroundMusic; // Background music clip

    [Header("Default Volumes")]
    [Range(0f, 1f)] public float hoverVolume = 0.3f;
    [Range(0f, 1f)] public float clickVolume = 0.1f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;
    [Range(0f, 1f)] public float musicVolume = 1.0f;
    [Range(0f, 1f)] public float masterVolume = 1.0f;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource musicAudioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadVolumeSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Khởi tạo AudioSources nếu chưa có
    /// </summary>
    private void InitializeAudioSources()
    {
        // SFX AudioSource
        if (sfxAudioSource == null)
        {
            GameObject sfxObj = new GameObject("SFX AudioSource");
            sfxObj.transform.SetParent(transform);
            sfxAudioSource = sfxObj.AddComponent<AudioSource>();
        }

        // Music AudioSource
        if (musicAudioSource == null)
        {
            GameObject musicObj = new GameObject("Music AudioSource");
            musicObj.transform.SetParent(transform);
            musicAudioSource = musicObj.AddComponent<AudioSource>();
            musicAudioSource.loop = true; // Music thường loop
        }

        // Load và play background music nếu có
        if (backgroundMusic != null && musicAudioSource.clip != backgroundMusic)
        {
            musicAudioSource.clip = backgroundMusic;
            musicAudioSource.Play();
        }
    }

    /// <summary>
    /// Load volume settings từ PlayerPrefs
    /// </summary>
    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1.0f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f);

        ApplyVolumes();
    }

    /// <summary>
    /// Áp dụng volume settings vào AudioSources
    /// </summary>
    private void ApplyVolumes()
    {
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfxVolume * masterVolume;
        }

        if (musicAudioSource != null)
        {
            musicAudioSource.volume = musicVolume * masterVolume;
        }
    }

    /// <summary>
    /// Set Master Volume
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Set Music Volume
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Set SFX Volume
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyVolumes();
    }

    /// <summary>
    /// Get Master Volume
    /// </summary>
    public float GetMasterVolume()
    {
        return masterVolume;
    }

    /// <summary>
    /// Get Music Volume
    /// </summary>
    public float GetMusicVolume()
    {
        return musicVolume;
    }

    /// <summary>
    /// Get SFX Volume
    /// </summary>
    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    /// <summary>
    /// Play hover sound
    /// </summary>
    public void PlayHover()
    {
        if (hoverSound != null && sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(hoverSound, hoverVolume * sfxVolume * masterVolume);
    }

    /// <summary>
    /// Play click sound
    /// </summary>
    public void PlayClick()
    {
        if (clickSound != null && sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(clickSound, clickVolume * sfxVolume * masterVolume);
    }

    /// <summary>
    /// Play SFX sound
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(clip, sfxVolume * masterVolume);
    }

    /// <summary>
    /// Play SFX sound với custom volume
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip != null && sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(clip, volume * sfxVolume * masterVolume);
    }

    /// <summary>
    /// Play background music
    /// </summary>
    public void PlayBackgroundMusic(AudioClip musicClip)
    {
        if (musicAudioSource != null && musicClip != null)
        {
            musicAudioSource.clip = musicClip;
            musicAudioSource.Play();
        }
    }

    /// <summary>
    /// Stop background music
    /// </summary>
    public void StopBackgroundMusic()
    {
        if (musicAudioSource != null)
        {
            musicAudioSource.Stop();
        }
    }
}
