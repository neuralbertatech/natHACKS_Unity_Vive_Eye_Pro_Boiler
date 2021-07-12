// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("ParticleScene");
    }

    public void QuitGame()
    {
        Debug.Log("QUIT THE GAME");
        Application.Quit();
    }
}

