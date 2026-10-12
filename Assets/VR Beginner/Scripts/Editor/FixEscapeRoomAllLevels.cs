using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBeginner.Gameplay;

namespace VRBeginner.Editor
{
    [InitializeOnLoad]
    public static class FixEscapeRoomAllLevels
    {
        private const string CleanPref = "EscapeRoom_AllLevels_Cleaned_v3";

        static FixEscapeRoomAllLevels()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!EditorPrefs.GetBool(CleanPref, false))
            {
                EditorPrefs.SetBool(CleanPref, true);
                CleanAndConfigureAllScenes();
            }
        }

        [MenuItem("Tools/Escape Room/Clean and Configure All 3 Levels", false, 5)]
        public static void CleanAndConfigureAllScenesMenu()
        {
            CleanAndConfigureAllScenes();
            EditorUtility.DisplayDialog("Escape Room Cleaned", "All 3 levels have been cleaned and configured! Missing scripts and conflicting cameras have been removed.", "OK");
        }

        public static void CleanAndConfigureAllScenes()
        {
            try
            {
                CleanLevel1();
                CleanLevel2();
                CleanLevel3();

                // Reopen Level 1
                if (File.Exists("Assets/Scenes/Level1_DiningRoom.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/Level1_DiningRoom.unity");
                }
                Debug.Log("[FixEscapeRoomAllLevels] Successfully cleaned and configured all 3 escape room levels!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FixEscapeRoomAllLevels] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void CleanLevel1()
        {
            string scenePath = "Assets/Scenes/Level1_DiningRoom.unity";
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Remove missing scripts from all GameObjects
            GameObject[] allGo = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }

            // Ensure EscapeRoomBootstrap exists
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }

            // Ensure GameLevelManager exists
            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixEscapeRoomAllLevels] Cleaned Level 1: Grand Dining Room");
        }

        private static void CleanLevel2()
        {
            string scenePath = "Assets/Scenes/Level2_MedievalRoom.unity";
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject[] allGo = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }

            // Ensure EscapeRoomBootstrap exists
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixEscapeRoomAllLevels] Cleaned Level 2: Medieval Chamber");
        }

        private static void CleanLevel3()
        {
            string scenePath = "Assets/Scenes/Level3_WitchCottage.unity";
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject[] allGo = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go != null)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }

            // Disable VR rigs in Level 3
            string[] vrNames = { "XR Origin (VR)", "======== VR MANAGMENT ========", "Locomotion", "Teleportation" };
            foreach (var vn in vrNames)
            {
                var vrGo = GameObject.Find(vn);
                if (vrGo != null) vrGo.SetActive(false);
            }

            // Ensure EscapeRoomBootstrap exists in Level 3
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }

            // Ensure GameLevelManager exists in Level 3
            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            // Ensure EscapeRoomManager exists in Level 3
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomManager>() == null)
            {
                GameObject mgr = new GameObject("EscapeRoomManager");
                mgr.AddComponent<EscapeRoomManager>();
            }

            // Ensure EscapeRoomHUD exists in Level 3
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomHUD>() == null)
            {
                GameObject hud = new GameObject("EscapeRoomHUD");
                hud.AddComponent<EscapeRoomHUD>();
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixEscapeRoomAllLevels] Cleaned Level 3: Witch's Cottage");
        }
    }
}
