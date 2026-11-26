using UnityEngine;
using UnityEngine.Events;

// Засчитывает непрерывное освещение кровати, пока игрок находится в палате.
public class FlashlightTarget : MonoBehaviour
{
    [Tooltip("Сколько секунд держать свет на предмете")]
    public float holdTime = 0.8f;
    [Tooltip("С какого расстояния засчитывается (метры)")]
    public float maxDistance = 6f;

    [Tooltip("Зона палаты (Box Collider с Is Trigger)")]
    public Collider insideZone;

    [Tooltip("Модель кровати, на которую нужно светить")]
    public Transform model;

    [Tooltip("Что произойдёт, когда посветили")]
    public UnityEvent onLit;

    Flashlight flashlight;
    Renderer[] targetRenderers;
    float timer;
    bool done;

    Transform Target => model != null ? model : transform;

    void Awake()
    {
        targetRenderers = Target.GetComponentsInChildren<Renderer>(true);
    }

    void OnDisable()
    {
        timer = 0f;
    }

    void Update()
    {
        if (done) return;
        if (flashlight == null) flashlight = FindObjectOfType<Flashlight>();

        RoomChecklist checklist = RoomChecklist.Instance;
        if (flashlight == null || !flashlight.hasFlashlight || !flashlight.IsOn ||
            (checklist != null && !checklist.IsShown))
        {
            timer = 0f;
            return;
        }

        Light lamp = flashlight.Lamp;
        Vector3 origin = lamp.transform.position;
        if ((insideZone != null && !insideZone.bounds.Contains(origin)) || !IsLit(lamp, origin))
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < holdTime) return;

        done = true;
        onLit?.Invoke();
    }

    bool IsLit(Light lamp, Vector3 origin)
    {
        float range = Mathf.Min(maxDistance, lamp.range);
        Transform target = Target;
        if (!target.gameObject.activeInHierarchy || range <= 0f) return false;

        Vector3 forward = lamp.transform.forward;
        // Прямое попадание засчитывается и у края длинной кровати.
        if (Physics.Raycast(origin, forward, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore) &&
            (hit.transform == target || hit.transform.IsChildOf(target)))
            return true;

        float halfAngle = lamp.type == LightType.Spot ? lamp.spotAngle * 0.5f : 30f;
        float angleCosine = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        foreach (Renderer renderer in targetRenderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

            Bounds bounds = renderer.bounds;
            Vector3 upperCenter = bounds.center + Vector3.up * (bounds.extents.y * 0.65f);
            Vector3 side = bounds.extents.x > bounds.extents.z
                ? Vector3.right * (bounds.extents.x * 0.65f)
                : Vector3.forward * (bounds.extents.z * 0.65f);

            if (InBeam(bounds.center, origin, forward, range, angleCosine, target) ||
                InBeam(upperCenter, origin, forward, range, angleCosine, target) ||
                InBeam(upperCenter + side, origin, forward, range, angleCosine, target) ||
                InBeam(upperCenter - side, origin, forward, range, angleCosine, target))
                return true;
        }
        return false;
    }

    static bool InBeam(Vector3 point, Vector3 origin, Vector3 forward, float range, float angleCosine, Transform target)
    {
        Vector3 offset = point - origin;
        float distance = offset.magnitude;
        if (distance <= Mathf.Epsilon || distance > range) return false;

        Vector3 direction = offset / distance;
        if (Vector3.Dot(forward, direction) < angleCosine) return false;

        return !Physics.Raycast(origin, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) ||
               (hit.transform == target || hit.transform.IsChildOf(target));
    }
}
