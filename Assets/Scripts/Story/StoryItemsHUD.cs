using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Вещи парня в углу экрана: три пустые рамки, в рамке появляется иконка, когда вещь найдена.
// Вешается на пустой объект ВНУТРИ Canvas. Рамки и иконки скрипт рисует сам при запуске.
// Из событий:
//   StoryItemsHUD → Show ()          — показать рамки (начало главы 4)
//   StoryItemsHUD → Collect (int)    — вещь найдена: 1 = первая, 2 = вторая, 3 = третья
public class StoryItemsHUD : MonoBehaviour
{
    [Serializable]
    public class Item
    {
        public string name = "вещь";
        public Sprite icon;
        [TextArea] public string description = "";
        [Tooltip("Картинка на весь экран (фото, медкарта) — её можно открыть из инвентаря. Можно пусто")]
        public Sprite viewImage;
        [HideInInspector] public bool found;
    }

    public static StoryItemsHUD Instance { get; private set; }

    public Item[] items = new Item[3];
    public float iconSize = 90f;
    public float spacing = 16f;
    public bool showOnStart = false;
    [Tooltip("Галочка — рамки висят на экране. Без галочки (по умолчанию) вещи видно только в инвентаре по клавише I")]
    public bool showOnScreen = false;
    [Tooltip("Сообщение внизу экрана, {0} — название вещи")]
    public string foundMessage = "Найдено: {0}  ({1} из {2})";

    [Tooltip("Когда найдены ВСЕ вещи")]
    public UnityEvent onAllCollected;

    GameObject panel;
    Image[] icons;
    TMP_Text[] labels;
    int count;

    public int Count => count;

    void Awake()
    {
        Instance = this;
        Build();
        panel.SetActive(showOnStart && showOnScreen);
    }

    void Build()
    {
        panel = new GameObject("Slots", typeof(RectTransform));
        RectTransform pr = (RectTransform)panel.transform;
        pr.SetParent(transform, false);
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 1f);
        pr.pivot = new Vector2(0f, 1f);
        pr.anchoredPosition = Vector2.zero;

        icons = new Image[items.Length];
        labels = new TMP_Text[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            float x = i * (iconSize + spacing);
            // рамка
            Image frame = MakeImage("Slot_" + (i + 1), pr, null, new Vector2(x, 0f), iconSize);
            frame.color = new Color(1f, 1f, 1f, 0.10f);
            // иконка (пока спрятана)
            icons[i] = MakeImage("Icon_" + (i + 1), frame.rectTransform, items[i] != null ? items[i].icon : null, Vector2.zero, iconSize);
            icons[i].rectTransform.anchorMin = icons[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icons[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icons[i].rectTransform.anchoredPosition = Vector2.zero;
            icons[i].rectTransform.sizeDelta = new Vector2(iconSize * 0.9f, iconSize * 0.9f);
            icons[i].gameObject.SetActive(false);
            // подпись под рамкой
            GameObject lg = new GameObject("Label_" + (i + 1), typeof(RectTransform));
            RectTransform lr = (RectTransform)lg.transform;
            lr.SetParent(pr, false);
            lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
            lr.pivot = new Vector2(0f, 1f);
            lr.anchoredPosition = new Vector2(x, -iconSize - 4f);
            lr.sizeDelta = new Vector2(iconSize, 50f);
            TextMeshProUGUI t = lg.AddComponent<TextMeshProUGUI>();
            t.text = "?";
            t.fontSize = 14f;
            t.enableWordWrapping = true;   // длинное название — в две строки, а не налезает на соседнее
            t.alignment = TextAlignmentOptions.Top;
            t.color = new Color(1f, 1f, 1f, 0.5f);
            t.raycastTarget = false;
            labels[i] = t;
        }
    }

    Image MakeImage(string n, Transform parent, Sprite sprite, Vector2 pos, float size)
    {
        GameObject go = new GameObject(n, typeof(RectTransform), typeof(Image));
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(size, size);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    static string Capital(string t) { return string.IsNullOrEmpty(t) ? t : char.ToUpper(t[0]) + t.Substring(1); }

    public void Show() { if (showOnScreen) panel.SetActive(true); }
    public void Hide() { panel.SetActive(false); }

    // number: 1 = первая вещь, 2 = вторая, 3 = третья
    public void Collect(int number)
    {
        int i = number - 1;
        if (i < 0 || i >= items.Length || items[i] == null || items[i].found) return;
        items[i].found = true;
        count++;
        if (showOnScreen) panel.SetActive(true);
        icons[i].gameObject.SetActive(true);
        labels[i].text = items[i].name;
        labels[i].color = Color.white;

        if (GameManager.Instance != null && !string.IsNullOrEmpty(foundMessage))
            GameManager.Instance.ShowMessage(string.Format(foundMessage, items[i].name, count, items.Length), 3f);

        string[] defaults = { "Старый диктофон. Корпус оплавлен, кассета внутри.",
                              "Фото: мальчик с мамой на море. Края обгорели.",
                              "Медкарта пациента 313. Половина страниц сгорела." };
        string desc = !string.IsNullOrEmpty(items[i].description) ? items[i].description : (i < defaults.Length ? defaults[i] : "");
        InventoryUI.AddItem("story_item_" + number, Capital(items[i].name), desc + "\nВещи парня: " + count + " из " + items.Length, items[i].icon);
        InventoryUI.SetView("story_item_" + number, Capital(items[i].name), items[i].viewImage);

        if (count >= items.Length) onAllCollected.Invoke();
    }
}
