using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Экран настроек. Один и тот же скрипт работает и в главном меню, и в меню паузы.
// Скрипт висит на объекте Settings (всегда включён), сам экран — дочерний SettingsPanel.
public class SettingsMenu : MonoBehaviour
{
    public GameObject panel;

    [Header("Управление")]
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValue;
    public Slider fovSlider;
    public TMP_Text fovValue;

    [Header("Изображение")]
    public Slider brightnessSlider;
    public TMP_Text brightnessValue;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;

    [Header("Звук")]
    public Slider volumeSlider;
    public TMP_Text volumeValue;

    [Header("Кнопки")]
    public Button resetButton;
    public Button backButton;

    // Сообщает меню, что настройки закрыли (меню снова показывает свои кнопки)
    public event Action Closed;

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    public bool IsOpen
    {
        get { return panel != null && panel.activeSelf; }
    }

    void Awake()
    {
        SetupSlider(sensitivitySlider, 0.2f, 3f, false);
        SetupSlider(fovSlider, 50f, 90f, true);
        SetupSlider(brightnessSlider, 0.5f, 1.5f, false);
        SetupSlider(volumeSlider, 0f, 1f, false);

        sensitivitySlider.onValueChanged.AddListener(v => { GameSettings.Sensitivity = v; UpdateLabels(); });
        fovSlider.onValueChanged.AddListener(v => { GameSettings.Fov = v; UpdateLabels(); });
        brightnessSlider.onValueChanged.AddListener(v => { GameSettings.Brightness = v; UpdateLabels(); });
        volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; UpdateLabels(); });


        // Разрешения экрана без повторов (Unity отдаёт одно разрешение с разной частотой несколько раз)
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }
        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        List<string> names = new List<string>();
        foreach (Vector2Int size in resolutions) names.Add(size.x + " × " + size.y);
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(names);
        resolutionDropdown.onValueChanged.AddListener(i => GameSettings.SetResolution(resolutions[i].x, resolutions[i].y));

        fullscreenToggle.onValueChanged.AddListener(on => GameSettings.Fullscreen = on);
        vsyncToggle.onValueChanged.AddListener(on => GameSettings.VSync = on);

        resetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); Refresh(); });
        backButton.onClick.AddListener(Close);

        panel.SetActive(false);
    }

    static void SetupSlider(Slider slider, float min, float max, bool whole)
    {
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
    }

    public void Open()
    {
        Refresh();
        panel.SetActive(true);
    }

    public void Close()
    {
        GameSettings.Save();
        panel.SetActive(false);
        if (Closed != null) Closed();
    }

    // Показать в элементах текущие значения (без вызова обработчиков)
    void Refresh()
    {
        sensitivitySlider.SetValueWithoutNotify(GameSettings.Sensitivity);
        fovSlider.SetValueWithoutNotify(GameSettings.Fov);
        brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);

        

        Vector2Int current = GameSettings.HasSavedResolution
            ? GameSettings.SavedResolution
            : new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        int index = resolutions.IndexOf(current);
        if (index < 0) index = resolutions.Count - 1;
        resolutionDropdown.SetValueWithoutNotify(index);
        resolutionDropdown.RefreshShownValue();

        fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        vsyncToggle.SetIsOnWithoutNotify(GameSettings.VSync);

        UpdateLabels();
    }

    void UpdateLabels()
    {
        if (sensitivityValue != null) sensitivityValue.text = GameSettings.Sensitivity.ToString("0.0");
        if (fovValue != null) fovValue.text = Mathf.RoundToInt(GameSettings.Fov) + "°";
        if (brightnessValue != null) brightnessValue.text = Mathf.RoundToInt(GameSettings.Brightness * 100f) + "%";
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(GameSettings.Volume * 100f) + "%";
    }
}
