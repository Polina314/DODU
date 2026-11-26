#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Меню Catatonia → Починить NavMesh.
// Беда пакета AI Navigation 1.1: при Bake с Use Geometry = Physics Colliders он считает
// невидимые зоны-триггеры (Is Trigger) полом, и «пол для монстра» поднимается на их крышу.
// Пункт меню вешает на каждый объект с триггером компонент NavMesh Modifier с галкой Ignore From Build
// (только на сам объект, не на детей) — тогда зоны в Bake не попадают.
// После этого нужно один раз нажать Bake у Hospital (пункт меню сам его выделит).
public static class NavMeshFix
{
    [MenuItem("Catatonia/Починить NavMesh (зоны-триггеры)")]
    static void Fix()
    {
        Type modType = Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        if (modType == null)
        {
            EditorUtility.DisplayDialog("Починить NavMesh", "Не найден пакет AI Navigation (NavMeshModifier).", "OK");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Починить NavMesh");

        int n = 0;
        var triggers = Resources.FindObjectsOfTypeAll<Collider>()
            .Where(c => c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c) && c.hideFlags == HideFlags.None && c.isTrigger)
            .Select(c => c.gameObject).Distinct()
            // монстра и игрока не трогаем (они и так не попадают в Bake)
            .Where(g => g.GetComponentInParent<UnityEngine.AI.NavMeshAgent>(true) == null && g.GetComponentInParent<CharacterController>(true) == null)
            .ToList();
        foreach (GameObject g in triggers)
        {
            Component m = g.GetComponent(modType);
            if (m == null) m = Undo.AddComponent(g, modType);
            Undo.RecordObject(m, "Modifier");
            Set(m, "ignoreFromBuild", true);
            Set(m, "applyToChildren", false);
            EditorUtility.SetDirty(m);
            n++;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Component surf = Resources.FindObjectsOfTypeAll<Component>()
            .FirstOrDefault(c => c != null && c.gameObject.scene.IsValid() && c.GetType().Name == "NavMeshSurface");
        if (surf != null) Selection.activeObject = surf.gameObject;

        EditorUtility.DisplayDialog("Починить NavMesh",
            "Зон-триггеров убрано из NavMesh: " + n + ".\n\n" +
            "Теперь справа в Inspector у выделенного объекта «" + (surf != null ? surf.gameObject.name : "Hospital") +
            "» в блоке NavMesh Surface нажми Bake.\nПотом Ctrl+S.", "OK");
    }

    static void Set(Component c, string prop, bool value)
    {
        var p = c.GetType().GetProperty(prop);
        if (p != null && p.CanWrite) p.SetValue(c, value);
    }
}
#endif
