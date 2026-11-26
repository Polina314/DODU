using System.Collections;
using System.Reflection;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Осмотр предмета в руках, как в Resident Evil: предмет висит перед камерой, его можно
// крутить во все стороны (зажать ЛКМ и двигать мышью, либо стрелки), приближать колесом.
// Ставить на сцену ничего не нужно — создаётся сам.
// Открывается из инвентаря: InspectItem.Instance.Open(модель, имя, описание)
public class InspectItem : MonoBehaviour
{
    public static InspectItem Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    [Header("Управление")]
    public float rotateSpeed = 6f;        // чувствительность мыши
    public float keyRotateSpeed = 120f;   // градусов в секунду для стрелок
    public float zoomSpeed = 1.5f;
    public float smoothing = 14f;         // плавность вращения и зума

    [Header("Размер и расстояние")]
    public float targetSize = 0.3f;       // самая длинная сторона предмета приводится к этому размеру
    public float startDistance = 0.65f;
    public float minDistance = 0.3f;
    public float maxDistance = 1.2f;

    // "Сцена осмотра" строится далеко от уровня, чтобы стены и мебель не попадали в кадр
    static readonly Vector3 StagePos = new Vector3(0f, -1000f, 0f);

    GameObject root, holder, spawned;
    Camera cam;
    FirstPersonController player;
    Quaternion targetRot = Quaternion.identity;
    float dist, targetDist;
    RenderTexture rt;
    bool open;

    // --- предметы, которые можно «использовать» в осмотре (диктофон) ---
    IInteractable playable;
    PlayerInteraction playerInteraction;
    AudioSource watched;
    float lastTime;
    bool finishedSent;
    string itemId;
    TMP_Text messageLabel;
    string pendingMessage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance == null)
            new GameObject("InspectItem").AddComponent<InspectItem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        gameObject.name = "InspectItem";
    }

    // ---------- открыть / закрыть ----------
    public void Open(GameObject prefab, string itemName, string desc, string id = null)
    {
        if (open || prefab == null) return;
        open = true;
        IsOpen = true;
        itemId = id;
        finishedSent = false;
        pendingMessage = null;
        playerInteraction = FindObjectOfType<PlayerInteraction>();

        player = FindObjectOfType<FirstPersonController>();
        if (player != null) player.canMove = false;       // игрок не ходит и не крутит камеру
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        try
        {
            Build(prefab, itemName, desc);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            Close(true);
        }
    }

    public void Close() { Close(true); }

    void Close(bool giveControl)
    {
        if (!open) return;
        open = false;
        IsOpen = false;
        if (cam != null) cam.targetTexture = null;
        if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
        if (root != null) Destroy(root);
        root = holder = spawned = null;
        cam = null;
        NoteUI.ClosedFrame = Time.frameCount;             // чтобы та же кнопка не сработала в игре

        playable = null; watched = null; messageLabel = null;
        if (!string.IsNullOrEmpty(pendingMessage) && GameManager.Instance != null)
            GameManager.Instance.Say(pendingMessage);     // после закрытия осмотра сообщение видно уже в игре
        pendingMessage = null;

        if (giveControl)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            StartCoroutine(GiveControlNextFrame());
        }
    }

    // Управление возвращаем на следующий кадр, чтобы E / I / Tab не сработали ещё и в игре
    IEnumerator GiveControlNextFrame()
    {
        yield return null;
        if (open || player == null || !player.enabled || PauseMenu.IsPaused) yield break;
        player.canMove = true;
    }

    // ---------- построение сцены осмотра ----------
    void Build(GameObject prefab, string itemName, string desc)
    {
        root = new GameObject("InspectRoot");
        root.transform.position = StagePos;

        // --- экран: затемнение игры + картинка с камеры осмотра + тексты ---
        GameObject overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        overlay.transform.SetParent(root.transform, false);
        Canvas c = overlay.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;
        CanvasScaler sc = overlay.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(overlay.transform, false);
        RectTransform dr = (RectTransform)dim.transform;
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        Image dimImg = dim.GetComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.85f);
        dimImg.raycastTarget = false;

        rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
        GameObject view = new GameObject("View", typeof(RectTransform), typeof(RawImage));
        view.transform.SetParent(overlay.transform, false);
        RectTransform vr = (RectTransform)view.transform;
        vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
        vr.anchoredPosition = Vector2.zero;
        vr.sizeDelta = new Vector2(900f, 900f);
        RawImage ri = view.GetComponent<RawImage>();
        ri.texture = rt;
        ri.raycastTarget = false;

        MakeText(overlay.transform, itemName, 34, new Vector2(0, 400), new Vector2(1400, 50), Color.white, true);
        MakeText(overlay.transform, desc, 22, new Vector2(0, 340), new Vector2(1200, 90), new Color(0.75f, 0.75f, 0.75f), false);

        // --- камера осмотра: рисует не на экран, а в RenderTexture ---
        GameObject camObj = new GameObject("InspectCamera", typeof(Camera));
        camObj.transform.SetParent(root.transform, false);
        cam = camObj.GetComponent<Camera>();
        cam.targetTexture = rt;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 0f);   // прозрачный фон: видно затемнённую игру
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;
        cam.depth = -50;

        // --- свет: ТОЧЕЧНЫЙ, рядом с предметом (а не Directional), чтобы не светить на весь уровень ---
        MakePointLight(camObj.transform, new Vector3(0.5f, 0.5f, 0.15f), 3f);
        MakePointLight(camObj.transform, new Vector3(-0.6f, -0.2f, 0.25f), 1.2f);

        // --- держатель: сюда кладём предмет, его и вращаем ---
        dist = targetDist = Mathf.Clamp(startDistance, minDistance, maxDistance);
        holder = new GameObject("Holder");
        holder.transform.SetParent(camObj.transform, false);
        holder.transform.localPosition = new Vector3(0f, 0f, dist);
        targetRot = Quaternion.identity;

        // --- копия предмета ---
        spawned = Instantiate(prefab, holder.transform);
        spawned.name = "Inspect_" + prefab.name;
        spawned.transform.localRotation = prefab.transform.rotation;      // как предмет лежал / задуман
        spawned.transform.localScale = prefab.transform.lossyScale;
        Sanitize(spawned);
        SetupPlayable(spawned);                           // диктофон: оставляем рабочим
        spawned.SetActive(true);                          // копия неактивного (подобранного) предмета неактивна

        // Масштаб подгоняем под targetSize и центрируем, чтобы крутился вокруг середины
        Renderer[] rs = GetMeshRenderers(spawned);
        if (rs.Length > 0)
        {
            Bounds b = CombinedBounds(rs);
            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim > 0.0001f) spawned.transform.localScale *= targetSize / maxDim;

            b = CombinedBounds(rs);
            spawned.transform.position += holder.transform.position - b.center;
        }
        else
        {
            Debug.LogWarning("[InspectItem] У модели нет рендереров: " + prefab.name);
        }

        string hint = playable != null
            ? "E — включить / остановить запись   ·   ЛКМ + мышь / стрелки — вращать   ·   колесо — приблизить   ·   R — сбросить   ·   ПКМ — закрыть"
            : "ЛКМ + мышь / стрелки — вращать   ·   колесо — приблизить   ·   R — сбросить   ·   ПКМ / E — закрыть";
        MakeText(overlay.transform, hint, 18, new Vector2(0, -470), new Vector2(1800, 40), new Color(0.55f, 0.55f, 0.55f), false);
        messageLabel = MakeText(overlay.transform, "", 28, new Vector2(0, -395), new Vector2(1500, 60), new Color(1f, 0.9f, 0.6f), false);

        Debug.Log("[InspectItem] Осмотр открыт: " + prefab.name + ", рендереров: " + rs.Length + ", можно включить: " + (playable != null));
    }

    // Из копии убираем всё, что могло бы жить своей жизнью: скрипты, физику, звук, свет
    static void Sanitize(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb) mb.enabled = false;
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) { a.playOnAwake = false; a.spatialBlend = 0f; }   // 2D: слышно везде
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
    }

    static Renderer[] GetMeshRenderers(GameObject go)
    {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        return list.ToArray();
    }

    static Bounds CombinedBounds(Renderer[] rs)
    {
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // ---------- каждый кадр ----------
    void Update()
    {
        if (!open) return;

        // открыли паузу или монстр поймал игрока — осмотр закрываем, управление не трогаем
        if (PauseMenu.IsPaused || (player != null && !player.enabled)) { Close(false); return; }

        // E: у диктофона — включить / остановить запись, у остальных предметов — закрыть
        if (Input.GetMouseButtonDown(1) || (playable == null && Input.GetKeyDown(KeyCode.E)) ||
            Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Backspace))
        { Close(true); return; }

        if (playable != null)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                try { playable.Interact(playerInteraction); }
                catch (System.Exception ex) { Debug.LogException(ex); }
                MakeAudio2D();
            }
            WatchAudio();
        }

        if (holder == null) return;

        // Вращение: мышь с зажатой ЛКМ + стрелки. Крутим вокруг осей КАМЕРЫ, без ограничений —
        // предмет можно повернуть любой стороной, в том числе дном.
        float dx = 0f, dy = 0f;
        if (Input.GetMouseButton(0))
        {
            dx = Input.GetAxis("Mouse X") * rotateSpeed;
            dy = Input.GetAxis("Mouse Y") * rotateSpeed;
        }
        float k = keyRotateSpeed * Time.unscaledDeltaTime;
        if (Input.GetKey(KeyCode.LeftArrow)) dx -= k;
        if (Input.GetKey(KeyCode.RightArrow)) dx += k;
        if (Input.GetKey(KeyCode.UpArrow)) dy += k;
        if (Input.GetKey(KeyCode.DownArrow)) dy -= k;
        if (dx != 0f || dy != 0f)
            targetRot = Quaternion.AngleAxis(-dx, Vector3.up) * Quaternion.AngleAxis(dy, Vector3.right) * targetRot;
        if (Input.GetKeyDown(KeyCode.R)) targetRot = Quaternion.identity;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f) targetDist = Mathf.Clamp(targetDist - scroll * zoomSpeed, minDistance, maxDistance);

        // плавно догоняем цель
        float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        holder.transform.localRotation = Quaternion.Slerp(holder.transform.localRotation, targetRot, t);
        dist = Mathf.Lerp(dist, targetDist, t);
        holder.transform.localPosition = new Vector3(0f, 0f, dist);
    }

    // ---------- диктофон в осмотре ----------
    // Оставляем рабочим скрипт предмета, который умеет «использоваться» (Dictaphone), и ловим момент «дослушал»
    void SetupPlayable(GameObject go)
    {
        playable = null;
        watched = null;
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is PickupItem) continue;
            if (mb is IInteractable ia)
            {
                mb.enabled = true;
                playable = ia;

                // у диктофона есть событие On Finished — подписываемся на него, если поле так называется
                FieldInfo f = mb.GetType().GetField("onFinished",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.GetValue(mb) is UnityEvent ev) ev.AddListener(NotifyFinished);
                break;
            }
        }
    }

    // Звук делаем 2D: сцена осмотра далеко от игрока, 3D-звук отсюда не слышен
    void MakeAudio2D()
    {
        if (spawned == null) return;
        foreach (var a in spawned.GetComponentsInChildren<AudioSource>(true)) a.spatialBlend = 0f;
    }

    // Запасной способ узнать, что запись дослушана до конца: звук остановился рядом с концом клипа
    void WatchAudio()
    {
        if (spawned == null) return;
        if (watched == null)
        {
            foreach (var a in spawned.GetComponentsInChildren<AudioSource>(true))
                if (a.isPlaying && a.clip != null && a.clip.length > 1f) { watched = a; a.spatialBlend = 0f; break; }
            if (watched == null) return;
        }
        if (watched.isPlaying) { lastTime = watched.time; return; }

        if (watched.clip != null && lastTime >= watched.clip.length - 0.35f) NotifyFinished();
        watched = null;
        lastTime = 0f;
    }

    void NotifyFinished()
    {
        if (finishedSent) return;
        finishedSent = true;
        InspectRegistry.NotifyFinished(itemId);
    }

    // Сообщение поверх осмотра (обычный текст игры скрыт под затемнением)
    public void ShowMessage(string text)
    {
        pendingMessage = text;
        if (messageLabel != null) messageLabel.text = text;
    }

    // ---------- мелочи ----------
    static void MakePointLight(Transform parent, Vector3 localPos, float intensity)
    {
        GameObject go = new GameObject("InspectLight", typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.GetComponent<Light>();
        l.type = LightType.Point;
        l.range = 4f;
        l.color = Color.white;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static TMP_Text MakeText(Transform parent, string t, float size, Vector2 pos, Vector2 box, Color col, bool bold)
    {
        GameObject go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = box;
        TextMeshProUGUI tx = go.AddComponent<TextMeshProUGUI>();
        tx.text = t;
        tx.fontSize = size;
        tx.alignment = TextAlignmentOptions.Center;
        tx.color = col;
        tx.raycastTarget = false;
        if (bold) tx.fontStyle = FontStyles.Bold;
        return tx;
    }
}
