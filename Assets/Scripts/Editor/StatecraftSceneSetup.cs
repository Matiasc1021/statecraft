// ═══════════════════════════════════════════════════════════════════════
//  Statecraft — Scene setup
//  Menu: Statecraft ▶ New Skirmish Scene
// ═══════════════════════════════════════════════════════════════════════
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Statecraft.Core;

namespace Statecraft.EditorTools
{
    public static class StatecraftSceneSetup
    {
        [MenuItem("Statecraft/New Skirmish Scene", false, 0)]
        public static void NewSkirmish()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Statecraft — New Skirmish",
                "Creates an empty scene containing a single MatchBootstrap object.\n\n" +
                "Everything else — camera, lighting, map, factions, HUD — is built at\n" +
                "runtime when you press Play.\n\n" +
                "Unsaved changes in the current scene will be lost.",
                "Create", "Cancel");
            if (!ok) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Match Bootstrap");
            go.AddComponent<MatchBootstrap>();
            Selection.activeGameObject = go;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Statecraft] Skirmish scene ready. Press Play.");
        }

        [MenuItem("Statecraft/Add Bootstrap To Current Scene", false, 1)]
        public static void AddBootstrap()
        {
            if (Object.FindAnyObjectByType<MatchBootstrap>() != null)
            {
                Debug.LogWarning("[Statecraft] This scene already has a MatchBootstrap.");
                return;
            }
            var go = new GameObject("Match Bootstrap");
            go.AddComponent<MatchBootstrap>();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
#endif
