using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Инвентарь как в Майнкрафте: на экране ничего не висит, по клавише I (или Tab) открывается окно
// с клетками. В клетках — всё, что игрок подобрал: вещи парня, ключи, фонарик, записки.
// Записку из инвентаря можно перечитать (щелчок по ней или Enter).
//
// НА СЦЕНУ НИЧЕГО СТАВИТЬ НЕ НАДО: инвентарь создаётся сам, когда загружается сцена с игроком.
// Предметы попадают сюда сами — из PickupItem, NoteItem (с галочкой Take After Reading) и StoryItemsHUD.
public class InventoryUI : MonoBehaviour
{
    public enum Kind { Item, Key, Tool, Note }

    public class Entry
    {
        public string id;
        public string name;
        public string description;
        public Sprite icon;
        public Kind kind;
        public string noteTitle, noteText;
        public Sprite noteImage;
    }

    public static InventoryUI Instance { get; private set; }

    const int Columns = 6;
    const int Rows = 3;
    const float Slot = 110f;
    const float Gap = 12f;

    readonly List<Entry> entries = new List<Entry>();
    readonly List<GameObject> slotObjects = new List<GameObject>();
    Image[] slotFrames;
    Image[] slotIcons;
    TMP_Text[] slotNames;

    GameObject window;
    TMP_Text titleText, nameText, descText, hintText, toastText;
    float toastUntil;
    int selected;
    bool open;
    FirstPersonController player;

    // ---------- автозапуск ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) { TryCreate(); }

    static void TryCreate()
    {
        if (Instance != null) return;
        if (FindObjectOfType<FirstPersonController>() == null) return;   // главное меню — инвентарь не нужен
        new GameObject("Inventory").AddComponent<InventoryUI>();
    }

    public static InventoryUI Ensure()
    {
        TryCreate();
        return Instance;
    }

    // ---------- добавить предмет ----------
    public static void AddItem(string id, string name, string description, Sprite icon, Kind kind = Kind.Item)
    {
        InventoryUI inv = Ensure();
        if (inv == null) return;
        if (!string.IsNullOrEmpty(id) && inv.entries.Exists(e => e.id == id)) return;
        inv.entries.Add(new Entry { id = id, name = name, description = description, icon = icon, kind = kind });
        inv.Toast("+ " + name + "      [I] — инвентарь");
        inv.Refresh();
    }

    public static void AddNote(string title, string text, Sprite image)
    {
        InventoryUI inv = Ensure();
        if (inv == null) return;
        string id = "note_" + title;
        if (inv.entries.Exists(e => e.id == id)) return;
        inv.entries.Add(new Entry
        {
            id = id,
            name = title,
            description = "Щёлкни или нажми Enter, чтобы перечитать.",
            icon = inv.noteIcon,
            kind = Kind.Note,
            noteTitle = title,
            noteText = text,
            noteImage = image
        });
        inv.Toast("+ " + title + "      [I] — инвентарь");
        inv.Refresh();
    }

    // Прикрепить к уже лежащему в инвентаре предмету картинку, которую можно открыть щелчком
    // (фото с мамой, медкарта)
    public static void SetView(string id, string title, Sprite image)
    {
        if (Instance == null || image == null) return;
        Entry e = Instance.entries.Find(x => x.id == id);
        if (e == null) return;
        e.noteTitle = title;
        e.noteText = "";
        e.noteImage = image;
        if (!e.description.Contains("Щёлкни")) e.description += "\nЩёлкни или нажми Enter, чтобы посмотреть.";
        Instance.Refresh();
    }

    public static void Remove(string id)
    {
        if (Instance == null) return;
        Instance.entries.RemoveAll(e => e.id == id);
        Instance.Refresh();
    }

    // ---------- создание окна ----------
    Sprite keyIcon, flashlightIcon, noteIcon, itemIcon;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        keyIcon = MakeIcon(DrawKey);
        flashlightIcon = MakeIcon(DrawFlashlight);
        noteIcon = MakeIcon(DrawNote);
        itemIcon = MakeIcon(DrawBox);
        Build();
        window.SetActive(false);
    }

    void Build()
    {
        Canvas c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 45;
        CanvasScaler sc = gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // всплывающая строка «+ предмет»
        toastText = MakeText(transform, "", 24, new Vector2(40f, 160f), new Vector2(900f, 40f), TextAlignmentOptions.Left);
        RectTransform tr = toastText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 0f);
        tr.pivot = new Vector2(0f, 0f);

        // затемнение
        window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(transform, false);
        Stretch((RectTransform)window.transform);
        window.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        float w = Columns * Slot + (Columns - 1) * Gap + 80f;
        float h = Rows * Slot + (Rows - 1) * Gap + 260f;
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(window.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.sizeDelta = new Vector2(w, h);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.07f, 0.95f);

        titleText = MakeText(pr, "ИНВЕНТАРЬ", 34, new Vector2(0f, h / 2f - 45f), new Vector2(w, 50f), TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;

        int n = Columns * Rows;
        slotFrames = new Image[n];
        slotIcons = new Image[n];
        slotNames = new TMP_Text[n];
        float gridTop = h / 2f - 90f;
        float gridLeft = -w / 2f + 40f;
        for (int i = 0; i < n; i++)
        {
            int col = i % Columns, row = i / Columns;
            Vector2 pos = new Vector2(gridLeft + col * (Slot + Gap) + Slot / 2f, gridTop - row * (Slot + Gap) - Slot / 2f);

            GameObject s = new GameObject("Slot_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            s.transform.SetParent(pr, false);
            RectTransform sr = (RectTransform)s.transform;
            sr.anchoredPosition = pos;
            sr.sizeDelta = new Vector2(Slot, Slot);
            slotFrames[i] = s.GetComponent<Image>();
            slotFrames[i].color = new Color(1f, 1f, 1f, 0.08f);
            int index = i;
            s.GetComponent<Button>().onClick.AddListener(() => OnSlotClicked(index));
            SlotHover hover = s.AddComponent<SlotHover>();
            hover.owner = this; hover.index = i;
            slotObjects.Add(s);

            GameObject ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            ic.transform.SetParent(sr, false);
            RectTransform ir = (RectTransform)ic.transform;
            ir.anchoredPosition = new Vector2(0f, 8f);
            ir.sizeDelta = new Vector2(Slot * 0.62f, Slot * 0.62f);
            slotIcons[i] = ic.GetComponent<Image>();
            slotIcons[i].preserveAspect = true;
            slotIcons[i].raycastTarget = false;

            slotNames[i] = MakeText(sr, "", 13, new Vector2(0f, -Slot / 2f + 14f), new Vector2(Slot - 6f, 24f), TextAlignmentOptions.Center);
            slotNames[i].enableWordWrapping = false;
            slotNames[i].overflowMode = TextOverflowModes.Ellipsis;
        }

        float infoY = gridTop - Rows * (Slot + Gap) - 30f;
        nameText = MakeText(pr, "", 26, new Vector2(0f, infoY), new Vector2(w - 80f, 36f), TextAlignmentOptions.Left);
        nameText.fontStyle = FontStyles.Bold;
        descText = MakeText(pr, "", 20, new Vector2(0f, infoY - 50f), new Vector2(w - 80f, 60f), TextAlignmentOptions.TopLeft);
        descText.color = new Color(0.8f, 0.8f, 0.8f);
        hintText = MakeText(pr, "I / Tab — закрыть      мышь или стрелки — выбрать      Enter / щелчок — перечитать записку", 16,
            new Vector2(0f, -h / 2f + 22f), new Vector2(w - 40f, 24f), TextAlignmentOptions.Center);
        hintText.color = new Color(0.6f, 0.6f, 0.6f);
    }

    // ---------- открыть / закрыть ----------
    void Update()
    {
        if (player == null) player = FindObjectOfType<FirstPersonController>();

        if (toastText.text != "" && Time.unscaledTime > toastUntil) toastText.text = "";

        if (open)
        {
            if (PauseMenu.IsPaused) { Close(false); return; }   // открыли паузу — инвентарь прячем
            if (player != null && !player.enabled) { Close(false); return; }   // монстр поймал — прячем
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) || Input.GetMouseButtonDown(1)) { Close(true); return; }
            int move = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow)) move = 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) move = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) move = Columns;
            if (Input.GetKeyDown(KeyCode.UpArrow)) move = -Columns;
            if (move != 0) Select(Mathf.Clamp(selected + move, 0, Mathf.Max(0, entries.Count - 1)));
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) OnSlotClicked(selected);
            return;
        }

        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (CanOpen()) Open();
        }
    }

    bool CanOpen()
    {
        if (player == null || !player.canMove) return false;            // катсцена, лифт, смерть, записка
        if (PauseMenu.IsPaused) return false;
        if (NoteUI.Instance != null && NoteUI.Instance.IsOpen) return false;
        if (CodeLock.AnyOpen) return false;
        return true;
    }

    void Open()
    {
        SyncAutomatic();
        Refresh();
        Select(0);
        open = true;
        window.SetActive(true);
        player.canMove = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close(bool giveControl)
    {
        open = false;
        window.SetActive(false);
        if (giveControl)
        {
            if (player != null) player.canMove = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        NoteUI.ClosedFrame = Time.frameCount;   // чтобы та же кнопка не нажала ничего в игре
    }

    // фонарик и ключи, подобранные до появления инвентаря
    void SyncAutomatic()
    {
        if (player == null) return;
        Flashlight fl = player.GetComponentInChildren<Flashlight>(true);
        if (fl != null && fl.hasFlashlight && !entries.Exists(e => e.id == "flashlight"))
            entries.Insert(0, new Entry { id = "flashlight", name = "Фонарик", description = "Клавиша F — включить / выключить. Монстр идёт на свет.", icon = flashlightIcon, kind = Kind.Tool });
    }

    // ---------- клетки ----------
    void Refresh()
    {
        if (slotIcons == null) return;
        int items = 0;
        foreach (Entry e in entries) if (e.kind == Kind.Item) items++;
        titleText.text = items > 0 ? "ИНВЕНТАРЬ" : "ИНВЕНТАРЬ";

        for (int i = 0; i < slotIcons.Length; i++)
        {
            bool has = i < entries.Count;
            slotIcons[i].gameObject.SetActive(has);
            slotNames[i].text = has ? entries[i].name : "";
            if (has)
            {
                Entry e = entries[i];
                slotIcons[i].sprite = e.icon != null ? e.icon : (e.kind == Kind.Key ? keyIcon : e.kind == Kind.Note ? noteIcon : itemIcon);
            }
        }
        Select(Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1)));
    }

    public void Select(int i)
    {
        selected = i;
        for (int k = 0; k < slotFrames.Length; k++)
            slotFrames[k].color = k == i && k < entries.Count ? new Color(1f, 0.85f, 0.5f, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
        if (i < entries.Count)
        {
            nameText.text = entries[i].name;
            descText.text = entries[i].description;
        }
        else
        {
            nameText.text = entries.Count == 0 ? "Пусто" : "";
            descText.text = "";
        }
    }
    void OnSlotClicked(int i)
    {
        if (i >= entries.Count) return;
        Select(i);
        Entry e = entries[i];

        // 1) записка / картинка — как раньше
        if ((e.kind == Kind.Note || e.noteImage != null) && NoteUI.Instance != null)
        {
            Close(false);
            NoteUI.Instance.Show(e.noteTitle, e.noteText, e.noteImage);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        // 2) любой предмет с моделью — осмотр в руках (крутить мышью)
        if (InspectRegistry.Has(e.id) && InspectItem.Instance != null)
        {
            Close(false);
            InspectItem.Instance.Open(InspectRegistry.Get(e.id), e.name, e.description, e.id);
            return;
        }
    }

    void Toast(string text)
    {
        toastText.text = text;
        toastUntil = Time.unscaledTime + 3.5f;
    }

    // ---------- мелочи ----------
    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static TMP_Text MakeText(Transform parent, string t, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = box;
        TextMeshProUGUI tx = go.AddComponent<TextMeshProUGUI>();
        tx.text = t;
        tx.fontSize = size;
        tx.alignment = align;
        tx.raycastTarget = false;
        return tx;
    }

    // ---------- простые иконки, нарисованные кодом ----------
    delegate void Painter(Color32[] px, int s);

    static Sprite MakeIcon(Painter paint)
    {
        const int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32[] px = new Color32[s * s];
        paint(px, s);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static void Rect(Color32[] px, int s, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(s, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(s, x1); x++) px[y * s + x] = c;
    }

    static void Circle(Color32[] px, int s, int cx, int cy, int r, int hole, Color32 c)
    {
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d <= r * r && d >= hole * hole) px[y * s + x] = c;
            }
    }

    static void DrawKey(Color32[] px, int s)
    {
        Color32 gold = new Color32(214, 176, 82, 255);
        Circle(px, s, 20, 40, 13, 6, gold);
        Rect(px, s, 30, 37, 58, 43, gold);
        Rect(px, s, 46, 28, 50, 37, gold);
        Rect(px, s, 53, 30, 57, 37, gold);
    }

    static void DrawFlashlight(Color32[] px, int s)
    {
        Color32 body = new Color32(70, 70, 78, 255), head = new Color32(110, 110, 120, 255), glow = new Color32(255, 240, 180, 255);
        Rect(px, s, 8, 26, 38, 38, body);
        Rect(px, s, 38, 22, 48, 42, head);
        Rect(px, s, 48, 24, 52, 40, glow);
        Rect(px, s, 18, 38, 24, 41, new Color32(160, 40, 40, 255));
    }

    static void DrawNote(Color32[] px, int s)
    {
        Rect(px, s, 14, 6, 50, 58, new Color32(235, 220, 170, 255));
        for (int y = 14; y < 52; y += 8) Rect(px, s, 20, y, 44, y + 2, new Color32(120, 105, 80, 255));
    }

    static void DrawBox(Color32[] px, int s)
    {
        Rect(px, s, 14, 14, 50, 50, new Color32(150, 120, 90, 255));
        Rect(px, s, 14, 30, 50, 34, new Color32(100, 80, 60, 255));
    }
}
