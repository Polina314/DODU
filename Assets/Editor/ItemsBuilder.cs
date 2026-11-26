#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Меню Catatonia → Подключить предметы подруги.
// Открывает окно: слева предмет подруги, справа объект в сцене, который он «оденет».
// Объекты находятся сами по именам (Note_1, Note2, ShiftSheet, Keypad, Key_Lift, Item_2_Photo...).
// Кнопка «Подключить» для каждого отмеченного:
//   • прячет серый кубик-заглушку и ставит вместо него модель подруги (на то же место, той же стороной);
//   • подгоняет коллайдер под модель, чтобы подсказка [E] появлялась;
//   • записке/журналу ставит картинку подруги на весь экран;
//   • фото и медкарте — просмотр картинки, потом вещь засчитывается;
//   • кодовому замку — модель навесного замка с крутящимися колёсиками.
// Все скрипты и события на объектах (что дверь отпирается, задачи и т.д.) остаются как были.
// Можно запускать повторно — старая модель заменится новой. Ctrl+Z отменяет.
public class ItemsBuilder : EditorWindow
{
    const string Root = "Assets/Items";
    const string Gen = Root + "/Generated";

    enum Kind { Note, StoryView, StoryItem, Padlock, Plain }

    class Row
    {
        public string label;
        public string[] names;     // как может называться объект в сцене
        public string model;       // файл в Items/Models
        public string sprite;      // файл в Items/Sprites
        public Kind kind;
        public float size;         // ожидаемый самый длинный размер модели, м
        public string title, prompt;
        public int storyNumber;
        public string storyName, storyDesc;
        public bool enabled = true;
        public GameObject target;
    }

    List<Row> rows;
    Vector2 scroll;
    string report = "";

    [MenuItem("Catatonia/Подключить предметы подруги")]
    static void Open()
    {
        ItemsBuilder w = GetWindow<ItemsBuilder>(true, "Предметы подруги");
        w.minSize = new Vector2(720, 520);
        w.Init();
    }

    void Init()
    {
        rows = new List<Row>
        {
            new Row { label = "Записка 1 (7 таблеток)", names = new[] { "Note_1", "Note1" }, model = "Note_1.fbx", sprite = "Note_1_view.png",
                      kind = Kind.Note, size = 0.17f, title = "Записка №1", prompt = "Прочитать записку" },
            new Row { label = "Записка 2 (2 стука)", names = new[] { "Note_2", "Note2" }, model = "Note_2.fbx", sprite = "Note_2_view.png",
                      kind = Kind.Note, size = 0.17f, title = "Записка №2", prompt = "Прочитать записку" },
            new Row { label = "Записка 3 (9 секунд)", names = new[] { "Note_3", "Note3" }, model = "Note_3.fbx", sprite = "Note_3_view.png",
                      kind = Kind.Note, size = 0.17f, title = "Записка №3", prompt = "Прочитать записку" },
            new Row { label = "Журнал ординаторской", names = new[] { "ShiftSheet", "Journal" }, model = "Journal_Open.fbx", sprite = "Journal_view.png",
                      kind = Kind.Note, size = 0.30f, title = "Журнал ординаторской", prompt = "Посмотреть журнал" },
            new Row { label = "Кодовый замок (729)", names = new[] { "Keypad", "CodeLock" }, kind = Kind.Padlock, size = 0.07f },
            new Row { label = "Ключ от лифта", names = new[] { "Key_Lift" }, model = "Key_Lift.fbx", kind = Kind.Plain, size = 0.14f },
            new Row { label = "Диктофон мальчика (вещь 1)", names = new[] { "Item_1_Player", "Item_1_Dictaphone" }, model = "Dictaphone.fbx",
                      kind = Kind.StoryItem, size = 0.11f, storyNumber = 1, storyName = "старый диктофон",
                      storyDesc = "Старый диктофон. Корпус оплавлен, кассета внутри.", prompt = "Взять: старый диктофон" },
            new Row { label = "Фото с мамой (вещь 2)", names = new[] { "Item_2_Photo" }, model = "Photo_Mom.fbx", sprite = "Photo_view.png",
                      kind = Kind.StoryView, size = 0.123f, storyNumber = 2, storyName = "фото с мамой",
                      storyDesc = "Фото: мальчик с мамой. Уголок обгорел. На обороте — мамин почерк.", title = "Фото", prompt = "Взять фото" },
            new Row { label = "Медкарта (вещь 3)", names = new[] { "Item_3_MedCard" }, model = "MedCard.fbx", sprite = "MedCard_view.png",
                      kind = Kind.StoryView, size = 0.297f, storyNumber = 3, storyName = "обгоревшая медкарта",
                      storyDesc = "Медкарта пациента 313. Зимин Тимофей. Края обгорели.", title = "Медицинская карта", prompt = "Посмотреть медкарту" },
            new Row { label = "Диктофон главврача (по желанию)", names = new[] { "Dictaphone" }, model = "Dictaphone.fbx",
                      kind = Kind.Plain, size = 0.11f, enabled = false },
        };
        List<Transform> all = AllInScene();
        foreach (Row r in rows)
        {
            if (r.kind == Kind.Padlock)
            {
                CodeLock cl = Resources.FindObjectsOfTypeAll<CodeLock>().FirstOrDefault(c => c.gameObject.scene.IsValid());
                if (cl != null) { r.target = cl.gameObject; continue; }
            }
            foreach (string n in r.names)
            {
                Transform t = all.FirstOrDefault(x => x.name == n);
                if (t != null) { r.target = t.gameObject; break; }
            }
        }
    }

    void OnGUI()
    {
        if (rows == null) Init();
        EditorGUILayout.HelpBox(
            "Справа — объекты из твоей сцены, на которые встанут модели подруги. Они нашлись сами по именам.\n" +
            "Если поле пустое или там не тот объект — перетащи нужный объект из Hierarchy в поле.\n" +
            "Галочка слева — подключать этот предмет или нет. Потом жми большую кнопку внизу.", MessageType.Info);

        if (!AssetDatabase.IsValidFolder(Root))
            EditorGUILayout.HelpBox("Нет папки Assets/Items. Сначала скопируй папку Items из архива в Assets.", MessageType.Error);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Row r in rows)
        {
            EditorGUILayout.BeginHorizontal();
            r.enabled = EditorGUILayout.Toggle(r.enabled, GUILayout.Width(18));
            EditorGUILayout.LabelField(r.label, GUILayout.Width(230));
            r.target = (GameObject)EditorGUILayout.ObjectField(r.target, typeof(GameObject), true, GUILayout.Width(220));
            string status;
            if (r.target == null) status = "не найден";
            else if (r.model != null && AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + r.model) == null) status = "нет модели";
            else status = "ок";
            GUIStyle st = new GUIStyle(EditorStyles.label);
            st.normal.textColor = status == "ок" ? new Color(0.3f, 0.75f, 0.3f) : new Color(0.9f, 0.4f, 0.3f);
            EditorGUILayout.LabelField(status, st);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);
        if (GUILayout.Button("Подключить отмеченные", GUILayout.Height(40))) Build();
        if (GUILayout.Button("Найти объекты заново")) Init();

        if (!string.IsNullOrEmpty(report))
        {
            GUILayout.Space(6);
            EditorGUILayout.HelpBox(report, MessageType.None);
        }
    }

    // =====================================================================
    void Build()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Подключить предметы подруги");
        var log = new System.Text.StringBuilder();
        StoryItemsHUD hud = Resources.FindObjectsOfTypeAll<StoryItemsHUD>().FirstOrDefault(h => h.gameObject.scene.IsValid());

        foreach (Row r in rows)
        {
            if (!r.enabled) continue;
            if (r.target == null) { log.AppendLine("✗ " + r.label + ": объект в сцене не найден — перетащи его в поле."); continue; }
            try
            {
                string msg = BuildRow(r, hud);
                log.AppendLine((msg.Contains("✗") ? "✗ " : "✓ ") + r.label + " → " + r.target.name + msg);
            }
            catch (System.Exception e)
            {
                log.AppendLine("✗ " + r.label + ": ошибка " + e.Message);
                Debug.LogException(e);
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report = log.ToString() + "\nГотово. Сохрани сцену: Ctrl+S.";
        Debug.Log("Предметы подруги:\n" + report);
    }

    string BuildRow(Row r, StoryItemsHUD hud)
    {
        GameObject go = r.target;
        string extra = "";

        // ---- 1. модель ----
        GameObject modelPrefab = null;
        if (r.kind != Kind.Padlock)
        {
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + r.model);
            if (modelPrefab == null) return ": ✗ нет файла Items/Models/" + r.model;
        }
        Transform model = Dress(go, r, modelPrefab);
        if (r.kind == Kind.Padlock && model.Find("Body") != null) extra += " (временный замок из кубиков — модель подруги пустая)";

        Sprite sprite = string.IsNullOrEmpty(r.sprite) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/" + r.sprite);
        if (!string.IsNullOrEmpty(r.sprite) && sprite == null) extra += "  ⚠ картинка " + r.sprite + " не Sprite";

        // ---- 2. скрипты ----
        switch (r.kind)
        {
            case Kind.Note:
            {
                NoteItem note = go.GetComponent<NoteItem>();
                if (note == null)
                {
                    note = Undo.AddComponent<NoteItem>(go);
                    note.takeAfterReading = true;
                    note.onFirstRead = new UnityEngine.Events.UnityEvent();
                    note.onAfterClose = new UnityEngine.Events.UnityEvent();
                }
                Undo.RecordObject(note, "Записка");
                note.image = sprite;
                note.text = "";              // текст уже нарисован на картинке подруги
                note.title = r.title;
                note.prompt = r.prompt;
                EditorUtility.SetDirty(note);
                break;
            }
            case Kind.StoryItem:
            {
                InteractEvent ie = go.GetComponent<InteractEvent>();
                if (ie != null) { Undo.RecordObject(ie, "Подсказка"); ie.prompt = r.prompt; EditorUtility.SetDirty(ie); }
                SetHudItem(hud, r, null);
                break;
            }
            case Kind.StoryView:
            {
                foreach (InteractEvent ie in go.GetComponents<InteractEvent>()) Undo.DestroyObjectImmediate(ie);
                StoryItemView v = go.GetComponent<StoryItemView>();
                if (v == null) v = Undo.AddComponent<StoryItemView>(go);
                Undo.RecordObject(v, "Вещь");
                if (v.onCollected == null) v.onCollected = new UnityEngine.Events.UnityEvent();
                v.prompt = r.prompt;
                v.title = r.title;
                v.image = sprite;
                v.hud = hud;
                v.number = r.storyNumber;
                EditorUtility.SetDirty(v);
                SetHudItem(hud, r, sprite);
                break;
            }
            case Kind.Padlock:
            {
                PadlockVisual pv = go.GetComponent<PadlockVisual>();
                if (pv == null) pv = Undo.AddComponent<PadlockVisual>(go);
                Undo.RecordObject(pv, "Замок");
                pv.codeLock = go.GetComponent<CodeLock>();
                bool generated = model.Find("Body") != null;
                pv.wheelAxis = Vector3.right;
                pv.invertDirection = generated;   // у модели подруги — как в её скрипте
                pv.zeroAngle = 0f;
                EditorUtility.SetDirty(pv);
                if (pv.codeLock == null) extra += "  ⚠ на объекте нет Code Lock";
                break;
            }
        }
        if (hud == null && (r.kind == Kind.StoryItem || r.kind == Kind.StoryView)) extra += "  ⚠ нет StoryItemsHUD (сначала собери главу 4)";
        return extra;
    }

    void SetHudItem(StoryItemsHUD hud, Row r, Sprite view)
    {
        if (hud == null || hud.items == null) return;
        int i = r.storyNumber - 1;
        if (i < 0 || i >= hud.items.Length) return;
        Undo.RecordObject(hud, "Вещи парня");
        if (hud.items[i] == null) hud.items[i] = new StoryItemsHUD.Item();
        hud.items[i].name = r.storyName;
        hud.items[i].description = r.storyDesc;
        if (view != null) hud.items[i].viewImage = view;
        EditorUtility.SetDirty(hud);
    }

    // ---------------------------------------------------------------------
    // Прячет кубик и ставит модель на его место
    Transform Dress(GameObject go, Row r, GameObject modelPrefab)
    {
        Transform t = go.transform;
        Undo.RegisterFullObjectHierarchyUndo(go, "Модель");

        // старая модель от прошлого запуска — запомнить, где она стояла (вдруг ты её подвинула), и убрать
        bool reuse = false;
        Vector3 prevPos = Vector3.zero, prevScale = Vector3.one;
        Quaternion prevRot = Quaternion.identity;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            Transform c = t.GetChild(i);
            if (!c.name.StartsWith("Model_")) continue;
            reuse = true;
            prevPos = c.localPosition; prevRot = c.localRotation; prevScale = c.localScale;
            Undo.DestroyObjectImmediate(c.gameObject);
        }

        // размеры и низ заглушки (запоминаем ДО изменений)
        Vector3 oldScale = t.localScale;
        int thinAxis = MinAxis(new Vector3(Mathf.Abs(oldScale.x), Mathf.Abs(oldScale.y), Mathf.Abs(oldScale.z)));
        MeshFilter mf = go.GetComponent<MeshFilter>();
        bool hadCube = mf != null && mf.sharedMesh != null;
        // точка «низа» заглушки в мире: центр минус половина толщины вниз по её оси
        Vector3 bottomWorld = t.position;
        if (hadCube && thinAxis == 1) bottomWorld = t.TransformPoint(new Vector3(0f, -0.5f, 0f));
        Vector3 bottomLocalAfter; // посчитаем после смены масштаба

        // убрать кубик
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) Undo.DestroyObjectImmediate(mr);
        if (mf != null) Undo.DestroyObjectImmediate(mf);
        Undo.RecordObject(t, "Масштаб");
        t.localScale = Vector3.one;
        bottomLocalAfter = t.InverseTransformPoint(bottomWorld);

        // поставить модель
        GameObject inst;
        if (r.kind == Kind.Padlock) inst = MakePadlock();
        else inst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        Undo.RegisterCreatedObjectUndo(inst, "Модель");
        inst.name = "Model_" + (r.kind == Kind.Padlock ? "Padlock" : System.IO.Path.GetFileNameWithoutExtension(r.model));
        Quaternion keepRot = inst.transform.localRotation;
        Vector3 keepScale = inst.transform.localScale;
        inst.transform.SetParent(t, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = keepRot;
        inst.transform.localScale = keepScale;
        foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        Bounds b;
        if (reuse)
        {
            inst.transform.localPosition = prevPos;
            inst.transform.localRotation = prevRot;
            inst.transform.localScale = prevScale;
            b = LocalBounds(t, inst);
            return FitCollider(go, b, inst.transform);
        }

        // если модель пришла в сантиметрах/огромная — привести к нужному размеру
        b = LocalBounds(t, inst);
        float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (longest > 0.0001f && (longest > r.size * 3f || longest < r.size / 3f))
        {
            inst.transform.localScale = keepScale * (r.size / longest);
            b = LocalBounds(t, inst);
        }

        // плоские вещи: тонкая сторона модели — туда же, куда была тонкая сторона кубика
        // (записка на столе — плашмя, записка за календарём — стоя)
        if (r.kind != Kind.Padlock)
        {
            int modelThin = MinAxis(b.size);
            if (hadCube && modelThin != thinAxis)
            {
                inst.transform.localRotation = Quaternion.FromToRotation(Axis(modelThin), Axis(thinAxis)) * inst.transform.localRotation;
                b = LocalBounds(t, inst);
            }
        }

        // низ модели — на низ кубика (на стол / в ящик / к стене)
        Vector3 shift;
        if (r.kind == Kind.Padlock) shift = -b.center;            // замок — центром туда, где была панель
        else
        {
            // лежит плашмя — низ модели на низ кубика; стоит (за календарём) — центр в центр
            Vector3 anchor = b.center;
            Vector3 targetPt = Vector3.zero;
            if (thinAxis == 1) { anchor.y = b.min.y; targetPt.y = bottomLocalAfter.y; }
            shift = targetPt - anchor;
        }
        inst.transform.localPosition += shift;
        b.center += shift;
        return FitCollider(go, b, inst.transform);
    }

    // коллайдер по модели (тонкие вещи — чуть толще, чтобы легче навестись)
    static Transform FitCollider(GameObject go, Bounds b, Transform model)
    {
        BoxCollider box = go.GetComponent<BoxCollider>();
        foreach (Collider c in go.GetComponents<Collider>()) if (c != box) Undo.DestroyObjectImmediate(c);
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        Undo.RecordObject(box, "Коллайдер");
        Vector3 size = b.size;
        size.x = Mathf.Max(size.x, 0.03f); size.y = Mathf.Max(size.y, 0.03f); size.z = Mathf.Max(size.z, 0.03f);
        box.center = b.center;
        box.size = size;
        box.isTrigger = false;
        return model;
    }

    static int MinAxis(Vector3 v) { return v.x <= v.y && v.x <= v.z ? 0 : (v.y <= v.z ? 1 : 2); }
    static Vector3 Axis(int i) { return i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward); }

    // Габариты модели в координатах объекта t
    static Bounds LocalBounds(Transform t, GameObject inst)
    {
        bool any = false;
        Bounds res = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter f in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = t.InverseTransformPoint(f.transform.TransformPoint(c));
                if (!any) { res = new Bounds(p, Vector3.zero); any = true; } else res.Encapsulate(p);
            }
        }
        return res;
    }

    // ---------------------------------------------------------------------
    // Навесной замок. Если в Items/Models/CodeLock.fbx есть модель с Wheel_1..3 — берём её,
    // иначе собираем простой латунный замок из кубиков с настоящими колёсиками с цифрами.
    GameObject MakePadlock()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/CodeLock.fbx");
        if (fbx != null && fbx.GetComponentsInChildren<Transform>(true).Any(x => x.name == "Wheel_1"))
            return (GameObject)PrefabUtility.InstantiatePrefab(fbx);

        if (!AssetDatabase.IsValidFolder(Gen)) AssetDatabase.CreateFolder(Root, "Generated");
        Material brass = Mat("Lock_Brass", new Color(0.72f, 0.56f, 0.26f), 0.85f, 0.55f, null);
        Material steel = Mat("Lock_Steel", new Color(0.62f, 0.63f, 0.66f), 0.9f, 0.6f, null);
        Texture2D digits = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Lock_digits.png");
        Material wheelMat = Mat("Lock_Wheel", Color.white, 0.2f, 0.35f, digits);
        Mesh wheelMesh = WheelMesh();

        GameObject root = new GameObject("Padlock");
        // корпус 5 x 4.5 x 2.2 см, лицевая сторона смотрит на -Z (синяя стрелка назад)
        Part(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(0.05f, 0.045f, 0.022f), brass);
        // дужка
        GameObject sh = new GameObject("Shackle");
        sh.transform.SetParent(root.transform, false);
        sh.transform.localPosition = new Vector3(0f, 0.0225f, 0f);
        Part(PrimitiveType.Cylinder, "Left", sh.transform, new Vector3(-0.016f, 0.012f, 0f), Vector3.zero, new Vector3(0.007f, 0.012f, 0.007f), steel);
        Part(PrimitiveType.Cylinder, "Right", sh.transform, new Vector3(0.016f, 0.012f, 0f), Vector3.zero, new Vector3(0.007f, 0.012f, 0.007f), steel);
        Part(PrimitiveType.Cylinder, "Top", sh.transform, new Vector3(0f, 0.024f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.007f, 0.0195f, 0.007f), steel);
        // колёсики снизу спереди
        for (int i = 0; i < 3; i++)
        {
            GameObject w = new GameObject("Wheel_" + (i + 1), typeof(MeshFilter), typeof(MeshRenderer));
            w.transform.SetParent(root.transform, false);
            w.transform.localPosition = new Vector3((i - 1) * 0.0135f, -0.006f, -0.004f);
            w.GetComponent<MeshFilter>().sharedMesh = wheelMesh;
            w.GetComponent<MeshRenderer>().sharedMaterial = wheelMat;
        }
        return root;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static Material Mat(string name, Color c, float metal, float smooth, Texture2D tex)
    {
        string path = Gen + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c;
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Glossiness", smooth);
        if (tex != null) m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Колёсико: кольцо вокруг оси X, 10 цифр по кругу. При повороте 0° спереди (-Z) цифра 0.
    static Mesh WheelMesh()
    {
        string path = Gen + "/Lock_Wheel.asset";
        Mesh old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) return old;

        const float r = 0.0085f, half = 0.0055f;
        const int sub = 4;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int k = 0; k < 10; k++)
        {
            int start = v.Count;
            for (int s = 0; s <= sub; s++)
            {
                float local = (float)s / sub - 0.5f;               // -0.5..0.5 внутри цифры
                float th = (k + local) * 36f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(0f, Mathf.Sin(th), -Mathf.Cos(th));
                float vv = 0.5f + local;                          // верх цифры — вверх по кругу
                v.Add(new Vector3(-half, 0f, 0f) + dir * r); n.Add(dir); uv.Add(new Vector2(k / 10f + 0.004f, vv));
                v.Add(new Vector3(half, 0f, 0f) + dir * r);  n.Add(dir); uv.Add(new Vector2((k + 1) / 10f - 0.004f, vv));
            }
            for (int s = 0; s < sub; s++)
            {
                int a = start + s * 2;
                tri.Add(a); tri.Add(a + 2); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(a + 2); tri.Add(a + 3);
            }
        }
        // боковые крышки (тёмные — край текстуры)
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -half : half;
            Vector3 nn = side == 0 ? Vector3.left : Vector3.right;
            int c = v.Count;
            v.Add(new Vector3(x, 0f, 0f)); n.Add(nn); uv.Add(new Vector2(0.002f, 0.02f));
            for (int s = 0; s <= 40; s++)
            {
                float th = s * 9f * Mathf.Deg2Rad;
                v.Add(new Vector3(x, Mathf.Sin(th) * r, -Mathf.Cos(th) * r)); n.Add(nn); uv.Add(new Vector2(0.002f, 0.02f));
            }
            for (int s = 0; s < 40; s++)
            {
                if (side == 0) { tri.Add(c); tri.Add(c + 2 + s); tri.Add(c + 1 + s); }
                else { tri.Add(c); tri.Add(c + 1 + s); tri.Add(c + 2 + s); }
            }
        }
        Mesh m = new Mesh { name = "Lock_Wheel" };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------------------------------------------------------------------
    static List<Transform> AllInScene()
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(t => t.gameObject.scene.IsValid() && t.hideFlags == HideFlags.None && !EditorUtility.IsPersistent(t))
            .ToList();
    }
}
#endif
