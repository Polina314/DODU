using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Отмечает палату, когда игрок осветил каждую привязанную кровать.
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

    [Tooltip("Остальные кровати в этой палате. Каждая засчитывается отдельно")]
    [SerializeField] Transform[] additionalBeds;

    [Tooltip("Что произойдёт, когда все кровати освещены")]
    public UnityEvent onLit;

    sealed class BedInspection
    {
        public Transform target;
        public Renderer[] renderers;
        public float timer;
        public bool checkedBed;
    }

    readonly List<BedInspection> beds = new List<BedInspection>();
    Flashlight flashlight;
    int checkedCount;
    bool done;

    void Awake()
    {
        var registered = new HashSet<Transform>();
        AddBed(model != null ? model : transform, registered);
        if (additionalBeds != null)
            foreach (Transform bed in additionalBeds)
                AddBed(bed, registered);
    }

    void AddBed(Transform bed, HashSet<Transform> registered)
    {
        if (bed == null || !registered.Add(bed)) return;
        beds.Add(new BedInspection
        {
            target = bed,
            renderers = bed.GetComponentsInChildren<Renderer>(true)
        });
    }

    void OnDisable()
    {
        ResetTimers();
    }

    void ResetTimers()
    {
        foreach (BedInspection bed in beds)
            bed.timer = 0f;
    }

    void Update()
    {
        if (done) return;
        if (flashlight == null) flashlight = FindObjectOfType<Flashlight>();

        RoomChecklist checklist = RoomChecklist.Instance;
        if (flashlight == null || !flashlight.hasFlashlight || !flashlight.IsOn ||
            (checklist != null && !checklist.IsShown))
        {
            ResetTimers();
            return;
        }

        Light lamp = flashlight.Lamp;
        Vector3 origin = lamp.transform.position;
        if (insideZone != null && !insideZone.bounds.Contains(origin))
        {
            ResetTimers();
            return;
        }

        float range = Mathf.Min(maxDistance, lamp.range);
        Vector3 forward = lamp.transform.forward;
        bool hasCenterHit = Physics.Raycast(origin, forward, out RaycastHit centerHit, range, ~0,
                                           QueryTriggerInteraction.Ignore);
        Transform centerTarget = hasCenterHit ? centerHit.collider.transform : null;
        int previousCount = checkedCount;
        foreach (BedInspection bed in beds)
        {
            if (bed.checkedBed) continue;
            if (!IsLit(bed, lamp, origin, forward, range, centerTarget))
            {
                bed.timer = 0f;
                continue;
            }

            bed.timer += Time.deltaTime;
            if (bed.timer < holdTime) continue;

            bed.checkedBed = true;
            checkedCount++;
        }

        if (checkedCount == beds.Count)
        {
            done = true;
            onLit?.Invoke();
        }
        else if (checkedCount != previousCount)
        {
            GameManager.Instance?.ShowMessage($"Кровати проверены: {checkedCount}/{beds.Count}", 2f);
        }
    }

    static bool IsLit(BedInspection bed, Light lamp, Vector3 origin, Vector3 forward, float range,
                      Transform centerTarget)
    {
        Transform target = bed.target;
        if (target == null || !target.gameObject.activeInHierarchy || range <= 0f) return false;
        if (centerTarget != null && centerTarget.IsChildOf(target)) return true;

        float halfAngle = lamp.type == LightType.Spot ? lamp.spotAngle * 0.5f : 30f;
        float angleCosine = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        foreach (Renderer renderer in bed.renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

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
               hit.collider.transform.IsChildOf(target);
    }
}
