using UnityEngine;
using UnityEngine.Events;

// Точка сохранения в начале главы.
// Reach() вызывается из сюжетного события, когда начинается глава (например, Zone_Floor2 → On Enter).
// On Restore — что нужно сделать при загрузке сохранения, чтобы мир стал таким, каким его оставила
// прошлая глава (убрать стенку на лестнице, выдать фонарик, включить лифт...).
// При загрузке главы N выполняются On Restore всех чекпоинтов с номером ≤ N по порядку.
public class ChapterCheckpoint : MonoBehaviour
{
    [Tooltip("Номер главы, которая начинается здесь")]
    public int chapter = 2;

    [Tooltip("Куда поставить игрока при загрузке (пустой объект, синяя стрелка смотрит, куда повернуться)")]
    public Transform spawnPoint;

    [Tooltip("Сохранять автоматически при начале главы")]
    public bool autoSave = true;

    public UnityEvent onRestore;

    public void Reach()
    {
        if (chapter < SaveSystem.CurrentChapter) return; // вернулись назад — прогресс не откатываем
        SaveSystem.CurrentChapter = chapter;
        if (autoSave) SaveSystem.Save();
    }

    void OnDrawGizmos()
    {
        if (spawnPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnPoint.position + Vector3.up, 0.4f);
        Gizmos.DrawLine(spawnPoint.position + Vector3.up, spawnPoint.position + Vector3.up + spawnPoint.forward);
    }
}
