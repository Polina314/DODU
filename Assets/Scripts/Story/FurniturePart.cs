using UnityEngine;
using UnityEngine.Events;

// Ящик стола или дверца шкафа из мебели Аси, которые открываются по E через нашу систему
// (подсказка «[E] Открыть ящик» появляется, как у дверей и записок).
// Куда выдвигать ящик и в какую сторону открывать дверцу, скрипт решает сам при первом открытии:
// ящик едет к игроку, дверца открывается наружу (к игроку), а не внутрь шкафа.
// Скрипты Аси (Drawer, CabinetDoor) при этом выключаются, чтобы E не срабатывало дважды.
// Вешает сам пункт меню Catatonia → Починить мебель.
public class FurniturePart : MonoBehaviour, IInteractable
{
    public enum Mode { Drawer, Door }
    public Mode mode = Mode.Drawer;

    [Tooltip("Ящик: на сколько метров выдвигается")]
    public float distance = 0.3f;
    [Tooltip("Дверца: на сколько градусов открывается")]
    public float angle = 100f;
    public float speed = 3f;

    [Header("Заперто")]
    public bool locked = false;
    public string lockedMessage = "Заперто.";

    [Header("Звук (необязательно)")]
    public AudioSource audioSource;
    public AudioClip openSound, closeSound;

    public UnityEvent onFirstOpen;

    public bool IsOpen { get; private set; }

    Vector3 closedPos;
    Quaternion closedRot;
    Vector3 slideLocal;     // направление выдвигания в координатах родителя
    float sign = 0f;        // для дверцы: +1 или -1
    float t;
    bool decided, openedOnce;

    void Awake()
    {
        closedPos = transform.localPosition;
        closedRot = transform.localRotation;
    }

    public string GetPrompt()
    {
        if (mode == Mode.Drawer) return IsOpen ? "Задвинуть ящик" : "Выдвинуть ящик";
        return IsOpen ? "Закрыть дверцу" : "Открыть дверцу";
    }

    public void Unlock() { locked = false; }
    public void Lock() { locked = true; }

    public void Interact(PlayerInteraction player)
    {
        if (locked)
        {
            if (GameManager.Instance != null && !string.IsNullOrEmpty(lockedMessage)) GameManager.Instance.ShowMessage(lockedMessage, 2f);
            return;
        }
        if (!decided) Decide(player != null ? player.transform.position : Camera.main.transform.position);
        IsOpen = !IsOpen;
        if (audioSource != null)
        {
            AudioClip c = IsOpen ? openSound : closeSound;
            if (c != null) audioSource.PlayOneShot(c);
        }
        if (IsOpen && !openedOnce) { openedOnce = true; onFirstOpen.Invoke(); }
    }

    // Куда открываться — к игроку
    void Decide(Vector3 playerPos)
    {
        decided = true;
        Vector3 toPlayer = playerPos - CenterWorld();
        toPlayer.y = 0f;
        if (mode == Mode.Drawer)
        {
            // выбираем из 4 горизонтальных осей родителя (корпуса стола) ту, что ближе всего к игроку
            Transform p = transform.parent != null ? transform.parent : transform;
            Vector3[] axes = { p.right, -p.right, p.forward, -p.forward, p.up, -p.up };
            Vector3 best = Vector3.zero; float bestDot = -2f;
            foreach (Vector3 a in axes)
            {
                Vector3 h = a; h.y = 0f;
                if (h.sqrMagnitude < 0.5f) continue;   // ось смотрит вверх/вниз — пропускаем
                float d = Vector3.Dot(h.normalized, toPlayer.normalized);
                if (d > bestDot) { bestDot = d; best = a; }
            }
            Vector3 worldDir = best; worldDir.y = 0f; worldDir.Normalize();
            // InverseTransformVector учитывает масштаб корпуса (у моделей из Blender он бывает 100)
            slideLocal = transform.parent != null ? transform.parent.InverseTransformVector(worldDir) : worldDir;
        }
        else
        {
            // пробуем открыть в обе стороны и берём ту, где дверца уходит к игроку, а не в шкаф
            Vector3 c0 = CenterWorld();
            float best = -1e9f;
            foreach (float s in new[] { 1f, -1f })
            {
                transform.localRotation = closedRot * Quaternion.AngleAxis(angle * s, HingeAxisLocal());
                float score = Vector3.Dot(CenterWorld() - c0, toPlayer.normalized);
                if (score > best) { best = score; sign = s; }
            }
            transform.localRotation = closedRot;
        }
    }

    // Ось петли — та ось дверцы, которая смотрит вверх в мире
    Vector3 HingeAxisLocal()
    {
        Vector3 upLocal = transform.InverseTransformDirection(Vector3.up);
        Vector3 a = new Vector3(Mathf.Abs(upLocal.x), Mathf.Abs(upLocal.y), Mathf.Abs(upLocal.z));
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(upLocal.x), 0f, 0f);
        if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(upLocal.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(upLocal.z));
    }

    Vector3 CenterWorld()
    {
        Renderer r = GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : transform.position;
    }

    void Update()
    {
        if (!decided) return;
        t = Mathf.MoveTowards(t, IsOpen ? 1f : 0f, Time.deltaTime * speed);
        float k = Mathf.SmoothStep(0f, 1f, t);
        if (mode == Mode.Drawer)
            transform.localPosition = closedPos + slideLocal * distance * k;
        else
            transform.localRotation = closedRot * Quaternion.AngleAxis(angle * sign * k, HingeAxisLocal());
    }
}
