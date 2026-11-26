using UnityEngine;
using UnityEngine.UI;

// Яркость: ниже 100 % экран затемняется полупрозрачным чёрным слоем,
// выше 100 % усиливается фоновый свет сцены (Ambient), и в тёмных местах становится лучше видно.
public class BrightnessApplier : MonoBehaviour
{
    [Tooltip("Чёрная картинка на весь экран (DarkOverlay)")]
    public Image darkOverlay;

    [Tooltip("Во сколько раз усиливается фоновый свет при яркости 150 %")]
    public float maxAmbientBoost = 4f;

    Color baseAmbient;

    void Start()
    {
        baseAmbient = RenderSettings.ambientLight;
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= Apply;
        RenderSettings.ambientLight = baseAmbient;
    }

    void Apply()
    {
        float b = GameSettings.Brightness; // 0.5 … 1.5

        float boost = b > 1f ? Mathf.Lerp(1f, maxAmbientBoost, (b - 1f) / 0.5f) : 1f;
        Color ambient = baseAmbient * boost;
        ambient.a = 1f;
        RenderSettings.ambientLight = ambient;

        if (darkOverlay != null)
        {
            Color c = darkOverlay.color;
            c.a = b < 1f ? (1f - b) * 1.2f : 0f; // при 50 % — затемнение 60 %
            darkOverlay.color = c;
        }
    }
}
