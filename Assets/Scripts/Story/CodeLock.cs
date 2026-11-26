using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Кодовый замок на двери. Вешается на кнопочную панель рядом с дверью (у неё нужен Collider).
// Игрок жмёт E — на экране появляется окно «Введите код: _ _ _».
// Цифры вводятся с клавиатуры (верхний ряд или NumPad), Backspace — стереть, Esc / ПКМ — выйти.
// Окно на экране скрипт создаёт сам — ничего в Canvas делать не надо.
public class CodeLock : MonoBehaviour, IInteractable
{
    [Tooltip("Правильный код (цифры)")]
    public string code = "713";
    [Tooltip("Дверь, которую замок отпирает. У двери: Is Locked = галочка, Required Key Id — пусто")]
    public Door door;
    [Tooltip("Открыть дверь сразу после правильного кода")]
    public bool openDoorOnSuccess = true;

    public string prompt = "Ввести код";
    public string title = "КОДОВЫЙ ЗАМОК";

    [Header("Звуки (необязательно)")]
    public AudioSource audioSource;
    public AudioClip keySound;
    public AudioClip wrongSound;
    public AudioClip rightSound;

    [Tooltip("Что произойдёт после правильного кода")]
    public UnityEvent onUnlocked;

    [Tooltip("Где на экране окно с цифрами. Y = -330 — внизу, чтобы был виден сам замок")]
    public Vector2 panelPosition = new Vector2(0f, -330f);

    public static bool AnyOpen { get; private set; }

    // Для PadlockVisual (колёсики на модели замка)
    public string CurrentInput => input;
    public bool IsUnlocked => unlocked;
    public bool IsInUse => open;

    bool unlocked, open;
    string input = "";
    float openedAt, wrongUntil;
    GameObject ui;
    TMP_Text digitsText, infoText;
    FirstPersonController player;

    public string GetPrompt()
    {
        return (unlocked || open) ? "" : prompt;
    }

    public void Interact(PlayerInteraction p)
    {
        if (unlocked || open) return;
        Open();
    }

    void Open()
    {
        if (ui == null) BuildUI();
        if (player == null) player = FindObjectOfType<FirstPersonController>();
        input = "";
        open = true; AnyOpen = true;
        openedAt = Time.unscaledTime;
        ui.SetActive(true);
        if (player != null) player.canMove = false;
        Refresh("Цифры — с клавиатуры.  Backspace — стереть.  Esc — выйти", Color.white);
    }

    void Close()
    {
        open = false; AnyOpen = false;
        if (ui != null) ui.SetActive(false);
        if (player != null) player.canMove = true;
        NoteUI.ClosedFrame = Time.frameCount; // чтобы E в этом же кадре не открыла замок снова
    }

    void Update()
    {
        if (!open) return;
        if (Time.unscaledTime - openedAt < 0.2f) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Close(); return; }
        if (Time.unscaledTime < wrongUntil) return;           // мигаем «неверно»
        if (wrongUntil > 0f) { wrongUntil = 0f; input = ""; Refresh("Попробуй ещё раз", Color.white); }

        if (Input.GetKeyDown(KeyCode.Backspace) && input.Length > 0)
        {
            input = input.Substring(0, input.Length - 1);
            Refresh(null, Color.white);
            return;
        }

        for (int d = 0; d <= 9; d++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d))
            {
                input += d.ToString();
                Play(keySound);
                Refresh(null, Color.white);
                if (input.Length >= code.Length) Check();
                break;
            }
        }
    }

    void Check()
    {
        if (input == code)
        {
            unlocked = true;
            Play(rightSound);
            Refresh("ОТКРЫТО", new Color(0.4f, 1f, 0.4f));
            if (door != null)
            {
                door.isLocked = false;
                if (door.linkedDoor != null) door.linkedDoor.isLocked = false;
                if (openDoorOnSuccess && player != null)
                {
                    door.SetOpen(true, player.transform.position);
                    if (door.linkedDoor != null) door.linkedDoor.SetOpen(true, player.transform.position);
                }
            }
            onUnlocked.Invoke();
            Invoke(nameof(Close), 0.8f);
        }
        else
        {
            Play(wrongSound);
            wrongUntil = Time.unscaledTime + 0.9f;
            Refresh("НЕВЕРНЫЙ КОД", new Color(1f, 0.35f, 0.35f));
        }
    }

    void Refresh(string info, Color color)
    {
        string s = "";
        for (int i = 0; i < code.Length; i++)
            s += (i < input.Length ? input[i].ToString() : "_") + (i < code.Length - 1 ? "  " : "");
        digitsText.text = s;
        digitsText.color = color;
        if (info != null) infoText.text = info;
    }

    void Play(AudioClip c)
    {
        if (audioSource != null && c != null) audioSource.PlayOneShot(c);
    }

    // ---- окно на экране (создаётся само) ----
    void BuildUI()
    {
        ui = new GameObject("CodeLock_UI", typeof(Canvas), typeof(CanvasScaler));
        Canvas c = ui.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 50;
        CanvasScaler sc = ui.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);
        sc.matchWidthOrHeight = 0.5f;

        GameObject bg = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(ui.transform, false);
        RectTransform br = (RectTransform)bg.transform;
        br.sizeDelta = new Vector2(620, 360);
        br.anchoredPosition = panelPosition;
        bg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.06f, 0.93f);

        MakeText(br, title, 34, new Vector2(0, 120), FontStyles.Bold);
        digitsText = MakeText(br, "", 90, new Vector2(0, 10), FontStyles.Bold);
        infoText = MakeText(br, "", 22, new Vector2(0, -120), FontStyles.Normal);
        ui.SetActive(false);
    }

    TMP_Text MakeText(RectTransform parent, string t, float size, Vector2 pos, FontStyles style)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(600, size * 1.4f);
        TextMeshProUGUI tx = go.AddComponent<TextMeshProUGUI>();
        tx.text = t; tx.fontSize = size; tx.fontStyle = style;
        tx.alignment = TextAlignmentOptions.Center;
        tx.enableWordWrapping = false;
        return tx;
    }
}
