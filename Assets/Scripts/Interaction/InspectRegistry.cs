using System.Collections.Generic;
using UnityEngine;

public static class InspectRegistry
{
    static readonly Dictionary<string, GameObject> map = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, System.Action> finished = new Dictionary<string, System.Action>();

    // onFinished — что сделать, когда предмет в режиме осмотра "отработал" (например, диктофон дослушан)
    public static void Register(string id, GameObject obj, System.Action onFinished = null)
    {
        if (string.IsNullOrEmpty(id) || obj == null) return;
        map[id] = obj;
        if (onFinished != null) finished[id] = onFinished;
    }

    // Есть ли у предмета модель для осмотра (без предупреждений в консоли)
    public static bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return map.TryGetValue(id, out var g) && g != null;
    }

    public static GameObject Get(string id)
    {
        if (id == null) return null;
        map.TryGetValue(id, out var g);
        return g;
    }

    public static void NotifyFinished(string id)
    {
        if (id != null && finished.TryGetValue(id, out var cb) && cb != null) cb();
    }
}
