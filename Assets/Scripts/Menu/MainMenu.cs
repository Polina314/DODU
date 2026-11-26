using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// Главное меню: Продолжить, Новая игра, Настройки, Авторы, Выход.
public class MainMenu : MonoBehaviour
{
    [Tooltip("Имя сцены с игрой (как в Build Settings)")]
    public string gameSceneName = "Cacatonia";

    [Header("Экраны")]
    public GameObject mainPanel;
    public GameObject creditsPanel;
    public GameObject loadingScreen;

    [Header("Кнопки")]
    public Button continueButton;
    public Button newGameButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;
    public Button creditsBackButton;

    [Header("Окна")]
    public SettingsMenu settings;
    public ConfirmDialog confirm;

    [Header("Атмосфера")]
    public RawImage videoDisplay;
    public AudioSource backgroundAudioSource;
    private VideoPlayer videoPlayer;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        continueButton.onClick.AddListener(ContinueGame);
        newGameButton.onClick.AddListener(NewGame);
        settingsButton.onClick.AddListener(OpenSettings);
        creditsButton.onClick.AddListener(OpenCredits);
        quitButton.onClick.AddListener(AskQuit);
        creditsBackButton.onClick.AddListener(CloseCredits);
        settings.Closed += OnSettingsClosed;

        mainPanel.SetActive(true);
        creditsPanel.SetActive(false);
        if (loadingScreen != null) loadingScreen.SetActive(false);

        UpdateContinueButton();
        SetupAtmosphere();
    }

    void OnDestroy()
    {
        if (settings != null) settings.Closed -= OnSettingsClosed;
        if (videoPlayer != null) videoPlayer.Stop();
        if (backgroundAudioSource != null) backgroundAudioSource.Stop();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (confirm.IsOpen) confirm.Hide();
        else if (settings.IsOpen) settings.Close();
        else if (creditsPanel.activeSelf) CloseCredits();
    }

    void SetupAtmosphere()
    {
        // --- ВИДЕО ---
        VideoClip clip = Resources.Load<VideoClip>("Videos/MainMenuClip");

        if (clip != null && videoDisplay != null)
        {
            if (videoPlayer == null)
                videoPlayer = gameObject.AddComponent<VideoPlayer>();

            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = clip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            int w = Mathf.Min(Screen.width, 1920);
            int h = Mathf.Min(Screen.height, 1080);
            videoPlayer.targetTexture = new RenderTexture(w, h, 0);

            videoDisplay.texture = videoPlayer.targetTexture;
            videoDisplay.gameObject.SetActive(true);

            videoPlayer.isLooping = true;
            videoPlayer.Play();

            Debug.Log("[MainMenu] Видео запущено.");
        }
        else
        {
            if (clip == null)
                Debug.LogError("[MainMenu] ВИДЕО НЕ НАЙДЕНО! Путь должен быть: Assets/Resources/Videos/MainMenuClip.mp4");
            if (videoDisplay == null)
                Debug.LogError("[MainMenu] videoDisplay = null! MenuBuilder не присвоил ссылку.");
        }

        // --- МУЗЫКА ---
        AudioClip track = Resources.Load<AudioClip>("Audio/MainMenuMusic");

        if (track != null && backgroundAudioSource != null)
        {
            backgroundAudioSource.clip = track;
            backgroundAudioSource.Play();
            Debug.Log("[MainMenu] Музыка запущена.");
        }
        else
        {
            if (track == null)
                Debug.LogError("[MainMenu] МУЗЫКА НЕ НАЙДЕНА! Путь должен быть: Assets/Resources/Audio/MainMenuMusic.wav");
            if (backgroundAudioSource == null)
                Debug.LogError("[MainMenu] backgroundAudioSource = null! MenuBuilder не присвоил ссылку.");
        }
    }

    void UpdateContinueButton()
    {
        bool has = SaveSystem.HasSave;
        continueButton.interactable = has;
        TMP_Text label = continueButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = has ? "Продолжить (глава " + SaveSystem.SavedChapter + ")" : "Продолжить";
            label.alpha = has ? 1f : 0.35f;
        }
    }

    void ContinueGame()
    {
        SaveSystem.PrepareContinue();
        LoadGame();
    }

    void NewGame()
    {
        if (SaveSystem.HasSave)
        {
            confirm.Show("Начать новую игру?\nСохранённый прогресс будет удалён.", () =>
            {
                SaveSystem.DeleteSave();
                StartNewGame();
            });
        }
        else
        {
            StartNewGame();
        }
    }

    void StartNewGame()
    {
        SaveSystem.PrepareNewGame();
        LoadGame();
    }

    void LoadGame()
    {
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        mainPanel.SetActive(false);
        if (loadingScreen != null) loadingScreen.SetActive(true);
        yield return null;
        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        while (!op.isDone) yield return null;
    }

    void OpenSettings()
    {
        mainPanel.SetActive(false);
        settings.Open();
    }

    void OnSettingsClosed()
    {
        mainPanel.SetActive(true);
    }

    void OpenCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    void CloseCredits()
    {
        creditsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    void AskQuit()
    {
        confirm.Show("Выйти из игры?", QuitGame);
    }

    public static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}