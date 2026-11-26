using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Меню Catatonia → Собрать главу 4.
// Создаёт в открытой сцене папку Chapter4: точку возврата у лифта 3 этажа, красный туман,
// иконки вещей в углу экрана, 3 вещи-заглушки, маршрут монстра, финальную сцену у палаты 313
// и связывает всё событиями. Скрипты друга (MonsterAI и др.) НЕ меняются —
// заполняются только поля монстра в Inspector (точки маршрута и пауза).
// Всё можно отменить через Ctrl+Z.
public static class Chapter4Builder
{
    const string IconFolder = "Assets/Images/Ch4";

    [MenuItem("Catatonia/Собрать главу 4")]
    static void Build()
    {
        if (FindInScene("Chapter4") != null)
        {
            EditorUtility.DisplayDialog("Глава 4", "Папка Chapter4 уже есть на сцене. Если хочешь собрать заново — сначала удали её.", "OK");
            return;
        }

        // ---- находим то, что уже есть ----
        GameManager gm = Object.FindObjectOfType<GameManager>(true);
        ObjectiveUI objective = Object.FindObjectOfType<ObjectiveUI>(true);
        FakeElevator elevator = Object.FindObjectOfType<FakeElevator>(true);
        MonsterAI monster = Object.FindObjectOfType<MonsterAI>(true);
        Transform canvas = FindInScene("Canvas");
        Transform liftSpawn = FindInScene("F3_LiftSpawn");
        Transform room313 = FindInScene("Комната 313");
        Transform wall313 = FindInScene("заглушка для комнаты 313");

        List<string> missing = new List<string>();
        if (gm == null) missing.Add("GameManager");
        if (objective == null) missing.Add("Objective_Text (ObjectiveUI)");
        if (elevator == null) missing.Add("Elevator (FakeElevator)");
        if (canvas == null) missing.Add("Canvas");
        if (gm == null || objective == null || elevator == null || canvas == null)
        {
            EditorUtility.DisplayDialog("Глава 4", "Не нашёл на сцене: " + string.Join(", ", missing) + ".\nОткрой сцену Cacatonia и попробуй снова.", "OK");
            return;
        }
        if (monster == null) missing.Add("монстр (MonsterAI)");
        if (liftSpawn == null) missing.Add("F3_LiftSpawn");
        if (room313 == null) missing.Add("«Комната 313»");
        if (wall313 == null) missing.Add("«заглушка для комнаты 313»");

        PrepareIcons();

        Undo.SetCurrentGroupName("Собрать главу 4");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject root = new GameObject("Chapter4");
        Undo.RegisterCreatedObjectUndo(root, "Chapter4");
        Vector3 floor3 = liftSpawn != null ? liftSpawn.position : new Vector3(0f, 7.4f, 0f);

        // ---- 1. Точка возврата у лифта ----
        GameObject respawn = Create("F3_Respawn", root.transform, floor3 + Vector3.up * 1f);

        // ---- 2. Красный туман ----
        GameObject fogGo = Create("Atmosphere_F3", root.transform, floor3);
        AtmosphereFog fog = fogGo.AddComponent<AtmosphereFog>();

        // ---- 3. Иконки вещей в углу экрана ----
        Transform oldHud = canvas.Find("ItemsHUD");
        if (oldHud != null) Undo.DestroyObjectImmediate(oldHud.gameObject);
        GameObject hudGo = new GameObject("ItemsHUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(hudGo, "ItemsHUD");
        RectTransform hr = (RectTransform)hudGo.transform;
        hr.SetParent(canvas, false);
        hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
        hr.pivot = new Vector2(0f, 1f);
        hr.anchoredPosition = new Vector2(20f, -80f);
        hr.sizeDelta = new Vector2(360f, 140f);
        Transform notePanel = FindInScene("NotePanel");
        if (notePanel != null && notePanel.parent == canvas) hudGo.transform.SetSiblingIndex(notePanel.GetSiblingIndex());
        StoryItemsHUD hud = hudGo.AddComponent<StoryItemsHUD>();
        if (hud.onAllCollected == null) hud.onAllCollected = new UnityEvent();
        string[] itemNames = { "старый плеер", "фото с мамой", "обгоревшая медкарта" };
        string[] iconFiles = { "icon_player.png", "icon_photo.png", "icon_medcard.png" };
        hud.items = new StoryItemsHUD.Item[3];
        for (int i = 0; i < 3; i++)
            hud.items[i] = new StoryItemsHUD.Item { name = itemNames[i], icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/" + iconFiles[i]) };

        // ---- 4. Три вещи (кубики-заглушки у лифта — их потом надо разнести) ----
        Material marker = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Marker.mat");
        Vector3[] scales = { new Vector3(0.12f, 0.04f, 0.07f), new Vector3(0.15f, 0.01f, 0.1f), new Vector3(0.21f, 0.02f, 0.3f) };
        string[] itemObj = { "Item_1_Player", "Item_2_Photo", "Item_3_MedCard" };
        for (int i = 0; i < 3; i++)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(item, itemObj[i]);
            item.name = itemObj[i];
            item.transform.SetParent(root.transform, false);
            item.transform.position = floor3 + new Vector3(0.6f * (i + 1), 0.4f, 0f);
            item.transform.localScale = scales[i];
            if (marker != null) item.GetComponent<Renderer>().sharedMaterial = marker;
            InteractEvent ie = item.AddComponent<InteractEvent>();
            if (ie.onInteract == null) ie.onInteract = new UnityEvent();
            if (ie.onAfterDelay == null) ie.onAfterDelay = new UnityEvent();
            ie.prompt = "Взять: " + itemNames[i];
            ie.onlyOnce = true;
            ie.delaySeconds = 0f;
            UnityEventTools.AddIntPersistentListener(ie.onInteract, hud.Collect, i + 1);
            UnityEventTools.AddBoolPersistentListener(ie.onInteract, item.SetActive, false);
        }

        // ---- 5. Маршрут монстра: точки у дверей палат 3 этажа ----
        GameObject patrol = Create("Patrol_F3", root.transform, floor3);
        List<Transform> doors = AllInScene().Where(t => t.name.StartsWith("F3_Door_")).OrderBy(t => t.name).ToList();
        List<Transform> points = new List<Transform>();
        int want = Mathf.Min(6, doors.Count);
        for (int k = 0; k < want; k++)
        {
            Transform door = doors[Mathf.RoundToInt(k * (doors.Count - 1) / Mathf.Max(1f, want - 1f))];
            Renderer r = door.GetComponentInChildren<Renderer>();
            Vector3 p = r != null ? r.bounds.center : door.position;
            p.y = floor3.y;
            points.Add(Create("Point_" + (k + 1) + "  (у " + door.name + ")", patrol.transform, p).transform);
        }
        if (points.Count == 0)
            for (int k = 0; k < 4; k++) points.Add(Create("Point_" + (k + 1), patrol.transform, floor3 + new Vector3(k * 3f, 0f, 2f)).transform);

        if (monster != null)
        {
            Undo.RecordObject(monster, "Маршрут монстра");
            monster.wanderPoints = points.ToArray();
            monster.wanderWait = 3.5f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(monster);
            Undo.RecordObject(monster.gameObject, "Выключить монстра");
            monster.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(monster.gameObject);
        }

        // ---- 6. Финал у палаты 313 ----
        Vector3 endPos = wall313 != null ? BoundsCenter(wall313) : floor3 + new Vector3(10f, 0f, 0f);
        endPos.y = floor3.y;
        GameObject ending = Create("Ending313", root.transform, endPos);
        Door313Sequence seq = ending.AddComponent<Door313Sequence>();
        if (seq.onFinished == null) seq.onFinished = new UnityEvent();
        seq.roamingMonster = monster != null ? monster.gameObject : null;
        seq.doorObject = room313 != null ? room313.gameObject : null;
        seq.wallObject = wall313 != null ? wall313.gameObject : null;

        GameObject monsterFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monster/Monster.fbx");
        if (monsterFbx != null)
        {
            GameObject still = (GameObject)PrefabUtility.InstantiatePrefab(monsterFbx);
            Undo.RegisterCreatedObjectUndo(still, "Monster_Final");
            still.name = "Monster_Final";
            still.transform.SetParent(root.transform, true);
            still.transform.position = endPos;
            Animator a = still.GetComponentInChildren<Animator>();
            if (a == null) a = still.AddComponent<Animator>();
            a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Monster/Animations/MonsterAnimator.controller");
            a.applyRootMotion = false;
            still.SetActive(false);
            seq.staticMonster = still;
        }
        else missing.Add("Assets/Monster/Monster.fbx");

        GameObject zone313 = Create("Zone_313", root.transform, endPos + Vector3.up * 1f);
        BoxCollider bc = zone313.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(4f, 2.5f, 4f);
        TriggerEvent te = zone313.AddComponent<TriggerEvent>();
        if (te.onEnter == null) te.onEnter = new UnityEvent();
        if (te.onAfterDelay == null) te.onAfterDelay = new UnityEvent();
        if (te.onExit == null) te.onExit = new UnityEvent();
        te.onlyOnce = true;
        UnityEventTools.AddPersistentListener(te.onEnter, seq.Begin);
        zone313.SetActive(false);

        // все вещи собраны → в тупик к палате 313
        UnityEventTools.AddStringPersistentListener(hud.onAllCollected, objective.SetObjective, "Найти место, где была палата 313 (тупик коридора по плану эвакуации)");
        UnityEventTools.AddStringPersistentListener(hud.onAllCollected, gm.Say, "Все его вещи у меня... По плану палата 313 была в тупике коридора.");
        UnityEventTools.AddBoolPersistentListener(hud.onAllCollected, zone313.SetActive, true);

        // ---- 7. Начало главы (через 3 секунды после приезда лифта) ----
        GameObject intro = Create("Ch4_Intro", root.transform, floor3);
        DelayedEvent de = intro.AddComponent<DelayedEvent>();
        if (de.onDone == null) de.onDone = new UnityEvent();
        de.delaySeconds = 3f;
        UnityEventTools.AddStringPersistentListener(de.onDone, gm.Say, "Пахнет гарью... Здесь кто-то ходит.");
        UnityEventTools.AddStringPersistentListener(de.onDone, objective.SetObjective, "Найти 3 вещи парня");

        // ---- 8. Лифт: приезд на 3 этаж запускает главу ----
        Undo.RecordObject(elevator, "Событие лифта");
        FakeElevator.ArrivalEvent ev = null;
        if (elevator.arrivalEvents != null) ev = elevator.arrivalEvents.FirstOrDefault(e => e != null && e.floor == 2);
        if (ev == null)
        {
            ev = new FakeElevator.ArrivalEvent { floor = 2, onlyOnce = true, onArrive = new UnityEvent() };
            List<FakeElevator.ArrivalEvent> list = elevator.arrivalEvents != null ? elevator.arrivalEvents.ToList() : new List<FakeElevator.ArrivalEvent>();
            list.Add(ev);
            elevator.arrivalEvents = list.ToArray();
        }
        if (ev.onArrive == null) ev.onArrive = new UnityEvent();
        UnityEventTools.AddObjectPersistentListener<Transform>(ev.onArrive, gm.SetCheckpoint, respawn.transform);
        if (monster != null) UnityEventTools.AddBoolPersistentListener(ev.onArrive, monster.gameObject.SetActive, true);
        UnityEventTools.AddPersistentListener(ev.onArrive, fog.Enable);
        UnityEventTools.AddPersistentListener(ev.onArrive, hud.Show);
        UnityEventTools.AddPersistentListener(ev.onArrive, de.Run);
        EditorUtility.SetDirty(elevator);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        string msg = "Готово! Папка Chapter4 создана.\n\nДальше — по плану: разнести вещи, поправить точки маршрута, монстра и финал.";
        if (missing.Count > 0) msg += "\n\nНе нашёл (это надо сделать вручную): " + string.Join(", ", missing);
        EditorUtility.DisplayDialog("Глава 4", msg, "OK");
    }

    // иконки → спрайты
    static void PrepareIcons()
    {
        foreach (string f in new[] { "icon_player.png", "icon_photo.png", "icon_medcard.png" })
        {
            TextureImporter ti = AssetImporter.GetAtPath(IconFolder + "/" + f) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.SaveAndReimport();
            }
        }
    }

    static GameObject Create(string name, Transform parent, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        return go;
    }

    static Vector3 BoundsCenter(Transform t)
    {
        Renderer r = t.GetComponentInChildren<Renderer>(true);
        return r != null ? r.bounds.center : t.position;
    }

    static IEnumerable<Transform> AllInScene()
    {
        foreach (GameObject rootGo in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
                yield return t;
    }

    static Transform FindInScene(string name)
    {
        return AllInScene().FirstOrDefault(t => t.name == name);
    }
}
