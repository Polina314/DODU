using UnityEngine;

// Выдвижной ящик стола с подсказкой «[E] Выдвинуть ящик». (Называется OfficeDrawer,
// потому что у мебели Аси уже есть свой скрипт Drawer.)
// Вешается на сам ящик (у него нужен Collider). Предметы внутри — его дочерние объекты,
// тогда они выезжают вместе с ящиком.
public class OfficeDrawer : MonoBehaviour, IInteractable
{
    [Tooltip("На сколько метров выдвигается")]
    public float distance = 0.35f;
    [Tooltip("Куда выдвигается в координатах ящика. Синяя стрелка (Z) в режиме Local")]
    public Vector3 direction = new Vector3(0f, 0f, 1f);
    public float speed = 3f;
    public AudioSource audioSource;
    public AudioClip sound;

    Vector3 closedPos;
    bool isOpen;
    float t;

    void Start()
    {
        closedPos = transform.localPosition;
    }

    public string GetPrompt()
    {
        return isOpen ? "Задвинуть ящик" : "Выдвинуть ящик";
    }

    public void Interact(PlayerInteraction player)
    {
        isOpen = !isOpen;
        if (audioSource != null && sound != null) audioSource.PlayOneShot(sound);
    }

    void Update()
    {
        t = Mathf.MoveTowards(t, isOpen ? 1f : 0f, Time.deltaTime * speed);
        // смещение в метрах по направлению ящика (с учётом его поворота)
        Vector3 worldDir = transform.TransformDirection(direction.normalized);
        Vector3 localStep = transform.parent != null ? transform.parent.InverseTransformDirection(worldDir) : worldDir;
        transform.localPosition = closedPos + localStep * distance * Mathf.SmoothStep(0f, 1f, t);
    }
}
