using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen only starts the existing Main scene; gameplay stays there.
public sealed class IntroMenu : MonoBehaviour
{
    public Button startButton;
    public Button quitButton;
    public Text statusText;
    public string mainSceneName = "01_Main";

    public bool IsStarting { get; private set; }

    void OnEnable()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    void OnDisable()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }

    public void StartGame()
    {
        if (IsStarting) return;
        if (string.IsNullOrWhiteSpace(mainSceneName) || !Application.CanStreamedLevelBeLoaded(mainSceneName))
        {
            if (statusText != null) statusText.text = "시작할 씬을 불러올 수 없습니다.";
            Debug.LogWarning("Intro: Main scene is missing from the build scene list.", this);
            return;
        }

        IsStarting = true;
        SetButtons(false);
        if (statusText != null) statusText.text = "쉘터로 이동 중…";
        StartCoroutine(LoadMain());
    }

    IEnumerator LoadMain()
    {
        // Show loading feedback before starting the scene load.
        yield return null;
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (operation != null)
        {
            yield return operation;
        }
        else
        {
            IsStarting = false;
            SetButtons(true);
            if (statusText != null) statusText.text = "시작하지 못했습니다. 다시 시도해 주세요.";
        }
    }

    void SetButtons(bool interactable)
    {
        if (startButton != null) startButton.interactable = interactable;
        if (quitButton != null) quitButton.interactable = interactable;
    }

    public void QuitGame()
    {
        if (IsStarting) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
