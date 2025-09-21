using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    // Tên scene muốn load khi click Start
    public string sceneToLoad = "GameScene";

    public void OnClickStart()
    {
        SceneManager.LoadScene(sceneToLoad);
    }

    public void OnClickQuit()
    {
        Application.Quit();
    }
}
