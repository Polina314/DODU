#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Меню Catatonia → Починить мебель.
// Одна кнопка делает так, чтобы мебель Аси заменила наши серые кубики и работала с сюжетом:
//  1. включает выключенную мебель;
//  2. ставит коллайдеры на всю мебель (игрок и предметы больше не проходят сквозь неё);
//  3. ящики стола и дверцы шкафов открываются по E с подсказкой (скрипт FurniturePart);
//  4. кровати в палатах: фонарик засчитывает настоящие кровати (FlashlightTarget → Model);
//  5. точки для записки 1 (NotePoint) — на крышки настоящих тумбочек;
//  6. ключ от лифта — в верхний ящик настоящего стола главврача, старый ящик-кубик выключается;
//  7. папка с документами — на полку настоящего шкафа в кабинете;
//  8. прячет наши кубики-заглушки (стол, кресло, шкаф, тумбочки, кровати) там, где стоит мебель;
//  9. записки/журналы/телефон, которые оказались внутри новой мебели, ставит на её поверхность.
// Скрипты Аси, друга и сцену больницы не меняет. Можно запускать повторно. Ctrl+Z отменяет всё.
public class FurnitureFixer : EditorWindow
{
    class Piece
    {
        public GameObject root;
        public string kind;     // имя файла модели: Bed_Hospital, Nightstand, Desk_Metal, Wardrobe...
        public Bounds bounds;
        public bool used;
    }

    bool doEnable = true, doColliders = true, doParts = true, doBeds = true, doNotes = true,
         doKey = true, doDocs = true, doHide = true, doSnap = true;
    string report = "";
    Vector2 scroll;
    List<Piece> pieces;

    [MenuItem("Catatonia/Починить мебель")]
    static void Open()
    {
        FurnitureFixer w = GetWindow<FurnitureFixer>(true, "Починить мебель");
        w.minSize = new Vector2(560, 560);
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Всё отмечено — просто нажми большую кнопку. Потом Ctrl+S.\n" +
                                "Если что-то пошло не так — Ctrl+Z отменит всё сразу.", MessageType.Info);
        doEnable = EditorGUILayout.ToggleLeft("Включить выключенную мебель", doEnable);
        doColliders = EditorGUILayout.ToggleLeft("Коллайдеры на мебель (сквозь неё не пройти)", doColliders);
        doParts = EditorGUILayout.ToggleLeft("Ящики столов и дверцы шкафов открываются по E", doParts);
        doBeds = EditorGUILayout.ToggleLeft("Палаты: фонарик засчитывает настоящие кровати", doBeds);
        doNotes = EditorGUILayout.ToggleLeft("Записка 1: точки NotePoint на настоящие тумбочки", doNotes);
        doKey = EditorGUILayout.ToggleLeft("Ключ от лифта — в ящик настоящего стола главврача", doKey);
        doDocs = EditorGUILayout.ToggleLeft("Папка с документами — на полку настоящего шкафа", doDocs);
        doHide = EditorGUILayout.ToggleLeft("Спрятать наши кубики там, где стоит мебель", doHide);
        doSnap = EditorGUILayout.ToggleLeft("Предметы, утонувшие в мебели, — поставить на её поверхность", doSnap);
        GUILayout.Space(8);
        if (GUILayout.Button("Починить мебель", GUILayout.Height(42))) Run();
        if (!string.IsNullOrEmpty(report))
        {
            GUILayout.Space(6);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }

    // =====================================================================
    System.Text.StringBuilder log;
    void Say(string s) { log.AppendLine(s); }

    void Run()
    {
        log = new System.Text.StringBuilder();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Починить мебель");
        try
        {
            pieces = FindPieces();
            Say("Найдено предметов мебели: " + pieces.Count);
            if (pieces.Count == 0) Say("✗ Мебель Аси в сцене не найдена (модели из папок Furniture).");

            if (doEnable) EnableFurniture();
            foreach (Piece p in pieces) p.bounds = MeshBounds(p.root, false);
            if (doColliders) AddColliders();
            if (doParts) MakeParts();
            Physics.SyncTransforms();
            if (doBeds) Beds();
            if (doNotes) NotePoints();
            if (doKey) Key();
            Physics.SyncTransforms();
            if (doDocs) Docs();
            if (doHide) HideCubes();
            Physics.SyncTransforms();
            if (doSnap) Snap();
            NavMeshHint();
        }
        catch (System.Exception e)
        {
            Say("✗ Ошибка: " + e.Message);
            Debug.LogException(e);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        report = log + "\nГотово. Сохрани сцену: Ctrl+S.";
        Debug.Log("Починить мебель:\n" + report);
    }

    // ---------------------------------------------------------------------
    // Поиск мебели. Мебелью считается любой видимый объект сцены, КРОМЕ:
    // самой больницы (Hospital), ламп, игрока, монстров, мальчика, UI и наших сюжетных папок Chapter.
    // Неважно, из какой папки модель и как её назвали.
    static readonly string[] skipRoots = { "Hospital_Lights", "Player", "Monster", "Boy", "Canvas", "Chapter",
                                           "GameManager", "Elevator", "Building", "Main Camera", "EventSystem", "PauseCanvas",
                                           "BrightnessCanvas", "Directional Light" };

    static bool Skipped(Transform t, string meshPath)
    {
        Transform top = t.root;
        // Bed_Hospital — мебель. Сама геометрия здания импортирована из Hospital.fbx.
        if (top.name.StartsWith("Hospital") && !top.name.StartsWith("Hospital_Lights") &&
            (string.IsNullOrEmpty(meshPath) ||
             System.IO.Path.GetFileNameWithoutExtension(meshPath).Equals("Hospital", System.StringComparison.OrdinalIgnoreCase)))
            return true;
        foreach (string s in skipRoots)
            if (top.name == s || top.name.StartsWith(s + " ") || top.name.StartsWith(s + "_") || (s == "Chapter" && top.name.StartsWith("Chapter")))
                return true;
        if (t.GetComponentInParent<Canvas>(true) != null) return true;
        if (t.GetComponentInParent<UnityEngine.AI.NavMeshAgent>(true) != null) return true;
        if (t.GetComponentInParent<CharacterController>(true) != null) return true;
        if (t.GetComponentInParent<Light>(true) != null) return true;
        return false;
    }

    static string Clean(string n)
    {
        int i = n.IndexOf(" (");
        if (i > 0) n = n.Substring(0, i);
        i = n.IndexOf('.');
        if (i > 0) n = n.Substring(0, i);
        return n.Trim();
    }

    // Подходит ли предмет под вид: по имени файла модели или по имени объекта
    static bool Is(Piece p, string prefix)
    {
        return p.kind.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase) ||
               Clean(p.root.name).StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
    }

    List<Piece> FindPieces()
    {
        var dict = new Dictionary<GameObject, Piece>();
        foreach (MeshFilter mf in AllInScene<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
            if (IsPrimitive(mf)) continue;                       // наши кубики — не мебель
            string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (Skipped(mf.transform, path)) continue;
            string kind = string.IsNullOrEmpty(path) ? Clean(mf.name) : System.IO.Path.GetFileNameWithoutExtension(path);

            Transform root = null;
            GameObject inst = PrefabUtility.GetNearestPrefabInstanceRoot(mf.gameObject);
            if (inst != null && !string.IsNullOrEmpty(path)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(mf.gameObject) == path) root = inst.transform;
            if (root == null)
            {
                Transform t = mf.transform;
                Transform found = Clean(t.name).StartsWith(kind) ? t : null;
                while (t.parent != null)
                {
                    t = t.parent;
                    if (Clean(t.name).StartsWith(kind)) found = t;
                }
                root = found != null ? found : mf.transform;
            }
            if (!dict.ContainsKey(root.gameObject)) dict[root.gameObject] = new Piece { root = root.gameObject, kind = kind };
        }
        var list = dict.Values.ToList();
        list.RemoveAll(p => list.Any(o => o != p && p.root.transform.IsChildOf(o.root.transform)));
        return list;
    }

    void EnableFurniture()
    {
        int n = 0;
        foreach (Piece p in pieces)
        {
            if (p.root.activeSelf) continue;
            Undo.RecordObject(p.root, "Включить");
            p.root.SetActive(true);
            n++;
            if (n <= 25) Say("   включила: " + Path(p.root.transform));
        }
        // и выключенные родители-папки мебели (например Furniture_Placed)
        foreach (Piece p in pieces)
        {
            Transform t = p.root.transform.parent;
            while (t != null)
            {
                if (!t.gameObject.activeSelf && !t.name.StartsWith("Chapter") && !t.name.StartsWith("Room_"))
                { Undo.RecordObject(t.gameObject, "Включить"); t.gameObject.SetActive(true); n++; }
                t = t.parent;
            }
        }
        Say((n > 0 ? "✓ Включено выключенной мебели: " : "✓ Выключенной мебели нет") + (n > 0 ? n.ToString() : ""));
    }

    void AddColliders()
    {
        Say(AddSolidColliders(pieces.SelectMany(p => p.root.GetComponentsInChildren<MeshFilter>(true)).Distinct()));
    }

    [MenuItem("Catatonia/Добавить коллайдеры всей мебели")]
    static void MakeFurnitureSolid()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Добавить коллайдеры всей мебели");
        var meshes = AllInScene<MeshFilter>().Where(mf => mf.gameObject.scene == scene &&
            mf.gameObject.activeInHierarchy && mf.TryGetComponent(out MeshRenderer renderer) &&
            renderer.enabled && IsFurnitureMesh(mf)).ToArray();

        string result = AddSolidColliders(meshes);
        Undo.CollapseUndoOperations(group);
        EditorUtility.DisplayDialog("Коллайдеры мебели", result + "\nПроверено моделей: " + meshes.Length +
            "\nCtrl+S — сохранить сцену.", "OK");
    }

    [MenuItem("Catatonia/Добавить коллайдеры всей мебели", true)]
    static bool CanMakeFurnitureSolid() => !EditorApplication.isPlayingOrWillChangePlaymode;

    static bool IsFurnitureMesh(MeshFilter filter)
    {
        if (filter.sharedMesh == null) return false;
        string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
        return (path.StartsWith("Assets/Furniture/") || path.StartsWith("Assets/Furniture_for_Unity/")) &&
               System.IO.Path.GetFileNameWithoutExtension(path) != "Room_Signs";
    }

    static string AddSolidColliders(IEnumerable<MeshFilter> meshes)
    {
        int added = 0, repaired = 0, layerFix = 0;
        CharacterController player = AllInScene<CharacterController>()
            .FirstOrDefault(cc => cc.gameObject.scene == EditorSceneManager.GetActiveScene());
        int playerLayer = player != null ? player.gameObject.layer : 0;

        foreach (MeshFilter filter in meshes)
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out _)) continue;
            GameObject obj = filter.gameObject;
            Rigidbody body = obj.GetComponentInParent<Rigidbody>(true);
            Collider solid = null;
            foreach (Collider collider in obj.GetComponents<Collider>())
            {
                if (collider.isTrigger) continue;
                if (collider is MeshCollider meshCollider)
                {
                    if (body != null && !body.isKinematic && !meshCollider.convex) continue;
                    if (meshCollider.sharedMesh == null)
                    {
                        Undo.RecordObject(meshCollider, "Коллайдер мебели");
                        meshCollider.sharedMesh = filter.sharedMesh;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(meshCollider);
                        repaired++;
                    }
                }

                if (!collider.enabled)
                {
                    Undo.RecordObject(collider, "Коллайдер мебели");
                    collider.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    repaired++;
                }
                solid = collider;
                break;
            }

            if (solid == null)
            {
                if (body != null && !body.isKinematic)
                {
                    BoxCollider box = Undo.AddComponent<BoxCollider>(obj);
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
                else
                {
                    MeshCollider mesh = Undo.AddComponent<MeshCollider>(obj);
                    mesh.sharedMesh = filter.sharedMesh;
                }
                added++;
            }

            if (Physics.GetIgnoreLayerCollision(playerLayer, obj.layer) &&
                !Physics.GetIgnoreLayerCollision(playerLayer, 0))
            {
                Undo.RecordObject(obj, "Слой коллайдера мебели");
                obj.layer = 0;
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                layerFix++;
            }
        }

        return "Добавлено коллайдеров: " + added + ", восстановлено: " + repaired +
               ", переведено на слой Default: " + layerFix;
    }

    // ---------------------------------------------------------------------
    // Ящики и дверцы
    static bool IsDrawerName(string n) { return n.StartsWith("Drawer_") || n.StartsWith("Tray_"); }
    static bool IsDoorName(string n)
    {
        string l = n.ToLower();
        return l.Contains("двер") || l.Contains("door");
    }

    void MakeParts()
    {
        int drawers = 0, doors = 0;
        foreach (Piece p in pieces)
        {
            foreach (Transform t in p.root.GetComponentsInChildren<Transform>(true))
            {
                if (t == p.root.transform) continue;
                bool asyaDrawer = t.GetComponent("Drawer") != null;
                bool asyaDoor = t.GetComponent("CabinetDoor") != null;
                bool drawer = IsDrawerName(t.name) || asyaDrawer;
                bool door = !drawer && (IsDoorName(t.name) || asyaDoor);
                if (!drawer && !door) continue;
                // дверца внутри дверцы (например ручка) — пропустить
                if (t.parent != null && t.parent.GetComponent<FurniturePart>() != null) continue;

                FurniturePart fp = t.GetComponent<FurniturePart>();
                if (fp == null)
                {
                    fp = Undo.AddComponent<FurniturePart>(t.gameObject);
                    fp.onFirstOpen = new UnityEngine.Events.UnityEvent();
                }
                Undo.RecordObject(fp, "Ящик");
                fp.mode = drawer ? FurniturePart.Mode.Drawer : FurniturePart.Mode.Door;
                if (door) fp.angle = Is(p, "Toilet") ? 90f : 100f;
                EditorUtility.SetDirty(fp);
                if (drawer) drawers++; else doors++;

                // скрипты Аси — выключить, чтобы E не срабатывало дважды (сами скрипты не меняются)
                foreach (string type in new[] { "Drawer", "CabinetDoor" })
                {
                    Behaviour b = t.GetComponent(type) as Behaviour;
                    if (b != null && b.enabled) { Undo.RecordObject(b, "Выкл"); b.enabled = false; }
                }
                // двигающиеся части не должны быть Static, иначе не поедут
                foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetStaticEditorFlags(c.gameObject) != 0)
                    {
                        Undo.RecordObject(c.gameObject, "Static");
                        GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
                    }
                // коллайдер, чтобы по нему попадал взгляд
                if (t.GetComponentInChildren<Collider>(true) == null) Undo.AddComponent<BoxCollider>(t.gameObject);
            }
        }
        Say("✓ Открываются по E: ящиков " + drawers + ", дверец " + doors);
    }

    // ---------------------------------------------------------------------
    // Кровати и фонарик
    void Beds()
    {
        var targets = AllInScene<FlashlightTarget>().ToList();
        if (targets.Count == 0) { Say("• Кровати: в сцене нет Flashlight Target"); return; }
        var beds = pieces.Where(p => Is(p, "Bed")).ToList();
        var assignedModels = new HashSet<Transform>(targets.Where(ft => ft.model != null).Select(ft => ft.model));
        int ok = 0;
        foreach (FlashlightTarget ft in targets)
        {
            if (ft.model != null)
            {
                ok++;
                continue;
            }

            Vector3 refPos = ft.transform.position;
            Bounds own = MeshBounds(ft.gameObject, true);
            if (own.size != Vector3.zero) refPos = own.center;

            Collider zone = ft.insideZone;
            Piece best = Nearest(beds.Where(b => !assignedModels.Contains(b.root.transform) &&
                (zone != null ? zone.bounds.Contains(b.bounds.center) : Mathf.Abs(b.bounds.center.y - refPos.y) < 1.5f)),
                refPos, 4f);
            if (best == null)
            {
                Say("✗ " + Path(ft.transform) + ": рядом нет кровати из мебели (ближе 4 м)");
                continue;
            }

            Undo.RecordObject(ft, "Кровать");
            ft.model = best.root.transform;
            assignedModels.Add(ft.model);
            EditorUtility.SetDirty(ft);
            if (!ft.gameObject.activeSelf) { Undo.RecordObject(ft.gameObject, "Вкл"); ft.gameObject.SetActive(true); }
            // кубики старой кровати — спрятать
            foreach (MeshRenderer r in ft.GetComponentsInChildren<MeshRenderer>(true))
                if (IsPrimitive(r.GetComponent<MeshFilter>())) HideCube(r.gameObject);
            ok++;
            Say("✓ " + Path(ft.transform) + " → кровать " + best.root.name);
        }
        Say("  Кровати, на которые засчитывается фонарик: " + ok + " из " + targets.Count);
    }

    // ---------------------------------------------------------------------
    // Точки для записки 1
    void NotePoints()
    {
        RoomChecklist rc = AllInScene<RoomChecklist>().FirstOrDefault();
        if (rc == null || rc.rooms == null) { Say("• NotePoint: нет RoomChecklist"); return; }
        var stands = pieces.Where(p => Is(p, "Nightstand") || Is(p, "Tumba") || Is(p, "Тумб")).ToList();
        foreach (Piece s in stands) s.used = false;
        foreach (RoomChecklist.Room room in rc.rooms)
        {
            if (room == null) continue;
            if (room.notePoint == null) { Say("✗ " + room.name + ": поле Note Point пустое"); continue; }
            Transform np = room.notePoint;
            if (np.GetComponent<MeshFilter>() != null || np.childCount > 0)
            {
                Say("✗ " + room.name + ": в Note Point стоит не пустая точка (" + np.name + ") — пропускаю");
                continue;
            }
            // ищем от кровати этой палаты, если она есть, иначе от самой точки
            Vector3 from = np.position;
            if (np.parent != null)
            {
                FlashlightTarget ft = np.parent.GetComponentInChildren<FlashlightTarget>(true);
                if (ft != null && ft.model != null) from = MeshBounds(ft.model.gameObject, true).center;
            }
            Piece best = null; float bestD = 1e9f;
            foreach (Piece s in stands)
            {
                if (s.used) continue;
                float d = Flat(s.bounds.center - from) + Mathf.Abs(s.bounds.min.y - np.position.y) * 3f;
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best == null || bestD > 4f) { Say("✗ " + room.name + ": рядом нет тумбочки"); continue; }
            best.used = true;
            Undo.RecordObject(np, "NotePoint");
            Bounds b = best.bounds;
            np.position = new Vector3(b.center.x, b.max.y + 0.004f, b.center.z);
            Say("✓ " + room.name + ": записка ляжет на " + best.root.name);
        }
    }

    // ---------------------------------------------------------------------
    // Ключ в настоящий стол
    void Key()
    {
        OfficeDrawer od = AllInScene<OfficeDrawer>().FirstOrDefault();
        Transform key = null;
        if (od != null)
        {
            PickupItem pi = od.GetComponentInChildren<PickupItem>(true);
            if (pi != null) key = pi.transform;
        }
        if (key == null)
        {
            Transform k = AllInScene<Transform>().FirstOrDefault(t => t.name == "Key_Lift");
            if (k != null) key = k;
        }
        if (key == null) { Say("✗ Ключ Key_Lift не найден"); return; }

        Vector3 from = od != null ? od.transform.position : key.position;
        Piece desk = Nearest(pieces.Where(p => Is(p, "Desk") && p.root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Drawer_"))), from, 8f);
        if (desk == null) { Say("✗ Ключ: рядом с кабинетом нет стола Desk_Metal — ключ остался в старом ящике"); return; }

        var drawers = desk.root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Drawer_")).ToList();
        if (drawers.Count == 0) { Say("✗ Ключ: у стола " + desk.root.name + " нет ящиков Drawer_"); return; }
        Transform drawer = drawers.FirstOrDefault(t => t.name == "Drawer_R1") ?? drawers.OrderByDescending(t => MeshBounds(t.gameObject, true).max.y).First();

        Bounds db = MeshBounds(drawer.gameObject, true);
        Undo.SetTransformParent(key, drawer, "Ключ в ящик");
        Undo.RecordObject(key, "Ключ");
        key.rotation = Quaternion.Euler(0f, desk.root.transform.eulerAngles.y, 0f);
        Bounds kb = MeshBounds(key.gameObject, true);
        float bottomOffset = kb.size == Vector3.zero ? 0.01f : key.position.y - kb.min.y;
        key.position = new Vector3(db.center.x, db.min.y + 0.02f + bottomOffset, db.center.z);
        if (!key.gameObject.activeSelf) { Undo.RecordObject(key.gameObject, "Ключ"); key.gameObject.SetActive(true); }

        if (od != null && od.gameObject.activeSelf)
        {
            Undo.RecordObject(od.gameObject, "Старый ящик");
            od.gameObject.SetActive(false);
        }
        Say("✓ Ключ от лифта лежит в ящике " + drawer.name + " стола " + desk.root.name + " (старый ящик-кубик выключен)");
    }

    // ---------------------------------------------------------------------
    // Папка с документами — в шкаф
    void Docs()
    {
        NoteItem docs = AllInScene<NoteItem>().FirstOrDefault(n => n.name.StartsWith("Docs"));
        if (docs == null) { Say("• Документы: объект Docs_Folder не найден — пропускаю"); return; }
        Transform oldCube = AllInScene<Transform>().FirstOrDefault(t => t.name == "Wardrobe" && t.GetComponent<MeshFilter>() != null && IsPrimitive(t.GetComponent<MeshFilter>()));
        Vector3 from = oldCube != null ? oldCube.position : docs.transform.position;
        Piece wr = Nearest(pieces.Where(p => Is(p, "Wardrobe") || Is(p, "Шкаф")), from, 8f);
        if (wr == null) { Say("✗ Документы: рядом нет шкафа Wardrobe"); return; }

        Bounds b = wr.bounds;
        Vector3 start = new Vector3(b.center.x, b.max.y - 0.12f, b.center.z);
        float bestY = float.NegativeInfinity;
        foreach (Collider c in wr.root.GetComponentsInChildren<Collider>())
        {
            if (c.GetComponentInParent<FurniturePart>() != null) continue;   // дверцы не считаем
            if (c.Raycast(new Ray(start, Vector3.down), out RaycastHit hit, 3f) && hit.point.y > bestY) bestY = hit.point.y;
        }
        if (float.IsNegativeInfinity(bestY)) bestY = b.min.y + 0.05f;

        Transform d = docs.transform;
        Undo.RecordObject(d, "Документы");
        d.rotation = Quaternion.Euler(0f, wr.root.transform.eulerAngles.y, 0f);
        Bounds own = MeshBounds(d.gameObject, true);
        float off = own.size == Vector3.zero ? 0.02f : d.position.y - own.min.y;
        d.position = new Vector3(b.center.x, bestY + off + 0.003f, b.center.z);
        Say("✓ Папка с документами — в шкафу " + wr.root.name + " (дверцы открываются по E)");
    }

    // ---------------------------------------------------------------------
    // Наши кубики-заглушки
    static readonly Dictionary<string, string[]> kindsFor = new Dictionary<string, string[]>
    {
        { "desk",     new[] { "Desk", "Table", "Nurse_Station", "Reception_Desk", "Cabinet" } },
        { "table",    new[] { "Desk", "Table", "Nurse_Station", "Reception_Desk" } },
        { "chair",    new[] { "Chair" } },
        { "wardrobe", new[] { "Wardrobe", "Shelves", "Rack", "Cabinet" } },
        { "tumba",    new[] { "Nightstand" } },
        { "bed",      new[] { "Bed" } },
        { "panel",    new[] { "Electric_Panel" } },
    };

    void HideCubes()
    {
        int n = 0;
        foreach (MeshRenderer r in AllInScene<MeshRenderer>())
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (!IsPrimitive(mf) || !r.enabled) continue;
            Transform top = r.transform.root;
            if (!top.name.StartsWith("Chapter")) continue;
            bool pure = r.GetComponents<MonoBehaviour>().Length == 0;
            Bounds cb = MeshBounds(r.gameObject, false, true);

            if (pure)
            {
                bool replace = false;
                foreach (Piece p in pieces)
                    if (Overlap(cb, p.bounds) >= 0.35f * Vol(cb)) { replace = true; break; }
                if (!replace)
                {
                    string key = KeyFor(r.transform);
                    if (key != null)
                    {
                        string[] kinds = kindsFor[key];
                        replace = pieces.Any(p => kinds.Any(k => Is(p, k)) && Flat(p.bounds.center - cb.center) < 4.5f
                                                  && Mathf.Abs(p.bounds.min.y - cb.min.y) < 1.5f);
                    }
                }
                if (replace) { HideCube(r.gameObject); n++; }
            }
            // сюжетные кубики (фонарик, папка, телефон) больше не трогаем — у них свои коллайдеры для E
        }
        Say("✓ Спрятано кубиков-заглушек: " + n);
    }

    static string KeyFor(Transform t)
    {
        for (Transform x = t; x != null; x = x.parent)
        {
            string l = x.name.ToLower();
            if (l.StartsWith("chapter")) break;
            if (l == "panel_box") return "panel";
            foreach (string k in kindsFor.Keys) if (k != "panel" && l.Contains(k)) return k;
        }
        return null;
    }

    void HideCube(GameObject g)
    {
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        if (r != null && r.enabled) { Undo.RecordObject(r, "Кубик"); r.enabled = false; }
        foreach (Collider c in g.GetComponents<Collider>())
            if (c.enabled) { Undo.RecordObject(c, "Кубик"); c.enabled = false; }
    }

    // ---------------------------------------------------------------------
    // Предметы, которые оказались внутри мебели — на поверхность
    void Snap()
    {
        var items = new List<Component>();
        items.AddRange(AllInScene<NoteItem>());
        items.AddRange(AllInScene<InteractEvent>());
        items.AddRange(AllInScene<PickupItem>());
        items.AddRange(AllInScene<Dictaphone>());
        items.AddRange(AllInScene<StoryItemView>());
        int n = 0;
        var done = new HashSet<GameObject>();
        foreach (Component item in items)
        {
            GameObject g = item.gameObject;
            if (!done.Add(g)) continue;
            if (g.name.StartsWith("Docs") || g.name == "Key_Lift") continue;
            if (g.GetComponentInParent<FurniturePart>() != null) continue;
            if (pieces.Any(p => g.transform.IsChildOf(p.root.transform))) continue;
            Bounds ib = MeshBounds(g, true);
            if (ib.size == Vector3.zero) ib = ColliderBounds(g);
            if (ib.size == Vector3.zero) continue;
            if (Mathf.Max(ib.size.x, ib.size.y, ib.size.z) > 0.7f) continue;   // календари, планы на стенах и т.п.

            foreach (Piece p in pieces)
            {
                Bounds pb = p.bounds;
                if (ib.center.x < pb.min.x || ib.center.x > pb.max.x || ib.center.z < pb.min.z || ib.center.z > pb.max.z) continue;
                if (ib.min.y > pb.max.y + 0.05f || ib.min.y < pb.min.y + 0.1f) continue;   // не на мебели / на полу рядом
                Ray ray = new Ray(new Vector3(ib.center.x, pb.max.y + 0.3f, ib.center.z), Vector3.down);
                float topY = float.NegativeInfinity;
                foreach (Collider c in p.root.GetComponentsInChildren<Collider>())
                    if (c.enabled && c.Raycast(ray, out RaycastHit hit, 3f) && hit.point.y > topY) topY = hit.point.y;
                if (float.IsNegativeInfinity(topY)) break;
                float delta = topY + 0.002f - ib.min.y;
                if (Mathf.Abs(delta) < 0.005f || Mathf.Abs(delta) > 0.5f) break;
                Undo.RecordObject(g.transform, "Поверхность");
                g.transform.position += Vector3.up * delta;
                Say("✓ " + Path(g.transform) + " поставлен на " + p.root.name + (delta > 0 ? " (был внутри)" : " (висел)"));
                n++;
                break;
            }
        }
        if (n == 0) Say("✓ Утонувших в мебели предметов нет");
    }

    // ---------------------------------------------------------------------
    void NavMeshHint()
    {
        Component surf = AllInScene<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "NavMeshSurface");
        Say("");
        if (surf != null)
        {
            Selection.activeObject = surf.gameObject;
            Say("Последний шаг: чтобы монстр обходил мебель, справа в Inspector у выделенного объекта «" + surf.gameObject.name +
                "» в блоке NavMesh Surface нажми кнопку Bake.");
        }
    }

    // =====================================================================
    // Ручные кнопки для выделенных объектов (если что-то осталось)
    [MenuItem("Catatonia/Выделенное: сделать твёрдым (коллайдеры)")]
    static void MakeSelectedSolid()
    {
        int n = 0;
        foreach (GameObject g in Selection.gameObjects)
            foreach (MeshFilter mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                bool solid = mf.GetComponents<Collider>().Any(c => c.enabled && !c.isTrigger &&
                                                                 !(c is MeshCollider m && m.sharedMesh == null));
                if (solid) continue;
                MeshCollider mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                n++;
            }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Коллайдеры", "Добавлено коллайдеров: " + n + "\nCtrl+S — сохранить.", "OK");
    }

    [MenuItem("Catatonia/Выделенное: спрятать кубик-заглушку")]
    static void HideSelectedCubes()
    {
        int n = 0;
        foreach (GameObject g in Selection.gameObjects)
            foreach (MeshRenderer r in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!IsPrimitive(r.GetComponent<MeshFilter>())) continue;
                if (r.enabled) { Undo.RecordObject(r, "Кубик"); r.enabled = false; n++; }
                if (r.GetComponents<MonoBehaviour>().Length == 0)   // у сюжетных кубиков коллайдер оставляем — по нему жмут E
                    foreach (Collider c in r.GetComponents<Collider>())
                        if (c.enabled) { Undo.RecordObject(c, "Кубик"); c.enabled = false; }
            }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Кубики", "Спрятано кубиков: " + n + "\nCtrl+S — сохранить.", "OK");
    }

    // Чинит коллайдеры сюжетных предметов, которые старая версия этого окна растянула на пол-комнаты
    [MenuItem("Catatonia/Починить огромные коллайдеры предметов")]
    static void FixHugeColliders()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new System.Text.StringBuilder();
        foreach (BoxCollider box in AllInScene<BoxCollider>())
        {
            if (!box.transform.root.name.StartsWith("Chapter")) continue;
            if (box.GetComponent<IInteractable>() == null) continue;
            Vector3 world = Vector3.Scale(box.size, Abs(box.transform.lossyScale));
            if (Mathf.Max(world.x, world.y, world.z) < 1.5f) continue;     // нормальный размер
            Bounds mb = MeshBounds(box.gameObject, false);
            Undo.RecordObject(box, "Коллайдер");
            if (mb.size != Vector3.zero && Mathf.Max(mb.size.x, mb.size.y, mb.size.z) < 1.5f)
            {
                Bounds lb = new Bounds(box.transform.InverseTransformPoint(mb.center), Vector3.zero);
                foreach (Vector3 c in Corners(mb)) lb.Encapsulate(box.transform.InverseTransformPoint(c));
                box.center = lb.center; box.size = lb.size;
            }
            else
            {
                box.center = Vector3.zero; box.size = Vector3.one;
            }
            log.AppendLine("✓ " + Path(box.transform));
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Огромные коллайдеры",
            (log.Length > 0 ? "Исправлено:\n" + log : "Огромных коллайдеров не нашлось.") + "\nCtrl+S — сохранить.", "OK");
    }

    // =====================================================================
    // помощники
    static IEnumerable<T> AllInScene<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>().Where(c => c != null && c.gameObject.scene.IsValid() &&
                                                              !EditorUtility.IsPersistent(c) && c.hideFlags == HideFlags.None);
    }

    static bool IsPrimitive(MeshFilter mf)
    {
        if (mf == null || mf.sharedMesh == null) return false;
        string p = AssetDatabase.GetAssetPath(mf.sharedMesh);
        return p.Contains("unity default resources") || p.Contains("unity_builtin_extra");
    }

    // Габариты по мешам (работает и для выключенных объектов)
    static Bounds MeshBounds(GameObject g, bool onlyVisible, bool selfOnly = false)
    {
        bool any = false;
        Bounds res = new Bounds();
        MeshFilter[] mfs = selfOnly ? new[] { g.GetComponent<MeshFilter>() } : g.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter f in mfs)
        {
            if (f == null || f.sharedMesh == null) continue;
            if (onlyVisible)
            {
                MeshRenderer r = f.GetComponent<MeshRenderer>();
                if (r == null || !r.enabled) continue;
            }
            foreach (Vector3 c in Corners(f.sharedMesh.bounds))
            {
                Vector3 w = f.transform.TransformPoint(c);
                if (!any) { res = new Bounds(w, Vector3.zero); any = true; } else res.Encapsulate(w);
            }
        }
        return res;
    }

    static Bounds ColliderBounds(GameObject g)
    {
        BoxCollider b = g.GetComponent<BoxCollider>();
        if (b == null) return new Bounds();
        Bounds res = new Bounds(g.transform.TransformPoint(b.center), Vector3.zero);
        foreach (Vector3 c in Corners(new Bounds(b.center, b.size))) res.Encapsulate(g.transform.TransformPoint(c));
        return res;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    static float Vol(Bounds b) { return b.size.x * b.size.y * b.size.z; }

    static float Overlap(Bounds a, Bounds b)
    {
        float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float y = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
        if (x <= 0 || y <= 0 || z <= 0) return 0f;
        return x * y * z;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

    Piece Nearest(IEnumerable<Piece> list, Vector3 from, float max)
    {
        Piece best = null; float bestD = max;
        foreach (Piece p in list)
        {
            float d = Vector3.Distance(p.bounds.center, from);
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    static string Path(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
#endif
