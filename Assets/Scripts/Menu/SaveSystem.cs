using System;
using UnityEngine;

// Сохранение прогресса по главам.
// Сохраняется номер главы. При загрузке игра продолжается с начала этой главы:
// SaveLoader восстанавливает состояние (ChapterCheckpoint → On Restore) и ставит игрока в точку главы.
public static class SaveSystem
{
    const string KeyChapter = "save_chapter";

    // Глава, до которой игрок дошёл в текущем запуске
    public static int CurrentChapter = 1;

    // true — сцена игры загружается из главного меню кнопкой «Продолжить»
    public static bool PendingLoad;

    public static event Action<int> Saved;

    public static bool HasSave
    {
        get { return PlayerPrefs.HasKey(KeyChapter); }
    }

    public static int SavedChapter
    {
        get { return PlayerPrefs.GetInt(KeyChapter, 1); }
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(KeyChapter, CurrentChapter);
        PlayerPrefs.Save();
        if (Saved != null) Saved(CurrentChapter);
    }

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(KeyChapter);
        PlayerPrefs.Save();
    }

    public static void PrepareNewGame()
    {
        PendingLoad = false;
        CurrentChapter = 1;
    }

    public static void PrepareContinue()
    {
        PendingLoad = HasSave;
        CurrentChapter = SavedChapter;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        CurrentChapter = 1;
        PendingLoad = false;
        Saved = null;
    }
}
