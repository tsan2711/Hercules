using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Game Settings")]
    public string sceneToLoad = "Main Scene"; // Tên scene để load khi bấm Start

    [SerializeField] private Button pauseButton;
    [SerializeField] private Button resumeButton;
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
        Time.timeScale = 0f; 
        pauseButton.gameObject.SetActive(false);
        resumeButton.gameObject.SetActive(true);
    }

    // Bấm nút Resume
    public void OnClickResume()
    {
        isPaused = false;
        Time.timeScale = 1f; 
        pauseButton.gameObject.SetActive(true);
        resumeButton.gameObject.SetActive(false);
    }

}
