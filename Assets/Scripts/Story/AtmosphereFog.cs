using System.Collections;
using UnityEngine;

// Туман на этаже (красный на 3 этаже). Плавно появляется и исчезает.
// Вешается на любой пустой объект. Из событий: AtmosphereFog → Enable () / Disable ().
public class AtmosphereFog : MonoBehaviour
{
    public Color color = new Color(0.32f, 0.05f, 0.04f);
    [Tooltip("Густота тумана. 0.03 — лёгкий, 0.08 — густой")]
    public float density = 0.06f;
    [Tooltip("За сколько секунд туман появляется")]
    public float fadeTime = 3f;

    public void Enable()
    {
        StopAllCoroutines();
        StartCoroutine(Fade(density, true));
    }

    public void Disable()
    {
        StopAllCoroutines();
        StartCoroutine(Fade(0f, false));
    }

    IEnumerator Fade(float target, bool keepOn)
    {
        float start = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = color;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            RenderSettings.fogDensity = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        RenderSettings.fogDensity = target;
        if (!keepOn) RenderSettings.fog = false;
    }
}
