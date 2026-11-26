using UnityEngine;
using UnityEngine.Events;

public class PickupItem : MonoBehaviour, IInteractable
{
    public enum ItemType { Key, Flashlight, Battery, Other }

    public ItemType type = ItemType.Key;
    public string itemId = "key_1";
    public string displayName = "ключ";
    public float batteryAmount = 30f;
    public AudioClip pickupSound;
    public UnityEvent onPicked;
    [Tooltip("Префаб для осмотра в инвентаре (перетащи сюда Dictaphone_Chief из Project)")]
    public GameObject inspectPrefab;

    [Header("Осмотр: когда предмет «отработал» (диктофон дослушан до конца)")]
    [Tooltip("Сообщение на экране (необязательно). Показывается и в осмотре, и после его закрытия")]
    [TextArea] public string inspectFinishedMessage;
    [Tooltip("Что произойдёт один раз: например SimpleDrawer.Open — открыть ящик с ключом")]
    public UnityEvent onInspectFinished;
    bool inspectFinishedFired;

    public string GetPrompt()
    {
        return "Подобрать " + displayName;
    }

    public void Interact(PlayerInteraction player)
    {
        switch (type)
        {
            case ItemType.Flashlight:
                Flashlight fl = player.GetComponentInChildren<Flashlight>(true);
                if (fl != null)
                {
                    fl.hasFlashlight = true;
                    fl.TurnOn();
                }
                break;
            case ItemType.Battery:
                Flashlight f = player.GetComponentInChildren<Flashlight>(true);
                if (f != null) f.AddBattery(batteryAmount);
                break;
            default:
                if (player.inventory != null) player.inventory.AddItem(itemId);
                InventoryUI.AddItem(
                    itemId,
                    displayName.Length > 0 ? char.ToUpper(displayName[0]) + displayName.Substring(1) : itemId,
                    "Подобрано: " + displayName + ".",
                    null,
                    InventoryUI.Kind.Key
                );
                break;
        }

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        onPicked.Invoke();

        // Прячем объект, но НЕ уничтожаем
        gameObject.SetActive(false);

        // Регистрируем ПРЕФАБ (а не сам объект), чтобы в осмотре была модель
        GameObject model = inspectPrefab != null ? inspectPrefab : gameObject;
        InspectRegistry.Register(itemId, model, NotifyInspectFinished);
        Debug.Log("[PickupItem] Зарегистрирован для осмотра: " + itemId + " (model=" + model.name + ")");
    }

    // Вызывается из режима осмотра, когда запись дослушана. Срабатывает только один раз
    void NotifyInspectFinished()
    {
        if (inspectFinishedFired) return;
        inspectFinishedFired = true;

        if (!string.IsNullOrEmpty(inspectFinishedMessage) && InspectItem.Instance != null)
            InspectItem.Instance.ShowMessage(inspectFinishedMessage);

        onInspectFinished.Invoke();
    }
}
