using System.Reflection;
using UnityEngine;

// Висит на игроке. Применяет чувствительность мыши и поле зрения из настроек.
// Чувствительность меняется прямо в поле FirstPersonController, поэтому сам контроллер переписывать не нужно:
// значение из инспектора считается «100 %», ползунок в настройках умножает его.
public class SettingsApplier : MonoBehaviour
{
    [Tooltip("Скрипт обзора мышью (FirstPersonController)")]
    public MonoBehaviour lookScript;

    [Tooltip("Имя поля чувствительности в этом скрипте. Если не найдено, ищется поле, в названии которого есть «sens»")]
    public string sensitivityFieldName = "mouseSensitivity";

    public Camera playerCamera;

    FieldInfo field;
    float baseSensitivity;

    void Start()
    {
        FindSensitivityField();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= Apply;
    }

    void FindSensitivityField()
    {
        if (lookScript == null) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = lookScript.GetType();

        field = type.GetField(sensitivityFieldName, flags);
        if (field == null || field.FieldType != typeof(float))
        {
            field = null;
            foreach (FieldInfo f in type.GetFields(flags))
            {
                if (f.FieldType == typeof(float) && f.Name.ToLower().Contains("sens"))
                {
                    field = f;
                    break;
                }
            }
        }

        if (field == null)
        {
            string list = "";
            foreach (FieldInfo f in type.GetFields(flags))
                if (f.FieldType == typeof(float)) list += f.Name + " ";
            Debug.LogWarning("SettingsApplier: не нашла поле чувствительности в " + type.Name +
                             ". Впиши его имя в Sensitivity Field Name. Числовые поля скрипта: " + list);
            return;
        }

        baseSensitivity = (float)field.GetValue(lookScript);
        Debug.Log("SettingsApplier: чувствительность — поле «" + field.Name + "», базовое значение " + baseSensitivity);
    }

    void Apply()
    {
        if (field != null && lookScript != null)
            field.SetValue(lookScript, baseSensitivity * GameSettings.Sensitivity);
        if (playerCamera != null)
            playerCamera.fieldOfView = GameSettings.Fov;
    }
}
