using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Вещь парня, которую сначала РАССМАТРИВАЕШЬ, а потом забираешь (фото с мамой, медкарта).
// Нажал E — картинка на весь экран (как записка). Закрыл — вещь засчитана (StoryItemsHUD → Collect)
// и пропала со сцены. Картинку потом можно открыть снова из инвентаря (I).
// Вешает сам пункт меню Catatonia → Подключить предметы подруги.
public class StoryItemView : MonoBehaviour, IInteractable
{
    public string prompt = "Взять фото";
    public string title = "Фото";
    [Tooltip("Картинка на весь экран (Texture Type = Sprite)")]
    public Sprite image;

    [Header("Какая это вещь из трёх")]
    [Tooltip("Пусто — найдётся сама")]
    public StoryItemsHUD hud;
    [Tooltip("1 = первая вещь, 2 = вторая, 3 = третья")]
    public int number = 2;

    [Tooltip("Что ещё сделать, когда вещь забрали (необязательно)")]
    public UnityEvent onCollected;

    bool used;

    public string GetPrompt()
    {
        return used ? "" : prompt;
    }

    public void Interact(PlayerInteraction player)
    {
        if (used) return;
        used = true;
        if (image != null && NoteUI.Instance != null)
        {
            NoteUI.Instance.Show(title, "", image);
            StartCoroutine(WaitClose());
        }
        else
        {
            Finish();
        }
    }

    IEnumerator WaitClose()
    {
        yield return null;
        while (NoteUI.Instance != null && NoteUI.Instance.IsOpen) yield return null;
        Finish();
    }

    void Finish()
    {
        StoryItemsHUD h = hud != null ? hud : StoryItemsHUD.Instance;
        if (h != null) h.Collect(number);
        onCollected.Invoke();
        gameObject.SetActive(false);
    }
}
