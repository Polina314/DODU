using UnityEngine;
using UnityEngine.Events;

// Диктофон (главврача или мальчика из 313 — скрипт один на оба). Игрок смотрит на диктофон, жмёт E — кнопка PLAY нажимается
// и играет запись. Ещё раз E — стоп.
// Повесить на корневой объект Dictaphone (из Dictaphone.fbx) или Dictaphone_Chief (из Dictaphone_Chief.fbx).
// Работает с вашей системой взаимодействия (IInteractable, как у дверей и записок).
public class Dictaphone : MonoBehaviour, IInteractable
{
    [Tooltip("Аудиозапись, которая играет")]
    public AudioClip recording;
    [Tooltip("Щелчок кнопки (необязательно)")]
    public AudioClip clickSound;

    [Tooltip("Кнопка PLAY (дочерний объект Dictaphone_PlayButton). Пусто — найдётся сама")]
    public Transform playButton;
    [Tooltip("Насколько кнопка утапливается, метры")]
    public float pressDepth = 0.0015f;

    [Header("Событие, когда запись дослушана до конца (например, выдать ключ от лифта)")]
    public UnityEvent onFinished;

    AudioSource src;
    Vector3 buttonUp;
    bool playing, finishedOnce;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;          // звук идёт от самого диктофона
        src.minDistance = 0.6f; src.maxDistance = 8f;
        if (playButton == null) playButton = transform.Find("Dictaphone_PlayButton");
        if (playButton != null) buttonUp = playButton.localPosition;
        if (!GetComponentInChildren<Collider>()) gameObject.AddComponent<BoxCollider>();
    }

    public string GetPrompt() => playing ? "Остановить запись" : "Включить диктофон";

    public void Interact(PlayerInteraction player)
    {
        if (clickSound != null) src.PlayOneShot(clickSound);
        if (playing) Stop(); else Play();
    }

    void Play()
    {
        if (recording == null) return;
        src.clip = recording; src.Play(); playing = true;
        SetButton(true);
    }

    void Stop()
    {
        src.Stop(); playing = false;
        SetButton(false);
    }

    void SetButton(bool down)
    {
        if (playButton == null) return;
        // кнопка на боку корпуса: утапливается внутрь (к центру по ширине)
        Vector3 inward = new Vector3(-Mathf.Sign(buttonUp.x), 0f, 0f);
        playButton.localPosition = buttonUp + (down ? inward * pressDepth : Vector3.zero);
    }

    void Update()
    {
        if (playing && !src.isPlaying)
        {
            playing = false; SetButton(false);
            if (!finishedOnce) { finishedOnce = true; onFinished?.Invoke(); }
        }
    }
}
