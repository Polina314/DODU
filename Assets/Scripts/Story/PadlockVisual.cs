using System.Collections;
using UnityEngine;

// Живая модель кодового замка. Вешается рядом с CodeLock (на тот же объект).
// Пока игрок вводит код, колёсики Wheel_1..Wheel_3 поворачиваются к набранным цифрам,
// то колёсико, которое сейчас набирается, чуть выдвигается.
// Правильный код — дужка (Shackle) подскакивает, а через секунду замок падает на пол.
// Вешает сам пункт меню Catatonia → Подключить предметы подруги.
public class PadlockVisual : MonoBehaviour
{
    public CodeLock codeLock;

    [Header("Колёсики")]
    [Tooltip("Вокруг какой оси крутятся колёсики (в их собственных координатах)")]
    public Vector3 wheelAxis = Vector3.right;
    public float degreesPerDigit = 36f;
    [Tooltip("Если цифры идут не в ту сторону — переключи")]
    public bool invertDirection = true;
    [Tooltip("Если спереди видна не та цифра — сдвинь на 36, 72...")]
    public float zeroAngle = 0f;

    [Header("Дужка")]
    public float shackleLift = 0.012f;

    [Header("После открытия")]
    [Tooltip("Замок падает на пол, чтобы не висел в воздухе, когда дверь откроется")]
    public bool fallAfterUnlock = true;
    public float fallDelay = 1.0f;

    Transform[] wheels = new Transform[3];
    Quaternion[] wheelRest = new Quaternion[3];
    Vector3[] wheelScale = new Vector3[3];
    float[] angle = new float[3];
    Transform shackle;
    Vector3 shackleRest;
    float openT;
    bool fallStarted;

    void Awake()
    {
        if (codeLock == null) codeLock = GetComponent<CodeLock>();
        for (int i = 0; i < 3; i++)
        {
            wheels[i] = FindDeep(transform, "Wheel_" + (i + 1));
            if (wheels[i] != null)
            {
                wheelRest[i] = wheels[i].localRotation;
                wheelScale[i] = wheels[i].localScale;
            }
        }
        shackle = FindDeep(transform, "Shackle");
        if (shackle != null) shackleRest = shackle.localPosition;
    }

    static Transform FindDeep(Transform t, string n)
    {
        foreach (Transform c in t.GetComponentsInChildren<Transform>(true)) if (c.name == n) return c;
        return null;
    }

    void Update()
    {
        if (codeLock == null) return;
        string typed = codeLock.IsUnlocked ? codeLock.code : codeLock.CurrentInput;
        if (typed == null) typed = "";

        for (int i = 0; i < 3; i++)
        {
            if (wheels[i] == null) continue;
            int d = (i < typed.Length && char.IsDigit(typed[i])) ? typed[i] - '0' : 0;
            float target = zeroAngle + d * degreesPerDigit * (invertDirection ? -1f : 1f);
            angle[i] = Mathf.LerpAngle(angle[i], target, Time.deltaTime * 14f);
            wheels[i].localRotation = wheelRest[i] * Quaternion.AngleAxis(angle[i], wheelAxis);

            bool active = codeLock.IsInUse && !codeLock.IsUnlocked && i == Mathf.Min(typed.Length, 2);
            Vector3 want = wheelScale[i] * (active ? 1.12f : 1f);
            wheels[i].localScale = Vector3.Lerp(wheels[i].localScale, want, Time.deltaTime * 12f);
        }

        if (codeLock.IsUnlocked)
        {
            if (shackle != null && openT < 1f)
            {
                openT = Mathf.MoveTowards(openT, 1f, Time.deltaTime * 4f);
                shackle.localPosition = shackleRest + Vector3.up * shackleLift * Mathf.SmoothStep(0f, 1f, openT);
            }
            if (fallAfterUnlock && !fallStarted)
            {
                fallStarted = true;
                StartCoroutine(Fall());
            }
        }
    }

    IEnumerator Fall()
    {
        yield return new WaitForSeconds(fallDelay);
        transform.SetParent(null, true);
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.isKinematic = false;
        rb.AddTorque(Random.onUnitSphere * 0.002f, ForceMode.Impulse);
    }
}
