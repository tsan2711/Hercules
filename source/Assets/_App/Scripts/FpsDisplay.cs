using UnityEngine;
using TMPro;

public class FpsDisplayTMP : MonoBehaviour
{
    public TextMeshProUGUI fpsText;
    public float updateInterval = 0.5f;

    float accum = 0f;
    int frames = 0;
    float timeLeft;

    void Start()
    {
        timeLeft = updateInterval;
        if (fpsText == null) Debug.LogWarning("FpsDisplayTMP: fpsText chưa được gán!");
    }

    void Update()
    {
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;
        if (timeLeft <= 0f)
        {
            float fps = frames / accum;
            fpsText.text = $"FPS: {fps:F1}";
            timeLeft = updateInterval;
            accum = 0f;
            frames = 0;
        }
    }
}
