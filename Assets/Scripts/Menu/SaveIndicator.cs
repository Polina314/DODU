using TMPro;
using UnityEngine;

// Надпись «Сохранено» в углу экрана, которая сама исчезает.
public class SaveIndicator : MonoBehaviour
{
    public TMP_Text text;
    public float showTime = 2.5f;

    float hideAt;

    void Awake()
    {
        if (text != null) text.alpha = 0f;
    }

    void OnEnable()
    {
        SaveSystem.Saved += OnSaved;
    }

    void OnDisable()
    {
        SaveSystem.Saved -= OnSaved;
    }

    void OnSaved(int chapter)
    {
        if (text == null) return;
        text.text = "Сохранено: глава " + chapter;
        text.alpha = 1f;
        hideAt = Time.unscaledTime + showTime;
    }

    void Update()
    {
        if (text == null || text.alpha <= 0f || Time.unscaledTime < hideAt) return;
        text.alpha = Mathf.MoveTowards(text.alpha, 0f, Time.unscaledDeltaTime * 1.5f);
    }
}
