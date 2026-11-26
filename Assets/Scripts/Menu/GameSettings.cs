using System;
using UnityEngine;

// Хранит настройки игры (PlayerPrefs) и применяет общие из них:
// громкость, качество графики, разрешение, полный экран, вертикальную синхронизацию.
// Чувствительность, поле зрения и яркость применяют SettingsApplier и BrightnessApplier.
public static class GameSettings
{
    const string KeySensitivity = "set_sensitivity";
    const string KeyFov = "set_fov";
    const string KeyBrightness = "set_brightness";
    const string KeyVolume = "set_volume";
    const string KeyQuality = "set_quality";
    const string KeyResWidth = "set_res_w";
    const string KeyResHeight = "set_res_h";
    const string KeyFullscreen = "set_fullscreen";
    const string KeyVSync = "set_vsync";

    public const float DefaultSensitivity = 1f;   // множитель: 1 = как настроено в FirstPersonController
    public const float DefaultFov = 60f;
    public const float DefaultBrightness = 1f;
    public const float DefaultVolume = 1f;

    // Вызывается при любом изменении настройки: скрипты-«применители» сразу обновляются
    public static event Action Changed;

    public static float Sensitivity
    {
        get { return PlayerPrefs.GetFloat(KeySensitivity, DefaultSensitivity); }
        set { PlayerPrefs.SetFloat(KeySensitivity, value); Notify(); }
    }

    public static float Fov
    {
        get { return PlayerPrefs.GetFloat(KeyFov, DefaultFov); }
        set { PlayerPrefs.SetFloat(KeyFov, value); Notify(); }
    }

    public static float Brightness
    {
        get { return PlayerPrefs.GetFloat(KeyBrightness, DefaultBrightness); }
        set { PlayerPrefs.SetFloat(KeyBrightness, value); Notify(); }
    }

    public static float Volume
    {
        get { return PlayerPrefs.GetFloat(KeyVolume, DefaultVolume); }
        set
        {
            PlayerPrefs.SetFloat(KeyVolume, value);
            AudioListener.volume = value;
            Notify();
        }
    }

    public static int Quality
    {
        get { return PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()); }
        set
        {
            PlayerPrefs.SetInt(KeyQuality, value);
            QualitySettings.SetQualityLevel(value, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0; // уровень качества сбрасывает V-Sync, возвращаем
            Notify();
        }
    }

    public static bool Fullscreen
    {
        get { return PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
            Screen.fullScreen = value;
            Notify();
        }
    }

    public static bool VSync
    {
        get { return PlayerPrefs.GetInt(KeyVSync, 1) == 1; }
        set
        {
            PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0);
            QualitySettings.vSyncCount = value ? 1 : 0;
            Notify();
        }
    }

    public static bool HasSavedResolution
    {
        get { return PlayerPrefs.HasKey(KeyResWidth) && PlayerPrefs.HasKey(KeyResHeight); }
    }

    public static Vector2Int SavedResolution
    {
        get { return new Vector2Int(PlayerPrefs.GetInt(KeyResWidth, Screen.width), PlayerPrefs.GetInt(KeyResHeight, Screen.height)); }
    }

    public static void SetResolution(int width, int height)
    {
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        Screen.SetResolution(width, height, Fullscreen);
        Notify();
    }

    // Применить всё сохранённое (вызывается автоматически при запуске игры)
    public static void ApplyAll()
    {
        AudioListener.volume = Volume;
        if (PlayerPrefs.HasKey(KeyQuality))
            QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        if (HasSavedResolution)
        {
            Vector2Int r = SavedResolution;
            Screen.SetResolution(r.x, r.y, Fullscreen);
        }
        else if (PlayerPrefs.HasKey(KeyFullscreen))
        {
            Screen.fullScreen = Fullscreen;
        }
    }

    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(KeySensitivity);
        PlayerPrefs.DeleteKey(KeyFov);
        PlayerPrefs.DeleteKey(KeyBrightness);
        PlayerPrefs.DeleteKey(KeyVolume);
        PlayerPrefs.DeleteKey(KeyVSync);
        // Качество, разрешение и полный экран не сбрасываем: они зависят от компьютера
        ApplyAll();
        PlayerPrefs.Save();
        Notify();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    static void Notify()
    {
        if (Changed != null) Changed();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnStartup()
    {
        Changed = null; // на случай, если в редакторе отключена перезагрузка домена
        ApplyAll();
    }
}
