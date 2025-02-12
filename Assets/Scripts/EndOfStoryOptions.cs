using UnityEngine;
using UnityEngine.SceneManagement;
public class EndOfStoryOptions : MonoBehaviour
{
    public void ReloadGame() {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame() {
        Application.Quit();
    }

}
