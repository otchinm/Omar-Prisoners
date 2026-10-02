using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PrisonersOfOmar.EditorTools
{
    /// <summary>
    /// One-time project configuration when the project is opened: creates the (empty) main scene and puts it in
    /// the build settings, sets player settings (gamma, run in background...) and makes sure the legacy Input
    /// Manager is enabled. Everything in the game is generated at runtime by GameBootstrap, so pressing Play
    /// in any scene starts the game.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/PrisonersOfOmar/Scenes/Main.unity";
        const string Done = "PrisonersOfOmar.SetupDone.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool(Done, false)) return;
                SessionState.SetBool(Done, true);
                Run(false);
            };
        }

        [MenuItem("Prisoners of Omar/Run Project Setup", priority = 0)]
        public static void RunFromMenu() => Run(true);

        static void Run(bool verbose)
        {
            try
            {
                EnsureScene();
                EnsureBuildSettings();
                ConfigurePlayer();
                EnsureLegacyInput();
                if (verbose) Debug.Log("[Prisoners of Omar] project setup done. Press Play in " + ScenePath + ".");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Prisoners of Omar] project setup failed: " + e.Message);
            }
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var active = EditorSceneManager.GetActiveScene();
            bool canReplace = string.IsNullOrEmpty(active.path) && !active.isDirty;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, canReplace ? NewSceneMode.Single : NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!canReplace) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            Debug.Log("[Prisoners of Omar] created " + ScenePath);
        }

        static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in scenes) if (s.path != ScenePath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Base of the Second Class";
            PlayerSettings.productName = "The Prisoners of Omar";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.visibleInBackground = true;
        }

        /// <summary>The game reads the legacy Input Manager; enable it (Both) if a project template switched it off.</summary>
        static void EnsureLegacyInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 1)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.LogWarning("[Prisoners of Omar] Active Input Handling set to 'Both' (the game uses the legacy Input Manager). Restart the editor if input does not work.");
            }
        }

        [MenuItem("Prisoners of Omar/Open Main Scene", priority = 1)]
        public static void OpenMainScene()
        {
            EnsureScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Prisoners of Omar/Reimport Game Assets", priority = 20)]
        public static void ReimportAssets()
        {
            AssetDatabase.ImportAsset("Assets/PrisonersOfOmar/Resources", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        [MenuItem("Prisoners of Omar/Build Windows (64-bit)", priority = 40)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/ThePrisonersOfOmar.exe");

        [MenuItem("Prisoners of Omar/Build Linux (64-bit)", priority = 41)]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/ThePrisonersOfOmar.x86_64");

        [MenuItem("Prisoners of Omar/Build macOS", priority = 42)]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/ThePrisonersOfOmar.app");

        static void Build(BuildTarget target, string path)
        {
            Run(false);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Prisoners of Omar] build " + report.summary.result + " -> " + path);
        }
    }
}
