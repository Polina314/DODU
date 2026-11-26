using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Меню паузы по Esc: Продолжить, Сохранить прогресс, Настройки, Главное меню, Выйти из игры.
public class PauseMenu : MonoBehaviour
{
    [Tooltip("Имя сцены главного меню (как в Build Settings)")]
    public string mainMenuSceneName = "MainMenu";

    public GameObject pausePanel;

    [Header("Кнопки")]
    public Button resumeButton;
    public Button saveButton;
    public Button settingsButton;
    public Button mainMenuButton;
    public Button quitButton;
    public TMP_Text statusText;

    [Header("Окна")]
    public SettingsMenu settings;
    public ConfirmDialog confirm;

    [Header("Что выключать на паузе")]
    [Tooltip("FirstPersonController, PlayerInteraction, Flashlight — чтобы камера не крутилась и E/F не срабатывали")]
    public MonoBehaviour[] disableWhilePaused;

    [Tooltip("Пока любой из этих объектов включён (например, NotePanel), Esc не открывает паузу")]
    public GameObject[] blockers;

    public static bool IsPaused { get; private set; }

    bool[] wasEnabled;
    bool blockedLastFrame;

    void Start()
    {
        IsPaused = false;
        resumeButton.onClick.AddListener(Resume);
        saveButton.onClick.AddListener(SaveProgress);
        settingsButton.onClick.AddListener(OpenSettings);
        mainMenuButton.onClick.AddListener(AskMainMenu);
        quitButton.onClick.AddListener(AskQuit);
        settings.Closed += OnSettingsClosed;
        pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (settings != null) settings.Closed -= OnSettingsClosed;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (!IsPaused)
        {
            // Esc закрывает записку — в этот же кадр паузу не открываем
            if (AnyBlockerActive() || blockedLastFrame) return;
            Pause();
            return;
        }

        if (confirm.IsOpen) confirm.Hide();
        else if (settings.IsOpen) settings.Close();
        else Resume();
    }

    void LateUpdate()
    {
        blockedLastFrame = AnyBlockerActive();
    }

    bool AnyBlockerActive()
    {
        if (blockers == null) return false;
        foreach (GameObject b in blockers)
            if (b != null && b.activeInHierarchy) return true;
        return false;
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;          // останавливает движение, таймеры и физику
        AudioListener.pause = true;   // останавливает звуки

        wasEnabled = new bool[disableWhilePaused.Length];
        for (int i = 0; i < disableWhilePaused.Length; i++)
        {
            MonoBehaviour c = disableWhilePaused[i];
            if (c == null) continue;
            wasEnabled[i] = c.enabled;
            c.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (statusText != null) statusText.text = "";
        pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (settings.IsOpen) settings.panel.SetActive(false);
        confirm.Hide();
        pausePanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsPaused = false;

        // Скрипты игрока включаем в следующем кадре,
        // чтобы они не «поймали» тот же нажатый Esc
        StartCoroutine(ReenableNextFrame());
    }

    IEnumerator ReenableNextFrame()
    {
        yield return null;
        if (IsPaused || wasEnabled == null) yield break;
        for (int i = 0; i < disableWhilePaused.Length; i++)
            if (disableWhilePaused[i] != null) disableWhilePaused[i].enabled = wasEnabled[i];
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void SaveProgress()
    {
        SaveSystem.Save();
        if (statusText != null)
            statusText.text = "Прогресс сохранён: глава " + SaveSystem.CurrentChapter +
                              ".\nПри загрузке игра продолжится с начала этой главы.";
    }

    void OpenSettings()
    {
        pausePanel.SetActive(false);
        settings.Open();
    }

    void OnSettingsClosed()
    {
        if (IsPaused) pausePanel.SetActive(true);
    }

    void AskMainMenu()
    {
        confirm.Show("Выйти в главное меню?\nПрогресс внутри текущей главы не сохраняется.", () =>
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
            SceneManager.LoadScene(mainMenuSceneName);
        });
    }

    void AskQuit()
    {
        confirm.Show("Выйти из игры?", MainMenu.QuitGame);
    }
}
