using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Game Settings")]
    public string sceneToLoad = "Main Scene"; // Tên scene để load khi bấm Start

    private bool isPaused = false;

    // Bấm nút Start
    public void OnClickStart()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    // Bấm nút Quit
    public void OnClickQuit()
    {
        Application.Quit();
    }

    // Bấm nút Pause
    public void OnClickPause()
    {
        isPaused = true;
        Time.timeScale = 0f; // Dừng game
    }

    // Bấm nút Resume
    public void OnClickResume()
    {
        isPaused = false;
        Time.timeScale = 1f; // Tiếp tục game
    }
}
