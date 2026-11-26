using System.Collections;
using UnityEngine;

// Все лампы внутри папки начинают мигать на несколько секунд, потом горят как раньше.
// Вешается на любой пустой объект. В поле Group — папка с лампами (например, F2_Corridor).
// Из события: LightGroupFlicker → Flicker (float) → сколько секунд мигать.
public class LightGroupFlicker : MonoBehaviour
{
    public Transform group;
    [Tooltip("Как часто переключается (секунды)")]
    public float minInterval = 0.04f;
    public float maxInterval = 0.18f;

    public void Flicker(float seconds)
    {
        if (group != null) StartCoroutine(Run(seconds));
    }

    IEnumerator Run(float seconds)
    {
        Light[] lights = group.GetComponentsInChildren<Light>();
        bool[] was = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++) was[i] = lights[i].enabled;

        float end = Time.time + seconds;
        while (Time.time < end)
        {
            bool on = Random.value > 0.5f;
            for (int i = 0; i < lights.Length; i++) if (was[i]) lights[i].enabled = on;
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
        for (int i = 0; i < lights.Length; i++) lights[i].enabled = was[i];
    }
}
