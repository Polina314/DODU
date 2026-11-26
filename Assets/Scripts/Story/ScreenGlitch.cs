using UnityEngine;
using UnityEngine.UI;

// «Глюки» экрана: дёргаются полосы, мигает, трясётся камера.
// Ничего ставить на сцену не надо: ScreenGlitch.Play() создаёт эффект сам, ScreenGlitch.Stop() — убирает.
public class ScreenGlitch : MonoBehaviour
{
    static ScreenGlitch instance;

    [Range(0f, 1f)] public float strength = 1f;

    Image[] bars;
    Image flash;
    Transform cam;
    Vector3 camStart;
    float nextBars;

    public static void Play(float strength = 1f)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ScreenGlitch");
            instance = go.AddComponent<ScreenGlitch>();
            instance.Build();
        }
        instance.strength = strength;
        instance.gameObject.SetActive(true);
        FirstPersonController p = FindObjectOfType<FirstPersonController>();
        instance.cam = p != null ? p.cameraHolder : null;
        if (instance.cam != null) instance.camStart = instance.cam.localPosition;
    }

    public static void Stop()
    {
        if (instance == null) return;
        if (instance.cam != null) instance.cam.localPosition = instance.camStart;
        instance.gameObject.SetActive(false);
    }

    void Build()
    {
        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 40;
        CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        flash = MakeImage("Flash");
        Stretch(flash.rectTransform);
        flash.color = Color.clear;

        bars = new Image[7];
        for (int i = 0; i < bars.Length; i++) bars[i] = MakeImage("Bar_" + i);
    }

    Image MakeImage(string n)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    void Update()
    {
        // полосы
        if (Time.unscaledTime >= nextBars)
        {
            nextBars = Time.unscaledTime + Random.Range(0.04f, 0.15f);
            foreach (Image b in bars)
            {
                bool on = Random.value < 0.55f * strength;
                b.gameObject.SetActive(on);
                if (!on) continue;
                RectTransform r = b.rectTransform;
                r.anchorMin = new Vector2(0f, 0.5f); r.anchorMax = new Vector2(1f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(Random.Range(-60f, 60f), Random.Range(-540f, 540f));
                r.sizeDelta = new Vector2(0f, Random.Range(4f, 60f));
                float k = Random.value;
                b.color = k < 0.5f ? new Color(0f, 0f, 0f, 0.85f)
                        : k < 0.8f ? new Color(0.7f, 0.05f, 0.05f, 0.5f)
                        : new Color(1f, 1f, 1f, 0.35f);
            }
            // иногда экран на мгновение темнеет или вспыхивает
            float f = Random.value;
            flash.color = f < 0.06f * strength ? new Color(0f, 0f, 0f, 0.9f)
                        : f < 0.09f * strength ? new Color(1f, 1f, 1f, 0.25f)
                        : Color.clear;
        }
        // камера дёргается
        if (cam != null)
            cam.localPosition = camStart + (Vector3)(Random.insideUnitCircle * 0.03f * strength);
    }
}
