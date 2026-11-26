using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Окно «Вы уверены?» с кнопками Да / Нет.
// Скрипт висит на объекте Confirm (всегда включён), само окно — дочерний ConfirmPanel.
public class ConfirmDialog : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text messageText;
    public Button yesButton;
    public Button noButton;

    Action onYes;

    public bool IsOpen
    {
        get { return panel != null && panel.activeSelf; }
    }

    void Awake()
    {
        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(Hide);
        panel.SetActive(false);
    }

    public void Show(string message, Action yesAction)
    {
        messageText.text = message;
        onYes = yesAction;
        panel.SetActive(true);
    }

    public void Hide()
    {
        panel.SetActive(false);
        onYes = null;
    }

    void OnYes()
    {
        Action action = onYes;
        Hide();
        if (action != null) action();
    }
}
