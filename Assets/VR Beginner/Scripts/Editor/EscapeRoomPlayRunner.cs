using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRBeginner.Editor
{
    /// <summary>
    /// Automation script that immediately launches the 3-Level Escape Room into Play Mode in Unity.
    /// Also provides a menu shortcut (F5) and standalone builder for all 3 levels.
    /// </summary>
    [InitializeOnLoad]
    public static class EscapeRoomPlayRunner
    {
        public const string DefaultStartScenePath = "Assets/Scenes/Level1_DiningRoom.unity";
        public const string CottageScenePath = "Assets/VR Beginner/Scenes/Final/EscapeRoom.unity";
        private const string SessionKey = "EscapeRoom_HasAutoPlayed_v1";

        static EscapeRoomPlayRunner()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                LaunchGameInUnity();
            }
        }

        [MenuItem("Tools/Escape Room/Play Game Now - Level 1 (F5) _f5", false, -10)]
        public static void LaunchGameInUnity()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[EscapeRoom] Game is already running in Play Mode!");
                return;
            }

            string sceneToLoad = File.Exists(DefaultStartScenePath) ? DefaultStartScenePath : CottageScenePath;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != sceneToLoad)
            {
                if (File.Exists(sceneToLoad))
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    EditorSceneManager.OpenScene(sceneToLoad);
                }
            }

            EditorApplication.isPlaying = true;
            Debug.Log($"[EscapeRoom] Launching Escape Room Campaign in Unity Play Mode ({sceneToLoad})! Enjoy playing!");
        }

        [MenuItem("Tools/Escape Room/Re-Trigger Auto Play", false, 50)]
        public static void ResetAutoPlay()
        {
            SessionState.SetBool(SessionKey, false);
            Debug.Log("[EscapeRoom] Auto-play armed! Will start on next script compile.");
        }

        [MenuItem("Tools/Escape Room/Build Standalone Windows Game (.exe)", false, 20)]
        public static void BuildStandaloneGame()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build");
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, "EscapeRoomCampaign.exe");

            string[] scenesToBuild;
            if (File.Exists("Assets/Scenes/Level1_DiningRoom.unity"))
            {
                scenesToBuild = new string[]
                {
                    "Assets/Scenes/Level1_DiningRoom.unity",
                    "Assets/Scenes/Level2_MedievalRoom.unity",
                    "Assets/Scenes/Level3_WitchCottage.unity",
                    CottageScenePath
                };
            }
            else
            {
                scenesToBuild = new string[] { CottageScenePath };
            }

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = scenesToBuild,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opt);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[EscapeRoom] Standalone build succeeded: {exePath}");
                EditorUtility.RevealInFinder(exePath);
                System.Diagnostics.Process.Start(exePath);
            }
            else
            {
                Debug.LogError($"[EscapeRoom] Build failed with {report.summary.totalErrors} errors.");
            }
        }
    }
}
