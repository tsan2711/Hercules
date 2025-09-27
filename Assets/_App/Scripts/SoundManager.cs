using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Clips")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Volumes")]
    [Range(0f, 1f)] public float hoverVolume = 0.3f;
    [Range(0f, 1f)] public float clickVolume = 0.1f;

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayHover()
    {
        if (hoverSound != null)
            audioSource.PlayOneShot(hoverSound, hoverVolume);
    }

    public void PlayClick()
    {
        if (clickSound != null)
            audioSource.PlayOneShot(clickSound, clickVolume);
    }
}
