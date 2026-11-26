using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Электрощиток с 3 рубильниками (глава 2, подсобка).
// Повесить на корневой объект FuseBox (из FuseBox.fbx). Больше ничего вешать не надо:
// скрипт сам найдёт дверцу (FuseBox_Door), рубильники (Lever_1..3) и лампочки (Lamp_1..3)
// и сам добавит им коллайдеры и взаимодействие по E (через вашу систему IInteractable).
//
// Как играется:
//   E на дверцу — открыть / закрыть щиток.
//   E на рубильник — поднять (ВКЛ) / опустить (ВЫКЛ). Лампочка над ним зелёная / красная.
//   Все 3 подняты — один раз срабатывает On All On (свет на этаже, лифт и т.п.).
public class FuseBox : MonoBehaviour
{
    [Header("Дверца")]
    [Tooltip("Угол открытия дверцы. Если открывается внутрь стены — поменять знак")]
    public float doorOpenAngle = -110f;
    public bool doorLocked = false;
    public float doorTime = 0.5f;

    [Header("Рубильники")]
    [Tooltip("Какие рубильники включены в начале игры (по сюжету все выключены)")]
    public bool[] startOn = { false, false, false };
    [Tooltip("Ось поворота рубильника (локальная). Обычно X")]
    public Vector3 leverAxis = new Vector3(1, 0, 0);
    [Tooltip("Угол «выключен» (рубильник вниз). Если опускается не туда — поменять знак")]
    public float leverOffAngle = 150f;
    public float leverTime = 0.25f;

    [Header("Лампочки")]
    public Color lampOff = new Color(0.9f, 0.08f, 0.05f);
    public Color lampOn = new Color(0.15f, 0.95f, 0.25f);
    [Tooltip("Яркость свечения лампочек")]
    public float lampGlow = 2.5f;
    [Tooltip("Лампочки горят только когда дверца открыта (как будто без питания — нет). Выключить, если не нужно")]
    public bool lampsAlwaysLit = true;

    [Header("События")]
    [Tooltip("Все 3 рубильника включены (срабатывает 1 раз): включить свет на этаже, лифт и т.д.")]
    public UnityEvent onAllOn;
    [Tooltip("Отдельные события на каждый рубильник (необязательно)")]
    public UnityEvent onLever1On, onLever2On, onLever3On;

    [Header("Звуки (необязательно)")]
    public AudioClip doorOpenSound, doorCloseSound, lockedSound, leverSound, powerOnSound;

    public bool DoorOpen { get; private set; }
    public bool AllOn { get; private set; }

    Transform door; Quaternion doorClosed; Coroutine doorMove;
    readonly Transform[] levers = new Transform[3];
    readonly Quaternion[] leverUp = new Quaternion[3];
    readonly Renderer[] lamps = new Renderer[3];
    readonly bool[] on = new bool[3];
    readonly Coroutine[] leverMove = new Coroutine[3];
    bool allOnFired;
    AudioSource src;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 1f; src.playOnAwake = false;

        door = FindDeep("FuseBox_Door");
        if (door != null) { doorClosed = door.localRotation; AddPart(door, -1); }
        for (int i = 0; i < 3; i++)
        {
            levers[i] = FindDeep("Lever_" + (i + 1));
            lamps[i] = FindDeep("Lamp_" + (i + 1))?.GetComponent<Renderer>();
            on[i] = startOn != null && i < startOn.Length && startOn[i];
            if (levers[i] != null)
            {
                leverUp[i] = levers[i].localRotation;           // в модели рубильник поднят
                if (!on[i]) levers[i].localRotation = leverUp[i] * Quaternion.AngleAxis(leverOffAngle, leverAxis);
                AddPart(levers[i], i);
            }
            UpdateLamp(i);
        }
        AllOn = on[0] && on[1] && on[2];
        allOnFired = AllOn;
    }

    Transform FindDeep(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    void AddPart(Transform t, int index)
    {
        if (!t.GetComponent<Collider>()) t.gameObject.AddComponent<BoxCollider>();
        var p = t.GetComponent<FuseBoxPart>();
        if (p == null) p = t.gameObject.AddComponent<FuseBoxPart>();
        p.box = this; p.index = index;
    }

    // ---- вызывается из FuseBoxPart ----
    public string PromptFor(int index)
    {
        if (index < 0) return doorLocked ? "Заперто" : (DoorOpen ? "Закрыть щиток" : "Открыть щиток");
        if (!DoorOpen) return "";
        return on[index] ? $"Выключить рубильник {index + 1}" : $"Включить рубильник {index + 1}";
    }

    public void Use(int index)
    {
        if (index < 0) ToggleDoor();
        else if (DoorOpen) ToggleLever(index);
    }

    // ---- можно вызывать и из других скриптов ----
    public void Unlock() { doorLocked = false; }

    public void ToggleDoor()
    {
        if (door == null) return;
        if (doorLocked) { Play(lockedSound); return; }
        DoorOpen = !DoorOpen;
        Play(DoorOpen ? doorOpenSound : doorCloseSound);
        if (doorMove != null) StopCoroutine(doorMove);
        doorMove = StartCoroutine(Rotate(door, DoorOpen ? doorClosed * Quaternion.Euler(0f, doorOpenAngle, 0f) : doorClosed, doorTime, true));
    }

    public void ToggleLever(int i)
    {
        if (levers[i] == null) return;
        on[i] = !on[i];
        Play(leverSound);
        if (leverMove[i] != null) StopCoroutine(leverMove[i]);
        var target = on[i] ? leverUp[i] : leverUp[i] * Quaternion.AngleAxis(leverOffAngle, leverAxis);
        leverMove[i] = StartCoroutine(Rotate(levers[i], target, leverTime, false));
        UpdateLamp(i);
        if (on[i]) (i == 0 ? onLever1On : i == 1 ? onLever2On : onLever3On)?.Invoke();

        AllOn = on[0] && on[1] && on[2];
        if (AllOn && !allOnFired)
        {
            allOnFired = true;
            Play(powerOnSound);
            onAllOn?.Invoke();
        }
    }

    public bool IsOn(int i) => on[i];

    void UpdateLamp(int i)
    {
        var r = lamps[i];
        if (r == null) return;
        var m = r.material;                       // своя копия материала у каждой лампочки
        var c = on[i] ? lampOn : lampOff;
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", lampsAlwaysLit ? c * lampGlow : Color.black);
    }

    IEnumerator Rotate(Transform t, Quaternion target, float time, bool smooth)
    {
        Quaternion start = t.localRotation; float k = 0f;
        while (k < 1f)
        {
            k += Time.deltaTime / Mathf.Max(0.01f, time);
            t.localRotation = Quaternion.Slerp(start, target, smooth ? Mathf.SmoothStep(0f, 1f, k) : k);
            yield return null;
        }
        t.localRotation = target;
    }

    void Play(AudioClip c) { if (c != null) src.PlayOneShot(c); }
}
