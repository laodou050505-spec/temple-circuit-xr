// v3 Polish post-setup script.
// Run in batchmode:
//   lockf /tmp/en-unity-A.lock Unity -batchmode -projectPath <proj> \
//     -buildTarget Android -executeMethod TempleCircuitEditor.TempleENv3PostSetup.RunSetup -quit
//
// This script wires TempleAudioManager Inspector slots (uiClick / uiHover) to the
// Resources audio files so the Inspector shows them assigned, and saves the scene.
// Because TempleAudioManager.Start() already falls back to Resources.Load at runtime,
// the wiring here is purely for Inspector clarity and build validation.

using TempleCircuit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TempleCircuitEditor
{
    public static class TempleENv3PostSetup
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string ClickClipPath = "Assets/Game/UI/Resources/UIAudio/TempleButtonClick.mp3";
        const string HoverClipPath = "Assets/Game/UI/Resources/UIAudio/TempleButtonHover.mp3";

        /// <summary>
        /// Batchmode entry point.  Opens the main scene, wires audio, saves.
        /// </summary>
        [MenuItem("Tools/IncenseCrossing/v3 Post-Setup (Wire Audio + Save Scene)")]
        public static void RunSetup()
        {
            Debug.Log("[TempleENv3PostSetup] Opening scene: " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[TempleENv3PostSetup] Scene not found: " + ScenePath);
                EditorApplication.Exit(1);
                return;
            }

            int changes = 0;
            changes += WireAudioManager();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TempleENv3PostSetup] Done. {changes} Inspector slot(s) assigned. Scene saved.");
            // In batchmode -quit is passed externally; do not call EditorApplication.Exit here
            // so that this method also works from the Editor menu.
        }

        static int WireAudioManager()
        {
            var mgr = Object.FindAnyObjectByType<TempleAudioManager>();
            if (mgr == null)
            {
                Debug.LogWarning("[TempleENv3PostSetup] TempleAudioManager not found in scene — skipping audio wiring.");
                return 0;
            }

            int changed = 0;

            if (mgr.uiClick == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickClipPath);
                if (clip != null)
                {
                    Undo.RecordObject(mgr, "Wire uiClick");
                    mgr.uiClick = clip;
                    EditorUtility.SetDirty(mgr);
                    Debug.Log("[TempleENv3PostSetup] Assigned uiClick → " + ClickClipPath);
                    changed++;
                }
                else
                    Debug.LogWarning("[TempleENv3PostSetup] uiClick clip not found at: " + ClickClipPath);
            }

            if (mgr.uiHover == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(HoverClipPath);
                if (clip != null)
                {
                    Undo.RecordObject(mgr, "Wire uiHover");
                    mgr.uiHover = clip;
                    EditorUtility.SetDirty(mgr);
                    Debug.Log("[TempleENv3PostSetup] Assigned uiHover → " + HoverClipPath);
                    changed++;
                }
                else
                    Debug.LogWarning("[TempleENv3PostSetup] uiHover clip not found at: " + HoverClipPath);
            }

            return changed;
        }
    }
}
