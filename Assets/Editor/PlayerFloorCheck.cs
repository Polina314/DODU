#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Меню Catatonia → Проверить пол под игроком.
// Ищет, почему игрок проваливается в начале игры, и сразу чинит:
//  1. игрок стоит ВНУТРИ коллайдера мебели (стул/стол у места, где он просыпается) —
//     физика выталкивает его вниз сквозь пол. Такой коллайдер мебели убирается.
//  2. под игроком нет пола, а есть выключенный коллайдер (например, спрятанный кубик) — включается обратно.
// Пишет в окошке, что нашёл и что сделал. Ctrl+Z отменяет.
public static class PlayerFloorCheck
{
    [MenuItem("Catatonia/Проверить пол под игроком")]
    static void Check()
    {
        CharacterController cc = Resources.FindObjectsOfTypeAll<CharacterController>()
            .FirstOrDefault(c => c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c));
        if (cc == null) { EditorUtility.DisplayDialog("Пол", "Не найден игрок (CharacterController).", "OK"); return; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Пол под игроком");
        Physics.SyncTransforms();
        var log = new System.Text.StringBuilder();
        Transform p = cc.transform;

        // --- 1. в чём застрял игрок ---
        Vector3 center = p.TransformPoint(cc.center);
        float r = cc.radius * Mathf.Max(p.lossyScale.x, p.lossyScale.z);
        float h = Mathf.Max(cc.height * p.lossyScale.y, 2f * r);
        Vector3 a = center + Vector3.up * (h / 2f - r);
        Vector3 b = center - Vector3.up * (h / 2f - r);
        Collider[] hits = Physics.OverlapCapsule(a, b, r + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        int removed = 0;
        foreach (Collider c in hits)
        {
            if (c.transform.IsChildOf(p)) continue;
            string path = Path(c.transform);
            bool building = c.transform.root.name == "Hospital" || c.transform.root.name == "Building";
            // пол под ногами касается капсулы — это нормально
            if (c.bounds.max.y <= b.y - r + 0.06f) continue;
            if (building)
            {
                log.AppendLine("• Игрок касается части больницы: " + path + " (не трогаю)");
                continue;
            }
            if (c.GetComponentInParent<IInteractable>() != null)
            {
                log.AppendLine("✗ Игрок стоит внутри коллайдера предмета " + path + " — его не убираю (по нему жмут E). " +
                               "Запусти Catatonia → Починить огромные коллайдеры предметов");
                continue;
            }
            log.AppendLine("✓ Игрок стоял внутри: " + path + " — коллайдер убран");
            Undo.DestroyObjectImmediate(c);
            removed++;
        }

        // --- 2. есть ли пол под ногами ---
        Vector3 feet = new Vector3(center.x, b.y - r, center.z);
        Physics.SyncTransforms();
        if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            log.AppendLine("✓ Пол под игроком есть: " + Path(hit.collider.transform) + " (" + hit.distance.ToString("0.00") + " м)");
        }
        else
        {
            log.AppendLine("✗ Под игроком нет пола на 3 м вниз.");
            // выключенные коллайдеры прямо под ногами — включить
            foreach (Collider c in Resources.FindObjectsOfTypeAll<Collider>())
            {
                if (!c.gameObject.scene.IsValid() || EditorUtility.IsPersistent(c) || c.enabled || c.isTrigger) continue;
                Bounds bb = BoundsOf(c);
                if (feet.x < bb.min.x || feet.x > bb.max.x || feet.z < bb.min.z || feet.z > bb.max.z) continue;
                if (bb.max.y > feet.y + 0.3f || bb.max.y < feet.y - 3f) continue;
                Undo.RecordObject(c, "Пол");
                c.enabled = true;
                MeshRenderer mr = c.GetComponent<MeshRenderer>();
                if (mr != null && !mr.enabled) { Undo.RecordObject(mr, "Пол"); mr.enabled = true; }
                log.AppendLine("✓ Включён обратно: " + Path(c.transform));
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeObject = cc.gameObject;
        if (removed == 0 && log.Length == 0) log.AppendLine("Ничего подозрительного рядом с игроком.");
        EditorUtility.DisplayDialog("Пол под игроком", log + "\nCtrl+S — сохранить, потом Play.", "OK");
        Debug.Log("Пол под игроком:\n" + log);
    }

    static Bounds BoundsOf(Collider c)
    {
        if (c is BoxCollider box)
        {
            Bounds res = new Bounds(c.transform.TransformPoint(box.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 k = box.center + Vector3.Scale(box.size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                res.Encapsulate(c.transform.TransformPoint(k));
            }
            return res;
        }
        if (c is MeshCollider mc && mc.sharedMesh != null)
        {
            Bounds mb = mc.sharedMesh.bounds;
            Bounds res = new Bounds(c.transform.TransformPoint(mb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 k = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                res.Encapsulate(c.transform.TransformPoint(k));
            }
            return res;
        }
        return new Bounds(c.transform.position, Vector3.one * 0.1f);
    }

    static string Path(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
#endif
