using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Собирает интерфейс меню одной кнопкой: верхнее меню Unity → Catatonia Menu.
public static class MenuBuilder
{
    static readonly Color BgColor = new Color32(7, 7, 9, 0);
    static readonly Color DimColor = new Color32(0, 0, 0, 200);
    static readonly Color BoxColor = new Color32(18, 18, 21, 245);
    static readonly Color TextColor = new Color32(216, 212, 204, 255);
    static readonly Color MutedText = new Color32(140, 136, 130, 255);
    static readonly Color Accent = new Color32(158, 43, 37, 255);
    static readonly Color ControlBg = new Color32(34, 34, 39, 255);
    static readonly Color BtnNormal = new Color(1f, 1f, 1f, 0f);
    static readonly Color BtnHover = new Color32(110, 22, 20, 220);
    static readonly Color BtnPressed = new Color32(150, 30, 26, 255);

    static TMP_DefaultControls.Resources tmpRes;
    static DefaultControls.Resources uiRes;

    [MenuItem("Catatonia Menu/1. Build Main Menu (сцена MainMenu)")]
    public static void BuildMainMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Catatonia Menu", "Сначала сохрани эту сцену (Ctrl+S) в папку Assets/Scenes под именем MainMenu, потом нажми кнопку ещё раз.", "OK");
            return;
        }

        // Удаляем старые канвасы
        if (!ReplaceIfExists("MainMenuCanvas")) return;
        GameObject oldVideo = FindInScene("VideoCanvas");
        if (oldVideo != null) Undo.DestroyObjectImmediate(oldVideo);

        LoadResources();
        EnsureEventSystem();
        EnsureCamera();

        // --- ВИДЕО ФОН (отдельный Canvas под меню) ---
        Canvas videoCanvas = CreateCanvas("VideoCanvas", 5);
        Transform videoRoot = videoCanvas.transform;
        GameObject videoLayer = NewUI("VideoBackground", videoRoot);
        Stretch(videoLayer);
        RawImage rawImg = videoLayer.AddComponent<RawImage>();
        rawImg.uvRect = new Rect(0, 0, 1, 1);
        rawImg.raycastTarget = false;
        videoLayer.SetActive(false);

        // --- ОСНОВНОЕ МЕНЮ ---
        Canvas canvas = CreateCanvas("MainMenuCanvas", 10);
        Transform root = canvas.transform;

       

        GameObject glow = NewUI("Glow", root);
        RectTransform grt = Rt(glow);
        grt.anchorMin = new Vector2(0f, 0f); grt.anchorMax = new Vector2(1f, 0.35f);
        grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
        Image gimg = glow.AddComponent<Image>();
        gimg.color = new Color32(60, 8, 8, 70);
        gimg.raycastTarget = false;

        TextMeshProUGUI title = Label(root, "Title", "CATATONIA", 130, TextAlignmentOptions.Left, TextColor);
        Place(title.gameObject, new Vector2(0f, 1f), new Vector2(150f, -130f), new Vector2(1400f, 170f));
        title.characterSpacing = 18f;
        title.fontStyle = FontStyles.Bold;
        UIFlicker flicker = title.gameObject.AddComponent<UIFlicker>();
        flicker.target = title;

        TextMeshProUGUI subtitle = Label(root, "Subtitle", "психологический хоррор", 30, TextAlignmentOptions.Left, Accent);
        Place(subtitle.gameObject, new Vector2(0f, 1f), new Vector2(160f, -300f), new Vector2(1000f, 50f));
        subtitle.characterSpacing = 8f;

        GameObject mainPanel = NewUI("MainPanel", root);
        Place(mainPanel, new Vector2(0f, 0.5f), new Vector2(150f, -110f), new Vector2(560f, 420f));
        VerticalLayoutGroup v = mainPanel.AddComponent<VerticalLayoutGroup>();
        SetupVertical(v, 10, TextAnchor.UpperLeft, new RectOffset(0, 0, 0, 0));
        Button bContinue = MakeButton(mainPanel.transform, "Btn_Continue", "Продолжить", 560f, 66f);
        Button bNew = MakeButton(mainPanel.transform, "Btn_NewGame", "Новая игра", 560f, 66f);
        Button bSettings = MakeButton(mainPanel.transform, "Btn_Settings", "Настройки", 560f, 66f);
        Button bCredits = MakeButton(mainPanel.transform, "Btn_Credits", "Авторы", 560f, 66f);
        Button bQuit = MakeButton(mainPanel.transform, "Btn_Quit", "Выйти из игры", 560f, 66f);

        GameObject credits = NewUI("CreditsPanel", root);
        Stretch(credits);
        credits.AddComponent<Image>().color = DimColor;
        GameObject cbox = Box(credits.transform, "Box", new Vector2(1000f, 640f));
        VerticalLayoutGroup cv = cbox.AddComponent<VerticalLayoutGroup>();
        SetupVertical(cv, 16, TextAnchor.UpperCenter, new RectOffset(60, 60, 50, 50));
        TextMeshProUGUI ctitle = Label(cbox.transform, "Title", "АВТОРЫ", 48, TextAlignmentOptions.Center, TextColor);
        LE(ctitle.gameObject, 880f, 80f);
        TextMeshProUGUI ctext = Label(cbox.transform, "Text",
            "Программирование, сборка глав, интерфейс\n<color=#D8D4CC>Панасюк П.А.</color>\n\n" +
            "3D-модели, анимации, звук\n<color=#D8D4CC>Зорина А.В.</color>\n\n" +
            "Расстановка мебели, противник\n<color=#D8D4CC>Кожевников А.В.</color>\n\n" +
            "СПбГУТ, 2026", 28, TextAlignmentOptions.Center, MutedText);
        LE(ctext.gameObject, 880f, 380f);
        Button bCreditsBack = MakeButton(cbox.transform, "Btn_Back", "Назад", 300f, 60f);
        CenterButtonText(bCreditsBack);

        GameObject loading = NewUI("LoadingScreen", root);
        Stretch(loading);
        loading.AddComponent<Image>().color = Color.black;
        TextMeshProUGUI ltext = Label(loading.transform, "Text", "Загрузка...", 34, TextAlignmentOptions.BottomRight, MutedText);
        Place(ltext.gameObject, new Vector2(1f, 0f), new Vector2(-80f, 60f), new Vector2(600f, 60f));
        Rt(ltext.gameObject).pivot = new Vector2(1f, 0f);

        TextMeshProUGUI ver = Label(root, "Version", "Catatonia · СПбГУТ 2026", 22, TextAlignmentOptions.BottomRight, MutedText);
        Place(ver.gameObject, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(600f, 40f));
        Rt(ver.gameObject).pivot = new Vector2(1f, 0f);

        SettingsMenu settings = BuildSettings(root);
        ConfirmDialog confirm = BuildConfirm(root);

        // --- МУЗЫКАЛЬНЫЙ ИСТОЧНИК ---
        GameObject musicObj = NewUI("MenuMusicSource", root);
        AudioSource audioSrc = musicObj.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.loop = true;
        audioSrc.spatialBlend = 0f;
        audioSrc.volume = 0.5f;

        MainMenu menu = canvas.gameObject.AddComponent<MainMenu>();
        menu.mainPanel = mainPanel;
        menu.creditsPanel = credits;
        menu.loadingScreen = loading;
        menu.continueButton = bContinue;
        menu.newGameButton = bNew;
        menu.settingsButton = bSettings;
        menu.creditsButton = bCredits;
        menu.quitButton = bQuit;
        menu.creditsBackButton = bCreditsBack;
        menu.settings = settings;
        menu.confirm = confirm;
        menu.videoDisplay = rawImg;
        menu.backgroundAudioSource = audioSrc;

        credits.SetActive(false);
        loading.SetActive(false);

        string report = SetupBuildSettings(scene.path, menu);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvas.gameObject;
        EditorUtility.DisplayDialog("Catatonia Menu", "Главное меню собрано.\n\n" + report + "\n\nСохрани сцену: Ctrl+S.", "OK");
    }

    [MenuItem("Catatonia Menu/2. Build Pause Menu (сцена игры)")]
    public static void BuildPauseMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!ReplaceIfExists("PauseCanvas")) return;
        if (!ReplaceIfExists("BrightnessCanvas")) return;
        LoadResources();
        EnsureEventSystem();

        Canvas bc = CreateCanvas("BrightnessCanvas", 50);
        bc.GetComponent<GraphicRaycaster>().enabled = false;
        GameObject overlay = NewUI("DarkOverlay", bc.transform);
        Stretch(overlay);
        Image oimg = overlay.AddComponent<Image>();
        oimg.color = new Color(0f, 0f, 0f, 0f);
        oimg.raycastTarget = false;
        BrightnessApplier brightness = bc.gameObject.AddComponent<BrightnessApplier>();
        brightness.darkOverlay = oimg;

        Canvas canvas = CreateCanvas("PauseCanvas", 100);
        Transform root = canvas.transform;

        GameObject pausePanel = NewUI("PausePanel", root);
        Stretch(pausePanel);
        pausePanel.AddComponent<Image>().color = DimColor;
        GameObject box = Box(pausePanel.transform, "Box", new Vector2(640f, 700f));
        VerticalLayoutGroup v = box.AddComponent<VerticalLayoutGroup>();
        SetupVertical(v, 10, TextAnchor.UpperCenter, new RectOffset(40, 40, 40, 30));
        TextMeshProUGUI ptitle = Label(box.transform, "Title", "ПАУЗА", 52, TextAlignmentOptions.Center, TextColor);
        ptitle.characterSpacing = 12f;
        LE(ptitle.gameObject, 560f, 90f);
        Button bResume = MakeButton(box.transform, "Btn_Resume", "Продолжить", 560f, 62f);
        Button bSave = MakeButton(box.transform, "Btn_Save", "Сохранить прогресс", 560f, 62f);
        Button bSettings = MakeButton(box.transform, "Btn_Settings", "Настройки", 560f, 62f);
        Button bMain = MakeButton(box.transform, "Btn_MainMenu", "Главное меню", 560f, 62f);
        Button bQuit = MakeButton(box.transform, "Btn_Quit", "Выйти из игры", 560f, 62f);
        TextMeshProUGUI status = Label(box.transform, "Status", "", 22, TextAlignmentOptions.Center, MutedText);
        LE(status.gameObject, 560f, 80f);

        SettingsMenu settings = BuildSettings(root);
        ConfirmDialog confirm = BuildConfirm(root);

        TextMeshProUGUI saveText = Label(root, "SaveIndicator", "Сохранено", 26, TextAlignmentOptions.BottomRight, TextColor);
        Place(saveText.gameObject, new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(600f, 50f));
        Rt(saveText.gameObject).pivot = new Vector2(1f, 0f);
        SaveIndicator indicator = saveText.gameObject.AddComponent<SaveIndicator>();
        indicator.text = saveText;

        PauseMenu pause = canvas.gameObject.AddComponent<PauseMenu>();
        pause.pausePanel = pausePanel;
        pause.resumeButton = bResume;
        pause.saveButton = bSave;
        pause.settingsButton = bSettings;
        pause.mainMenuButton = bMain;
        pause.quitButton = bQuit;
        pause.statusText = status;
        pause.settings = settings;
        pause.confirm = confirm;

        List<MonoBehaviour> toDisable = new List<MonoBehaviour>();
        MonoBehaviour look = null;
        GameObject intro = null;
        CanvasGroup faderGroup = null;
        foreach (MonoBehaviour mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            if (n == "FirstPersonController" || n == "PlayerInteraction" || n == "Flashlight")
                toDisable.Add(mb);
            if (n == "FirstPersonController") look = mb;
            if (n == "IntroWakeUp") intro = mb.gameObject;
            if (n == "ScreenFader") faderGroup = mb.GetComponent<CanvasGroup>();
        }
        pause.disableWhilePaused = toDisable.ToArray();

        List<GameObject> blockers = new List<GameObject>();
        GameObject notePanel = FindInScene("NotePanel");
        if (notePanel != null) blockers.Add(notePanel);
        pause.blockers = blockers.ToArray();

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes.Length > 0 && scenes[0].path != scene.path)
            pause.mainMenuSceneName = Path.GetFileNameWithoutExtension(scenes[0].path);

        string playerInfo;
        if (look != null)
        {
            SettingsApplier applier = look.GetComponent<SettingsApplier>();
            if (applier == null) applier = Undo.AddComponent<SettingsApplier>(look.gameObject);
            applier.lookScript = look;
            applier.playerCamera = look.GetComponentInChildren<Camera>(true);
            EditorUtility.SetDirty(applier);

            SaveLoader loader = Object.FindObjectOfType<SaveLoader>(true);
            if (loader == null)
            {
                GameObject lgo = new GameObject("SaveLoader");
                Undo.RegisterCreatedObjectUndo(lgo, "Create SaveLoader");
                loader = lgo.AddComponent<SaveLoader>();
            }
            loader.player = look.transform;
            loader.disableOnLoad = intro != null ? new GameObject[] { intro } : new GameObject[0];
            loader.screenFader = faderGroup;
            EditorUtility.SetDirty(loader);
            playerInfo = "Игрок найден: " + look.gameObject.name;
        }
        else
        {
            playerInfo = "ВНИМАНИЕ: не нашла FirstPersonController.";
        }

        EditorUtility.SetDirty(pause);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = canvas.gameObject;
        EditorUtility.DisplayDialog("Catatonia Menu",
            "Меню паузы собрано.\n\n" + playerInfo +
            "\nВыключаются на паузе: " + toDisable.Count + " скрипта(ов)." +
            "\nNotePanel: " + (notePanel != null ? "найдена" : "НЕ найдена") +
            "\nГлавное меню: " + pause.mainMenuSceneName +
            "\n\nСохрани сцену: Ctrl+S.", "OK");
    }

    static SettingsMenu BuildSettings(Transform canvas)
    {
        GameObject root = NewUI("Settings", canvas);
        Stretch(root);
        SettingsMenu sm = root.AddComponent<SettingsMenu>();

        GameObject panel = NewUI("SettingsPanel", root.transform);
        Stretch(panel);
        panel.AddComponent<Image>().color = DimColor;

        GameObject box = Box(panel.transform, "Box", new Vector2(1240f, 1000f));
        VerticalLayoutGroup v = box.AddComponent<VerticalLayoutGroup>();
        SetupVertical(v, 8, TextAnchor.UpperCenter, new RectOffset(70, 70, 40, 40));

        TextMeshProUGUI title = Label(box.transform, "Title", "НАСТРОЙКИ", 50, TextAlignmentOptions.Center, TextColor);
        title.characterSpacing = 10f;
        LE(title.gameObject, 1100f, 80f);

        Section(box.transform, "Управление");
        sm.sensitivitySlider = SliderRow(box.transform, "Чувствительность мыши", out sm.sensitivityValue);
        sm.fovSlider = SliderRow(box.transform, "Поле зрения", out sm.fovValue);

        Section(box.transform, "Изображение");
        sm.brightnessSlider = SliderRow(box.transform, "Яркость", out sm.brightnessValue);
        sm.resolutionDropdown = DropdownRow(box.transform, "Разрешение экрана");
        sm.fullscreenToggle = ToggleRow(box.transform, "Полноэкранный режим");
        sm.vsyncToggle = ToggleRow(box.transform, "Вертикальная синхронизация");

        Section(box.transform, "Звук");
        sm.volumeSlider = SliderRow(box.transform, "Общая громкость", out sm.volumeValue);

        GameObject spacer = NewUI("Spacer", box.transform);
        LE(spacer, 1100f, 16f);

        GameObject buttons = NewUI("Buttons", box.transform);
        HorizontalLayoutGroup h = buttons.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 30f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        LE(buttons, 1100f, 64f);
        sm.resetButton = MakeButton(buttons.transform, "Btn_Reset", "Сбросить", 320f, 60f);
        sm.backButton = MakeButton(buttons.transform, "Btn_Back", "Назад", 320f, 60f);
        CenterButtonText(sm.resetButton);
        CenterButtonText(sm.backButton);

        sm.panel = panel;
        EditorUtility.SetDirty(sm);
        panel.SetActive(false);
        return sm;
    }

    static ConfirmDialog BuildConfirm(Transform canvas)
    {
        GameObject root = NewUI("Confirm", canvas);
        Stretch(root);
        ConfirmDialog cd = root.AddComponent<ConfirmDialog>();

        GameObject panel = NewUI("ConfirmPanel", root.transform);
        Stretch(panel);
        panel.AddComponent<Image>().color = DimColor;
        GameObject box = Box(panel.transform, "Box", new Vector2(820f, 360f));
        VerticalLayoutGroup v = box.AddComponent<VerticalLayoutGroup>();
        SetupVertical(v, 20, TextAnchor.MiddleCenter, new RectOffset(50, 50, 40, 40));

        TextMeshProUGUI msg = Label(box.transform, "Message", "Вы уверены?", 32, TextAlignmentOptions.Center, TextColor);
        LE(msg.gameObject, 720f, 160f);

        GameObject buttons = NewUI("Buttons", box.transform);
        HorizontalLayoutGroup h = buttons.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 40f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        LE(buttons, 720f, 64f);
        Button yes = MakeButton(buttons.transform, "Btn_Yes", "Да", 260f, 60f);
        Button no = MakeButton(buttons.transform, "Btn_No", "Нет", 260f, 60f);
        CenterButtonText(yes);
        CenterButtonText(no);

        cd.panel = panel;
        cd.messageText = msg;
        cd.yesButton = yes;
        cd.noButton = no;
        EditorUtility.SetDirty(cd);
        panel.SetActive(false);
        return cd;
    }

    static void Section(Transform parent, string text)
    {
        TextMeshProUGUI t = Label(parent, "Section_" + text, text.ToUpper(), 24, TextAlignmentOptions.BottomLeft, Accent);
        t.characterSpacing = 6f;
        LE(t.gameObject, 1100f, 46f);
    }

    static Transform Row(Transform parent, string label)
    {
        GameObject row = NewUI("Row_" + label, parent);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        LE(row, 1100f, 52f);
        TextMeshProUGUI l = Label(row.transform, "Label", label, 28, TextAlignmentOptions.Left, TextColor);
        LE(l.gameObject, 420f, 52f);
        return row.transform;
    }

    static Slider SliderRow(Transform parent, string label, out TMP_Text valueText)
    {
        Transform row = Row(parent, label);
        GameObject go = DefaultControls.CreateSlider(uiRes);
        go.name = "Slider";
        go.transform.SetParent(row, false);
        LE(go, 520f, 26f);
        SetImageColor(go.transform, "Background", ControlBg);
        SetImageColor(go.transform, "Fill Area/Fill", Accent);
        SetImageColor(go.transform, "Handle Slide Area/Handle", TextColor);
        Transform handle = go.transform.Find("Handle Slide Area/Handle");
        if (handle != null) Rt(handle.gameObject).sizeDelta = new Vector2(28f, 0f);

        TextMeshProUGUI val = Label(row, "Value", "", 26, TextAlignmentOptions.Right, MutedText);
        LE(val.gameObject, 120f, 52f);
        valueText = val;
        return go.GetComponent<Slider>();
    }

    static TMP_Dropdown DropdownRow(Transform parent, string label)
    {
        Transform row = Row(parent, label);
        GameObject go = TMP_DefaultControls.CreateDropdown(tmpRes);
        go.name = "Dropdown";
        go.transform.SetParent(row, false);
        LE(go, 520f, 46f);

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = ControlBg;
        SetTextStyle(go.transform, "Label", 24, TextColor);
        SetImageColor(go.transform, "Arrow", TextColor);

        Transform template = go.transform.Find("Template");
        if (template != null)
        {
            Rt(template.gameObject).sizeDelta = new Vector2(0f, 320f);
            SetImageColor(template, "", new Color32(26, 26, 30, 255));
            SetImageColor(template, "Scrollbar", new Color32(26, 26, 30, 255));
            SetImageColor(template, "Scrollbar/Sliding Area/Handle", MutedText);
            Transform content = template.Find("Viewport/Content");
            if (content != null) Rt(content.gameObject).sizeDelta = new Vector2(0f, 42f);
            Transform item = template.Find("Viewport/Content/Item");
            if (item != null)
            {
                Rt(item.gameObject).sizeDelta = new Vector2(0f, 42f);
                SetTextStyle(item, "Item Label", 22, TextColor);
                SetImageColor(item, "Item Background", Color.white);
                SetImageColor(item, "Item Checkmark", Accent);
                Toggle t = item.GetComponent<Toggle>();
                if (t != null)
                {
                    ColorBlock cb = t.colors;
                    cb.normalColor = new Color32(26, 26, 30, 255);
                    cb.highlightedColor = BtnHover;
                    cb.pressedColor = BtnPressed;
                    cb.selectedColor = new Color32(26, 26, 30, 255);
                    t.colors = cb;
                }
            }
        }
        return go.GetComponent<TMP_Dropdown>();
    }

    static Toggle ToggleRow(Transform parent, string label)
    {
        Transform row = Row(parent, label);
        GameObject go = DefaultControls.CreateToggle(uiRes);
        go.name = "Toggle";
        go.transform.SetParent(row, false);
        LE(go, 44f, 44f);

        Transform oldLabel = go.transform.Find("Label");
        if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);

        Transform bgT = go.transform.Find("Background");
        if (bgT != null)
        {
            RectTransform r = Rt(bgT.gameObject);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            SetImageColor(go.transform, "Background", ControlBg);
            Transform check = bgT.Find("Checkmark");
            if (check != null)
            {
                RectTransform c = Rt(check.gameObject);
                c.anchorMin = Vector2.zero; c.anchorMax = Vector2.one;
                c.offsetMin = new Vector2(6f, 6f); c.offsetMax = new Vector2(-6f, -6f);
                SetImageColor(bgT, "Checkmark", Accent);
            }
        }
        return go.GetComponent<Toggle>();
    }

    static Button MakeButton(Transform parent, string name, string text, float width, float height)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpRes);
        go.name = name;
        go.transform.SetParent(parent, false);
        Rt(go).sizeDelta = new Vector2(width, height);
        LE(go, width, height);

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = Color.white;
        Button b = go.GetComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = BtnNormal;
        cb.highlightedColor = BtnHover;
        cb.pressedColor = BtnPressed;
        cb.selectedColor = BtnNormal;
        cb.disabledColor = BtnNormal;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null)
        {
            t.text = text;
            t.fontSize = 32;
            t.color = TextColor;
            t.alignment = TextAlignmentOptions.Left;
            t.margin = new Vector4(26f, 0f, 20f, 0f);
        }
        return b;
    }

    static void CenterButtonText(Button b)
    {
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t == null) return;
        t.alignment = TextAlignmentOptions.Center;
        t.margin = Vector4.zero;
        t.fontSize = 28;
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = new Color(1f, 1f, 1f, 1f);
        ColorBlock cb = b.colors;
        cb.normalColor = ControlBg;
        cb.selectedColor = ControlBg;
        b.colors = cb;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = NewUI(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.enableWordWrapping = true;
        return t;
    }

    static GameObject Box(Transform parent, string name, Vector2 size)
    {
        GameObject go = NewUI(name, parent);
        Rt(go).sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = BoxColor;
        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color32(90, 20, 18, 160);
        o.effectDistance = new Vector2(2f, -2f);
        return go;
    }

    static void LoadResources()
    {
        Sprite standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        Sprite dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        Sprite mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");

        uiRes = new DefaultControls.Resources();
        uiRes.standard = standard; uiRes.background = background; uiRes.inputField = inputField;
        uiRes.knob = knob; uiRes.checkmark = checkmark; uiRes.dropdown = dropdown; uiRes.mask = mask;

        tmpRes = new TMP_DefaultControls.Resources();
        tmpRes.standard = standard; tmpRes.background = background; tmpRes.inputField = inputField;
        tmpRes.knob = knob; tmpRes.checkmark = checkmark; tmpRes.dropdown = dropdown; tmpRes.mask = mask;
    }

    static Canvas CreateCanvas(string name, int order)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        CanvasScaler s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
        return c;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>(true) != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    static void EnsureCamera()
    {
        Camera cam = Object.FindObjectOfType<Camera>(true);
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, "Create Camera");
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    static string SetupBuildSettings(string menuPath, MainMenu menu)
    {
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == menuPath);
        list.Insert(0, new EditorBuildSettingsScene(menuPath, true));

        string gamePath = null;
        for (int i = 1; i < list.Count; i++)
            if (list[i].enabled) { gamePath = list[i].path; break; }

        if (gamePath == null)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == menu.gameSceneName) { gamePath = p; break; }
            }
            if (gamePath != null) list.Add(new EditorBuildSettingsScene(gamePath, true));
        }

        EditorBuildSettings.scenes = list.ToArray();

        if (gamePath == null)
            return "ВНИМАНИЕ: сцена игры не найдена.";

        menu.gameSceneName = Path.GetFileNameWithoutExtension(gamePath);
        return "Build Settings: 0 — " + Path.GetFileNameWithoutExtension(menuPath) + ", 1 — " + menu.gameSceneName + ".";
    }

    static bool ReplaceIfExists(string name)
    {
        GameObject old = FindInScene(name);
        if (old == null) return true;
        if (!EditorUtility.DisplayDialog("Catatonia Menu", "В сцене уже есть " + name + ". Удалить его и собрать заново?", "Пересобрать", "Отмена"))
            return false;
        Undo.DestroyObjectImmediate(old);
        return true;
    }

    static GameObject FindInScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject rootGo in scene.GetRootGameObjects())
            foreach (Transform t in rootGo.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static RectTransform Rt(GameObject go)
    {
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(GameObject go)
    {
        RectTransform r = Rt(go);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static void Place(GameObject go, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform r = Rt(go);
        r.anchorMin = anchor; r.anchorMax = anchor;
        r.pivot = new Vector2(0f, anchor.y);
        r.anchoredPosition = position;
        r.sizeDelta = size;
    }

    static void SetupVertical(VerticalLayoutGroup v, float spacing, TextAnchor align, RectOffset padding)
    {
        v.spacing = spacing;
        v.childAlignment = align;
        v.padding = padding;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = false; v.childForceExpandHeight = false;
    }

    static void LE(GameObject go, float w, float h)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w; le.preferredHeight = h;
        le.minWidth = w; le.minHeight = h;
    }

    static void SetImageColor(Transform root, string path, Color color)
    {
        Transform t = string.IsNullOrEmpty(path) ? root : root.Find(path);
        if (t == null) return;
        Image img = t.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    static void SetTextStyle(Transform root, string path, float size, Color color)
    {
        Transform t = root.Find(path);
        if (t == null) return;
        TMP_Text txt = t.GetComponent<TMP_Text>();
        if (txt == null) return;
        txt.fontSize = size;
        txt.color = color;
    }
}