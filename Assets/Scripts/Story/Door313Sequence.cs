using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Финал главы 4: в тупике стоит монстр, экран глючит, дверь 313 то появляется, то снова стена.
// Игрок подходит вплотную — монстр исчезает, глюки прекращаются, дверь остаётся.
// Вешается на пустой объект (Ending313). Этот объект должен быть ВКЛЮЧЁН с начала игры:
// он сразу прячет дверь и показывает стену.
// Запуск из события (зона в коридоре): Door313Sequence → Begin ().
public class Door313Sequence : MonoBehaviour
{
    [Tooltip("Монстр БЕЗ скрипта ИИ, который просто стоит в конце коридора (в начале выключен)")]
    public GameObject staticMonster;
    [Tooltip("Бродящий монстр с ИИ. Выключается, когда начинается эта сцена")]
    public GameObject roamingMonster;
    [Tooltip("Дверь (или комната) 313")]
    public GameObject doorObject;
    [Tooltip("Стена-заглушка на месте палаты 313")]
    public GameObject wallObject;

    [Tooltip("В начале игры спрятать дверь и показать стену")]
    public bool hideDoorAtStart = true;
    [Tooltip("На каком расстоянии от монстра он исчезает")]
    public float vanishDistance = 1.8f;
    public float minFlicker = 0.06f;
    public float maxFlicker = 0.45f;

    public string vanishMessage = "Он исчез... А дверь осталась.";
    public string finishedObjective = "Открыть дверь 313";

    [Header("Свет и красный дым во время сцены (настраивать не обязательно)")]
    [Tooltip("Погасить все лампы этажа, кроме одной — над монстром")]
    public bool darkenFloor = true;
    [Tooltip("Лампы этажа ищутся по высоте: от этой...")]
    public float floorMinY = 7f;
    [Tooltip("...до этой (3 этаж: 7 — 11.5)")]
    public float floorMaxY = 11.5f;
    [Tooltip("Лампа над монстром. Можно оставить пустым — возьмётся ближайшая к монстру (или создастся новая)")]
    public Light redLamp;
    public Color redLampColor = new Color(1f, 0.08f, 0.04f);
    public float redLampIntensity = 4f;
    public float redLampRange = 9f;
    [Tooltip("Красный дым (густой туман) во время сцены")]
    public bool redSmoke = true;
    public Color smokeColor = new Color(0.45f, 0.02f, 0.02f);
    [Tooltip("Густота дыма: 0.1 — средне, 0.2 — очень густо")]
    public float smokeDensity = 0.13f;
    [Tooltip("Когда монстр исчез — вернуть свет и туман как было")]
    public bool restoreAfter = true;

    [Tooltip("Что произойдёт, когда монстр исчез и дверь осталась")]
    public UnityEvent onFinished;

    // что запомнили, чтобы потом вернуть
    readonly List<Light> turnedOff = new List<Light>();
    Light usedLamp;
    bool createdLamp;
    Color lampColor; float lampIntensity, lampRange; bool lampEnabled;
    bool fogWas; Color fogColorWas; float fogDensityWas; FogMode fogModeWas;

    bool running, done;
    Transform player;

    void Awake()
    {
        if (hideDoorAtStart)
        {
            if (doorObject != null) doorObject.SetActive(false);
            if (wallObject != null) wallObject.SetActive(true);
        }
        if (staticMonster != null) staticMonster.SetActive(false);
    }

    public void Begin()
    {
        if (running || done) return;
        running = true;
        if (roamingMonster != null) roamingMonster.SetActive(false);
        if (staticMonster != null) staticMonster.SetActive(true);
        FirstPersonController p = FindObjectOfType<FirstPersonController>();
        player = p != null ? p.transform : null;
        ScreenGlitch.Play();
        LightsAndSmokeOn();
        StartCoroutine(Flicker());
    }

    void LightsAndSmokeOn()
    {
        Vector3 m = staticMonster != null ? staticMonster.transform.position : transform.position;

        if (darkenFloor)
        {
            // все включённые лампы этажа, кроме фонарика игрока
            List<Light> floorLights = new List<Light>();
            foreach (Light l in FindObjectsOfType<Light>())
            {
                if (!l.enabled || l.type == LightType.Directional) continue;
                float y = l.transform.position.y;
                if (y < floorMinY || y > floorMaxY) continue;
                if (player != null && l.transform.IsChildOf(player)) continue;
                floorLights.Add(l);
            }

            // лампа над монстром: указанная, или ближайшая к нему (не дальше 7 м), или новая
            usedLamp = redLamp;
            if (usedLamp == null)
            {
                float best = 7f;
                foreach (Light l in floorLights)
                {
                    Vector3 d = l.transform.position - m; d.y = 0f;
                    if (d.magnitude < best) { best = d.magnitude; usedLamp = l; }
                }
            }
            if (usedLamp == null)
            {
                GameObject go = new GameObject("RedLamp313");
                go.transform.position = m + Vector3.up * 2.6f;
                usedLamp = go.AddComponent<Light>();
                usedLamp.type = LightType.Point;
                createdLamp = true;
            }

            foreach (Light l in floorLights)
            {
                if (l == usedLamp) continue;
                l.enabled = false;
                turnedOff.Add(l);
            }

            lampColor = usedLamp.color; lampIntensity = usedLamp.intensity;
            lampRange = usedLamp.range; lampEnabled = usedLamp.enabled;
            usedLamp.enabled = true;
            usedLamp.color = redLampColor;
            usedLamp.intensity = redLampIntensity;
            usedLamp.range = redLampRange;
            FlickeringLight fl = usedLamp.GetComponent<FlickeringLight>();
            if (fl != null) fl.enabled = false;   // эта лампа горит ровно
        }

        if (redSmoke)
        {
            fogWas = RenderSettings.fog; fogColorWas = RenderSettings.fogColor;
            fogDensityWas = RenderSettings.fogDensity; fogModeWas = RenderSettings.fogMode;
            foreach (AtmosphereFog af in FindObjectsOfType<AtmosphereFog>()) af.StopAllCoroutines();
            StartCoroutine(SmokeIn());
        }
    }

    IEnumerator SmokeIn()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        Color c0 = RenderSettings.fogColor;
        float d0 = fogWas ? RenderSettings.fogDensity : 0f;
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            float k = t / 1.5f;
            RenderSettings.fogColor = Color.Lerp(c0, smokeColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(d0, smokeDensity, k);
            yield return null;
        }
    }

    void LightsAndSmokeOff()
    {
        foreach (Light l in turnedOff) if (l != null) l.enabled = true;
        turnedOff.Clear();
        if (usedLamp != null)
        {
            if (createdLamp) Destroy(usedLamp.gameObject);
            else
            {
                usedLamp.color = lampColor; usedLamp.intensity = lampIntensity;
                usedLamp.range = lampRange; usedLamp.enabled = lampEnabled;
                FlickeringLight fl = usedLamp.GetComponent<FlickeringLight>();
                if (fl != null) fl.enabled = true;
            }
        }
        if (redSmoke)
        {
            RenderSettings.fog = fogWas; RenderSettings.fogColor = fogColorWas;
            RenderSettings.fogDensity = fogDensityWas; RenderSettings.fogMode = fogModeWas;
        }
    }

    void Update()
    {
        if (!running || player == null || staticMonster == null) return;
        Vector3 d = player.position - staticMonster.transform.position;
        d.y = 0f;
        if (d.magnitude <= vanishDistance) Finish();
    }

    IEnumerator Flicker()
    {
        bool showDoor = false;
        while (running)
        {
            showDoor = !showDoor;
            if (doorObject != null) doorObject.SetActive(showDoor);
            if (wallObject != null) wallObject.SetActive(!showDoor);
            yield return new WaitForSeconds(Random.Range(minFlicker, maxFlicker));
        }
    }

    void Finish()
    {
        running = false;
        done = true;
        StopAllCoroutines();
        if (staticMonster != null) staticMonster.SetActive(false);
        ScreenGlitch.Stop();
        if (restoreAfter) LightsAndSmokeOff();
        if (doorObject != null) doorObject.SetActive(true);
        if (wallObject != null) wallObject.SetActive(false);
        if (GameManager.Instance != null && !string.IsNullOrEmpty(vanishMessage)) GameManager.Instance.ShowMessage(vanishMessage, 4f);
        if (ObjectiveUI.Instance != null && !string.IsNullOrEmpty(finishedObjective)) ObjectiveUI.Instance.SetObjective(finishedObjective);
        onFinished.Invoke();
    }
}
