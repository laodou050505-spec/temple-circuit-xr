using System.IO;
using UnityEditor;
using UnityEngine;

namespace TempleCircuitEditor
{
    // Executes only when an explicit request file is present.
    [InitializeOnLoad]
    static class TempleEditorAutoRunner
    {
        static TempleEditorAutoRunner()
        {
            EditorApplication.delayCall += RunRequestedActions;
        }

        static void RunRequestedActions()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var installRequest = Path.Combine(projectRoot, "Assets/Game/.install-request");
            var picoRequest = Path.Combine(projectRoot, "Assets/Game/.pico-request");
            var audioRequest = Path.Combine(projectRoot, "Assets/Game/.audio-request");
            if (File.Exists(installRequest))
            {
                File.Delete(installRequest);
                AssetDatabase.Refresh();
                TempleGameSceneInstaller.Install();
            }
            if (File.Exists(picoRequest))
            {
                File.Delete(picoRequest);
                AssetDatabase.Refresh();
                TemplePicoProjectSetup.ConfigurePico();
                TemplePicoProjectSetup.VerifyPico();
            }
            if (File.Exists(audioRequest))
            {
                File.Delete(audioRequest);
                AssetDatabase.Refresh();
                TempleGameSceneInstaller.InstallGameAudio();
            }
        }
    }
}
