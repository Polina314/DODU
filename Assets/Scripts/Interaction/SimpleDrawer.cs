using UnityEngine;

// Простой скрипт для выдвижения ящика.
// Вешается на сам объект ящика (ту часть, которая должна выезжать).
// Открывается вызовом Open() — например, из события On Inspect Finished у PickupItem диктофона.
public class SimpleDrawer : MonoBehaviour
{
    [Tooltip("На сколько ящик выдвигается, в локальных координатах РОДИТЕЛЯ ящика. " +
             "Выдели ящик в сцене: зелёная линия показывает, куда он поедет. Не туда — меняй ось или знак.")]
    public Vector3 openOffset = new Vector3(0f, 0f, 0.3f);

    [Tooltip("Скорость выдвижения")]
    public float openSpeed = 2f;

    [Tooltip("Звук выдвижения (необязательно)")]
    public AudioClip openSound;

    [Header("Ключ внутри ящика")]
    [Tooltip("Что включить, когда ящик открылся. Сюда можно перетащить ключ (PickupItem), " +
             "который лежит в ящике и до этого выключен. Ключ должен быть ДОЧЕРНИМ объектом ящика, тогда он поедет вместе с ним")]
    public GameObject[] enableOnOpen;

    [Tooltip("Коллайдеры, которые надо выключить при открытии. Если коллайдер самого ящика закрывает ключ, " +
             "игрок не сможет навести на ключ. Перетащи сюда коллайдер ящика")]
    public Collider[] disableCollidersOnOpen;

    Vector3 closedPos;
    Vector3 targetPos;
    bool isOpening;
    bool isOpen;

    void Awake()
    {
        closedPos = transform.localPosition;
        targetPos = closedPos;
    }

    // Вызывается из события (UnityEvent)
    public void Open()
    {
        if (isOpen) return;                 // повторные вызовы игнорируем
        isOpen = true;
        isOpening = true;
        targetPos = closedPos + openOffset;

        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        foreach (GameObject go in enableOnOpen) if (go != null) go.SetActive(true);
        foreach (Collider c in disableCollidersOnOpen) if (c != null) c.enabled = false;

        Debug.Log("[SimpleDrawer] Открываю ящик: " + name);
    }

    // Для проверки в Play Mode: правый клик по заголовку компонента -> "Тест: открыть ящик"
    [ContextMenu("Тест: открыть ящик")]
    void TestOpen() { Open(); }

    void Update()
    {
        if (!isOpening) return;

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * openSpeed);

        // Почти доехал — ставим точно и останавливаемся
        if (Vector3.Distance(transform.localPosition, targetPos) < 0.001f)
        {
            transform.localPosition = targetPos;
            isOpening = false;
        }
    }

    // Зелёная линия в окне Scene: куда поедет ящик (виден, когда ящик выделен)
    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? closedPos : transform.localPosition;
        Vector3 end = start + openOffset;
        Transform parent = transform.parent;
        Vector3 a = parent != null ? parent.TransformPoint(start) : start;
        Vector3 b = parent != null ? parent.TransformPoint(end) : end;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(b, 0.03f);
    }
}
