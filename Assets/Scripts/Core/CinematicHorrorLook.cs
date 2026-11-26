using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

// Пиксельный хоррор-стиль: PS2 + Silent Hill + VHS.
// Рендерит в маленькую текстуру с Point-фильтром = чёткие пиксели.
[RequireComponent(typeof(Camera))]
public class CinematicHorrorLook : MonoBehaviour
{
    [Header("Пиксельность (главное)")]
    [Tooltip("0.5 = половина разрешения (PS2), 0.25 = четверть (PS1)")]
    [Range(0.1f, 1f)] public float renderScale = 0.5f;

    [Header("Туман")]
    public bool enableFog = true;
    // Грязно-зеленый туман (Silent Hill)
    public Color fogColor = new Color(0.12f, 0.18f, 0.12f);
    [Range(0f, 0.2f)] public float fogDensity = 0.08f;

    [Header("Цветокоррекция (болезненный зеленоватый)")]
    [Range(-2f, 2f)] public float postExposure = -0.6f; // Чуть темнее для атмосферы
    [Range(-100f, 100f)] public float temperature = -10f; // Холодный оттенок
    [Range(-100f, 100f)] public float tint = 20f; // Сдвиг в зеленый (в Unity положительный tint часто дает зеленый/пурпурный оттенок в зависимости от версии, но colorFilter это исправит)
    [Range(-100f, 100f)] public float saturation = -60f; // Сильно обесцвечиваем
    [Range(-100f, 100f)] public float contrast = 35f;
    // Фильтр: меньше красного, больше зеленого и желтого
    public Color colorFilter = new Color(0.75f, 0.85f, 0.70f);
    [Header("Зерно плёнки")]
    [Range(0f, 1f)] public float grainIntensity = 0.7f;
    [Range(0f, 3f)] public float grainSize = 1.8f;
    public bool grainColored = true;

    [Header("Виньетка")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.65f;
    [Range(0f, 1f)] public float vignetteSmoothness = 0.35f;

    [Header("Хроматическая аберрация")]
    [Range(0f, 1f)] public float chromaticIntensity = 0.35f;

    [Header("Bloom")]
    [Range(0f, 2f)] public float bloomIntensity = 1.0f;
    [Range(0f, 1f)] public float bloomThreshold = 0.55f;

    [Header("Ambient Occlusion")]
    public bool enableAO = true;
    [Range(0f, 4f)] public float aoIntensity = 0.9f;
    [Range(0.1f, 2f)] public float aoRadius = 0.5f;

    [Header("VHS-дрожание")]
    public bool enableVHSShake = true;
    [Range(0f, 0.2f)] public float shakeIntensity = 0.05f;

    private Camera cam;
    private PostProcessVolume volume;
    private RenderTexture lowResTexture;
    private GameObject displayCanvas;
    private Camera dummyCamera; // Камера-заглушка чтобы убрать "No cameras rendering"
    private bool fogWasOn;
    private Color ambientWas;
    private float shakeTimer = 0f;
    private Vector3 originalPosition;

    void Awake()
    {
        cam = GetComponent<Camera>();
        originalPosition = cam.transform.localPosition;

        // Туман
        fogWasOn = RenderSettings.fog;
        if (enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        // Ambient
        ambientWas = RenderSettings.ambientLight;
        RenderSettings.ambientLight = new Color(0.05f, 0.08f, 0.05f);

        // Тени
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 60f;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowCascades = 2;
        QualitySettings.pixelLightCount = 2;

        // Создаём маленькую текстуру с Point-фильтром = чёткие пиксели
        SetupPixelRender();

        // Пост-обработка
        if (cam.GetComponent<PostProcessLayer>() == null)
        {
            var layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.volumeLayer = LayerMask.GetMask("Default");
            layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
        }

        BuildProfile();
    }

    void SetupPixelRender()
    {
        if (renderScale >= 1f) return;

        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * renderScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * renderScale));

        // Создаём RenderTexture с Point-фильтром = чёткие пиксели
        lowResTexture = new RenderTexture(width, height, 16, RenderTextureFormat.Default);
        lowResTexture.filterMode = FilterMode.Point; // ГЛАВНОЕ! Чёткие пиксели
        lowResTexture.isPowerOfTwo = false;
        lowResTexture.name = "PixelRT";

        // Камера рендерит в маленькую текстуру
        cam.targetTexture = lowResTexture;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // Создаём Canvas для отображения текстуры на весь экран
        displayCanvas = new GameObject("PixelDisplayCanvas");
        Canvas canvas = displayCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1000; // Под всем UI

        CanvasScaler scaler = displayCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        displayCanvas.AddComponent<GraphicRaycaster>();

        // RawImage на весь экран
        GameObject imgObj = new GameObject("PixelImage");
        imgObj.transform.SetParent(displayCanvas.transform, false);

        RectTransform rect = imgObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage rawImg = imgObj.AddComponent<RawImage>();
        rawImg.texture = lowResTexture;
        rawImg.raycastTarget = false; // Пропускаем клики к кнопкам
        rawImg.color = Color.white;

        // Камера-заглушка чтобы убрать "No cameras rendering"
        GameObject dummyObj = new GameObject("DummyCamera");
        dummyCamera = dummyObj.AddComponent<Camera>();
        dummyCamera.clearFlags = CameraClearFlags.SolidColor;
        dummyCamera.backgroundColor = Color.black;
        dummyCamera.cullingMask = 0; // Ничего не рендерит
        dummyCamera.depth = -100;
        dummyCamera.enabled = true;
    }

    void OnDestroy()
    {
        if (enableFog) RenderSettings.fog = fogWasOn;
        RenderSettings.ambientLight = ambientWas;
        if (volume != null) Destroy(volume.gameObject);
        if (lowResTexture != null)
        {
            lowResTexture.Release();
            Destroy(lowResTexture);
        }
        if (displayCanvas != null) Destroy(displayCanvas);
        if (dummyCamera != null) Destroy(dummyCamera.gameObject);
    }

    void Update()
    {
        // VHS-дрожание
        if (enableVHSShake && shakeIntensity > 0f)
        {
            shakeTimer += Time.deltaTime;
            float shakeX = Mathf.Sin(shakeTimer * 20f) * shakeIntensity * 0.02f;
            float shakeY = Mathf.Cos(shakeTimer * 17f) * shakeIntensity * 0.02f;
            cam.transform.localPosition = originalPosition + new Vector3(shakeX, shakeY, 0f);
        }
    }

    void BuildProfile()
    {
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

        // Color Grading
        var cg = profile.AddSettings<ColorGrading>();
        cg.enabled.value = true;
        cg.postExposure.value = postExposure;
        cg.temperature.value = temperature;
        cg.tint.value = tint;
        cg.saturation.value = saturation;
        cg.contrast.value = contrast;
        cg.colorFilter.value = colorFilter;

        // Grain
        var grain = profile.AddSettings<Grain>();
        grain.enabled.value = true;
        grain.colored.value = grainColored;
        grain.intensity.value = grainIntensity;
        grain.size.value = grainSize;

        // Vignette
        var vig = profile.AddSettings<Vignette>();
        vig.enabled.value = true;
        vig.color.value = Color.black;
        vig.intensity.value = vignetteIntensity;
        vig.smoothness.value = vignetteSmoothness;

        // Chromatic Aberration
        var ca = profile.AddSettings<ChromaticAberration>();
        ca.enabled.value = true;
        ca.intensity.value = chromaticIntensity;

        // Bloom
        var bloom = profile.AddSettings<Bloom>();
        bloom.enabled.value = true;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.value = bloomThreshold;
        bloom.softKnee.value = 0.5f;

        // Ambient Occlusion
        if (enableAO)
        {
            var ao = profile.AddSettings<AmbientOcclusion>();
            ao.enabled.value = true;
            ao.intensity.value = aoIntensity;
            ao.radius.value = aoRadius;
        }

        // Volume
        volume = gameObject.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 100;
        volume.profile = profile;
    }
}