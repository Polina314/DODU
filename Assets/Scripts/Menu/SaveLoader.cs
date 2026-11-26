using System.Collections;
using UnityEngine;

// Висит в сцене игры. Если игру открыли кнопкой «Продолжить»,
// восстанавливает состояние главы и переносит игрока в её начало.
public class SaveLoader : MonoBehaviour
{
    [Tooltip("Объект Player")]
    public Transform player;

    [Tooltip("Что выключить при загрузке ещё до старта (например, Intro — пробуждение за столом из главы 1)")]
    public GameObject[] disableOnLoad;

    [Tooltip("Canvas Group затемнения (объект Fader). После загрузки экран делается прозрачным")]
    public CanvasGroup screenFader;

    bool loading;

    void Awake()
    {
        if (!SaveSystem.PendingLoad) return;
        loading = true;
        // Awake выполняется раньше всех Start: выключенные здесь объекты свой Start уже не запустят
        if (disableOnLoad != null)
            foreach (GameObject go in disableOnLoad)
                if (go != null) go.SetActive(false);
    }

    IEnumerator Start()
    {
        if (!loading) yield break;
        SaveSystem.PendingLoad = false;

        yield return null; // ждём кадр, чтобы все остальные скрипты успели выполнить свой Start

        int target = SaveSystem.SavedChapter;
        ChapterCheckpoint[] all = FindObjectsOfType<ChapterCheckpoint>(true);
        System.Array.Sort(all, (a, b) => a.chapter.CompareTo(b.chapter));

        ChapterCheckpoint targetCheckpoint = null;
        foreach (ChapterCheckpoint cp in all)
        {
            if (cp.chapter > target) continue;
            cp.onRestore.Invoke();
            if (cp.chapter == target) targetCheckpoint = cp;
        }

        SaveSystem.CurrentChapter = target;

        if (targetCheckpoint != null && targetCheckpoint.spawnPoint != null && player != null)
            Teleport(targetCheckpoint.spawnPoint);
        else if (target > 1)
            Debug.LogWarning("SaveLoader: не нашла ChapterCheckpoint с главой " + target + " или у него нет Spawn Point");

        if (screenFader != null)
        {
            screenFader.alpha = 0f;
            screenFader.blocksRaycasts = false;
        }
    }

    void Teleport(Transform to)
    {
        // CharacterController не даёт двигать игрока напрямую — на время выключаем
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.SetPositionAndRotation(to.position, Quaternion.Euler(0f, to.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;
    }
}
