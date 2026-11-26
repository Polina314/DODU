using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Мерцание надписи или картинки, как у неисправной лампы (для заголовка в главном меню).
public class UIFlicker : MonoBehaviour
{
    public Graphic target;
    [Range(0f, 1f)] public float minAlpha = 0.25f;
    public float minPause = 2f;
    public float maxPause = 6f;

    float baseAlpha = 1f;

    void OnEnable()
    {
        if (target == null) target = GetComponent<Graphic>();
        if (target == null) return;
        baseAlpha = target.color.a;
        StartCoroutine(Flicker());
    }

    void OnDisable()
    {
        if (target != null) SetAlpha(baseAlpha);
    }

    IEnumerator Flicker()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(minPause, maxPause));
            int blinks = Random.Range(2, 5);
            for (int i = 0; i < blinks; i++)
            {
                SetAlpha(Random.Range(minAlpha, baseAlpha * 0.7f));
                yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.09f));
                SetAlpha(baseAlpha);
                yield return new WaitForSecondsRealtime(Random.Range(0.04f, 0.15f));
            }
        }
    }

    void SetAlpha(float a)
    {
        Color c = target.color;
        c.a = a;
        target.color = c;
    }
}
