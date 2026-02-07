using UnityEngine;
using UnityEngine.SceneManagement; 

public class ScenesManager : MonoBehaviour
{
    public void GoToGame()
    {
        SceneManager.LoadSceneAsync(1);
    }

    public void GoToMenu()
    {
        SceneManager.LoadSceneAsync(0);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}