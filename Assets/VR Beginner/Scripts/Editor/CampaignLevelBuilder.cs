using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBeginner.Gameplay;

namespace VRBeginner.Editor
{
    /// <summary>
    /// Editor automation tool that builds and configures all 3 escape room levels:
    /// Level 1: The Grand Dining Room (EASY)
    /// Level 2: The Modern Living Room (MEDIUM)
    /// Level 3: The Witch's Alchemical Cottage (HARD)
    /// Automatically sets up colliders, lights, player spawn, puzzle keys, exit doors,
    /// and registers all scenes into EditorBuildSettings.
    /// </summary>
    [InitializeOnLoad]
    public static class CampaignLevelBuilder
    {
        private const string BuildDonePref = "EscapeRoom_CampaignLevels_Built_v4";

        public const string Level1ScenePath = "Assets/Scenes/Level1_DiningRoom.unity";
        public const string Level2ScenePath = "Assets/Scenes/Level2_MedievalRoom.unity";
        public const string Level3ScenePath = "Assets/Scenes/Level3_WitchCottage.unity";

        public const string SourceDiningRoomPath = "Assets/diningRoom/diningRoom/scene/deningRoomScene.unity";
        public const string SourceCottagePath = "Assets/VR Beginner/Scenes/Final/EscapeRoom.unity";

        static CampaignLevelBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!EditorPrefs.GetBool(BuildDonePref, false) || !File.Exists(Level1ScenePath) || !File.Exists(Level2ScenePath))
            {
                EditorPrefs.SetBool(BuildDonePref, true);
                BuildAllLevels(false);
            }
        }

        [MenuItem("Tools/Escape Room/Build All 3 Levels (Easy, Medium, Hard)", false, 0)]
        public static void BuildAllLevelsMenu()
        {
            BuildAllLevels(true);
        }

        public static void BuildAllLevels(bool showDialog = true)
        {
            try
            {
                EnsureScenesFolder();

                // 1. Build Level 1 (Dining Room - Easy)
                BuildLevel1();

                // 2. Build Level 2 (Modern Living Room - Medium)
                BuildLevel2();

                // 3. Build Level 3 (Witch Cottage - Hard)
                BuildLevel3();

                // 4. Update EditorBuildSettings
                UpdateBuildSettings();

                // 5. Open Level 1 as the default active scene
                EditorSceneManager.OpenScene(Level1ScenePath);

                Debug.Log("[CampaignLevelBuilder] Successfully built and configured all 3 escape room levels!");

                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "3-Level Escape Room Campaign Ready!",
                        "All 3 Levels have been created and configured:\n\n" +
                        "• Level 1: The Grand Dining Room (EASY)\n" +
                        "• Level 2: The Modern Living Room (MEDIUM)\n" +
                        "• Level 3: The Witch's Cottage (HARD)\n\n" +
                        "Each room now features interactive keys, unlocking doors, on-screen movement/jump buttons, and persistent menu!\n\n" +
                        "Hit PLAY to start the campaign!",
                        "Let's Play!"
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CampaignLevelBuilder] Error building levels: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static void BuildLevel1()
        {
            Debug.Log("[CampaignLevelBuilder] Building Level 1: Grand Dining Room (EASY)...");

            string sourcePath = File.Exists("Assets/Scenes/Level2_DiningRoom.unity") 
                ? "Assets/Scenes/Level2_DiningRoom.unity" 
                : SourceDiningRoomPath;

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"Source dining room scene not found: {sourcePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);

            // Enclose the Dining Room with room walls & floor
            GameObject roomEnclosure = GameObject.Find("RoomEnclosure");
            if (roomEnclosure == null)
            {
                roomEnclosure = new GameObject("RoomEnclosure");

                var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                wallMat.color = new Color(0.20f, 0.22f, 0.28f);

                // Floor
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "SafetyFloor";
                floor.transform.SetParent(roomEnclosure.transform, false);
                floor.transform.position = new Vector3(0f, -0.05f, 0f);
                floor.transform.localScale = new Vector3(16f, 0.1f, 16f);
                var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                floorMat.color = new Color(0.18f, 0.16f, 0.15f);
                floor.GetComponent<Renderer>().material = floorMat;

                // Walls
                CreateWall(roomEnclosure.transform, "NorthWall", new Vector3(0f, 2.5f, 6.5f), new Vector3(16f, 5f, 0.4f), wallMat);
                CreateWall(roomEnclosure.transform, "SouthWall", new Vector3(0f, 2.5f, -6.5f), new Vector3(16f, 5f, 0.4f), wallMat);
                CreateWall(roomEnclosure.transform, "EastWall", new Vector3(6.5f, 2.5f, 0f), new Vector3(0.4f, 5f, 16f), wallMat);
                CreateWall(roomEnclosure.transform, "WestWall", new Vector3(-6.5f, 2.5f, 0f), new Vector3(0.4f, 5f, 16f), wallMat);
                CreateWall(roomEnclosure.transform, "Ceiling", new Vector3(0f, 5.0f, 0f), new Vector3(16f, 0.3f, 16f), wallMat);
            }

            // Add colliders to all table & chair meshes
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                if (mr.GetComponent<Collider>() == null && mr.gameObject.GetComponent<MeshFilter>() != null && mr.gameObject.GetComponent<MeshFilter>().sharedMesh != null)
                {
                    var col = mr.gameObject.AddComponent<MeshCollider>();
                    col.convex = false;
                }
            }

            // Start Point
            GameObject startPt = GameObject.Find("StartPoint");
            if (startPt == null) startPt = new GameObject("StartPoint");
            startPt.transform.position = new Vector3(0f, 0.15f, -3.5f);
            startPt.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // Add Chandelier Light
            GameObject chandelier = GameObject.Find("ChandelierLight");
            if (chandelier == null)
            {
                chandelier = new GameObject("ChandelierLight");
                chandelier.transform.position = new Vector3(0f, 2.8f, 0f);
                Light l = chandelier.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.92f, 0.75f);
                l.intensity = 2.5f;
                l.range = 14f;
            }

            // Place Golden Dining Key on the dining table / pedestal
            GameObject keyObj = GameObject.Find("GoldenDiningKey");
            if (keyObj == null)
            {
                keyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                keyObj.name = "GoldenDiningKey";
                keyObj.transform.localScale = new Vector3(0.09f, 0.025f, 0.20f);
            }
            keyObj.transform.position = new Vector3(0.0f, 0.95f, 0.0f);
            keyObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            RoomKeyItem keyComp = keyObj.GetComponent<RoomKeyItem>();
            if (keyComp == null) keyComp = keyObj.AddComponent<RoomKeyItem>();
            keyComp.KeyId = "Level1_Key";
            keyComp.KeyDisplayName = "Golden Dining Key";
            keyComp.GlowColor = new Color(1f, 0.75f, 0.2f, 1f);

            // Place Exit Doorway leading to Level 2
            GameObject doorObj = GameObject.Find("DiningRoomExitDoor");
            if (doorObj == null) doorObj = new GameObject("DiningRoomExitDoor");
            doorObj.transform.position = new Vector3(0f, 0f, 5.75f);
            doorObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            Transform doorVisual = doorObj.transform.Find("DoorFrame");
            if (doorVisual == null)
            {
                GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "DoorFrame";
                frame.transform.SetParent(doorObj.transform, false);
                frame.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                frame.transform.localScale = new Vector3(1.6f, 2.6f, 0.2f);

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.18f, 0.24f, 0.35f);
                frame.GetComponent<Renderer>().material = mat;
            }

            RoomEscapeDoor doorComp = doorObj.GetComponent<RoomEscapeDoor>();
            if (doorComp == null) doorComp = doorObj.AddComponent<RoomEscapeDoor>();
            doorComp.RequiredKeyId = "Level1_Key";
            doorComp.DoorName = "Living Room Passage Door";

            // Ensure Managers
            EnsureManagersInScene();

            // Save Scene
            EditorSceneManager.SaveScene(scene, Level1ScenePath);
            Debug.Log($"[CampaignLevelBuilder] Saved Level 1 to {Level1ScenePath}");
        }

        private static void BuildLevel2()
        {
            Debug.Log("[CampaignLevelBuilder] Building Level 2: Medieval Chamber (MEDIUM)...");

            if (!File.Exists(Level2ScenePath))
            {
                Debug.LogWarning($"Level 2 scene not found: {Level2ScenePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);

            // Deactivate old cameras
            Camera[] oldCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in oldCams)
            {
                if (!cam.name.Contains("PlayerCamera"))
                    cam.gameObject.SetActive(false);
            }

            // Directional Sun Light
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool hasDir = false;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.enabled) { hasDir = true; break; }
            }
            if (!hasDir)
            {
                GameObject sun = new GameObject("MedievalRoomSun");
                Light l = sun.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(1f, 0.94f, 0.85f);
                l.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Add colliders to mesh renderers (floors, walls, furniture)
            MeshRenderer[] renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                if (mr.GetComponent<Collider>() == null && mr.gameObject.GetComponent<MeshFilter>() != null && mr.gameObject.GetComponent<MeshFilter>().sharedMesh != null)
                {
                    var col = mr.gameObject.AddComponent<MeshCollider>();
                    col.convex = false;
                }
            }

            // Align Ceiling (Y=3.0) and Floor tiles (Y=0.0) with zero gaps
            GameObject cNW = GameObject.Find("Ceiling_NW");
            if (cNW != null) cNW.transform.position = new Vector3(-2f, 3f, 0.1157f);
            GameObject cNE = GameObject.Find("Ceiling_NE");
            if (cNE != null) cNE.transform.position = new Vector3(2f, 3f, 0.1157f);
            GameObject cSW = GameObject.Find("Ceiling_SW");
            if (cSW != null) cSW.transform.position = new Vector3(-2f, 3f, -3.8843f);
            GameObject cSE = GameObject.Find("Ceiling_SE");
            if (cSE != null) cSE.transform.position = new Vector3(2f, 3f, -3.8843f);

            GameObject fNW = GameObject.Find("Floor_NW");
            if (fNW != null) fNW.transform.position = new Vector3(-2f, 0f, 0.1157f);
            GameObject fNE = GameObject.Find("Floor_NE");
            if (fNE != null) fNE.transform.position = new Vector3(2f, 0f, 0.1157f);
            GameObject fSW = GameObject.Find("Floor_SW");
            if (fSW != null) fSW.transform.position = new Vector3(-2f, 0f, -3.8843f);
            GameObject fSE = GameObject.Find("Floor_SE");
            if (fSE != null) fSE.transform.position = new Vector3(2f, 0f, -3.8843f);

            // Start Point on stone floor
            GameObject startPt = GameObject.Find("StartPoint");
            if (startPt == null) startPt = new GameObject("StartPoint");
            startPt.transform.position = new Vector3(0f, 0.05f, -2.6f);
            startPt.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // Clue Note on Council Table at (0.0f, 1.01f, -1.0f)
            GameObject noteGo = GameObject.Find("KnightsDecreeScroll");
            if (noteGo == null)
            {
                noteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                noteGo.name = "KnightsDecreeScroll";
            }
            noteGo.transform.position = new Vector3(0.0f, 1.01f, -1.0f);
            noteGo.transform.localScale = new Vector3(0.42f, 0.04f, 0.32f);
            noteGo.transform.rotation = Quaternion.Euler(0f, 15f, 0f);

            var noteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            noteMat.color = new Color(0.96f, 0.91f, 0.76f);
            noteGo.GetComponent<Renderer>().material = noteMat;

            var noteComp = noteGo.GetComponent<RoomClueNote>();
            if (noteComp == null) noteComp = noteGo.AddComponent<RoomClueNote>();
            noteComp.NoteTitle = "Knight's Ancient Decree";
            noteComp.NoteContent = "📜 KNIGHT'S ANCIENT DECREE:\n'Hark, traveler! The gateway to the Witch's Alchemical Sanctum is locked.\nThe Royal Castle Key lies secured within the Ironbound Chest beside the hearth.\nTurn the chest's runic dial to IV (4 o'clock) to align the ancient sigils and unlock the key!'";
            noteComp.ShortHint = "Inspect the ironbound chest beside the fireplace hearth!";
            noteComp.ObjectiveIndexToComplete = 0;

            // Royal Castle Key floating above chest at (2.8f, 1.25f, -2.8f)
            GameObject keyObj = GameObject.Find("RoyalCastleKey");
            if (keyObj == null)
            {
                keyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                keyObj.name = "RoyalCastleKey";
            }
            keyObj.transform.position = new Vector3(2.8f, 1.25f, -2.8f);
            keyObj.transform.localScale = new Vector3(0.12f, 0.03f, 0.24f);
            keyObj.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            var keyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            keyMat.color = new Color(1.0f, 0.85f, 0.25f);
            keyObj.GetComponent<Renderer>().material = keyMat;

            var keySphereCol = keyObj.GetComponent<SphereCollider>();
            if (keySphereCol == null) keySphereCol = keyObj.AddComponent<SphereCollider>();
            keySphereCol.radius = 0.45f;

            RoomKeyItem keyComp = keyObj.GetComponent<RoomKeyItem>();
            if (keyComp == null) keyComp = keyObj.AddComponent<RoomKeyItem>();
            keyComp.KeyId = "Level2_Key";
            keyComp.KeyDisplayName = "Royal Castle Key";
            keyComp.GlowColor = new Color(1.0f, 0.85f, 0.2f, 1f);

            // Dial Mechanism on Chest front panel at (2.55f, 0.82f, -2.55f)
            GameObject boxObj = GameObject.Find("MedievalChestLock");
            if (boxObj == null)
            {
                boxObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                boxObj.name = "MedievalChestLock";
            }
            boxObj.transform.position = new Vector3(2.55f, 0.82f, -2.55f);
            boxObj.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
            boxObj.transform.rotation = Quaternion.Euler(45f, -45f, 0f);

            var boxMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            boxMat.color = new Color(0.85f, 0.65f, 0.25f);
            boxObj.GetComponent<Renderer>().material = boxMat;

            var boxCol = boxObj.GetComponent<BoxCollider>();
            if (boxCol == null) boxCol = boxObj.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(0.9f, 0.9f, 0.9f);

            var pBox = boxObj.GetComponent<ClockworkPuzzleBox>();
            if (pBox == null) pBox = boxObj.AddComponent<ClockworkPuzzleBox>();
            pBox.BoxName = "Chest Runic Dial";
            pBox.KeyDisplayName = "Royal Castle Key";
            pBox.KeyToReveal = keyObj;
            pBox.BoxLid = boxObj.transform;

            if (keyObj != null) keyObj.SetActive(false);

            // Exit Gateway Door leading to Level 3
            GameObject doorObj = GameObject.Find("MedievalChamberExitDoor");
            if (doorObj == null) doorObj = new GameObject("MedievalChamberExitDoor");
            doorObj.transform.position = new Vector3(0f, 0f, 4.0f);
            doorObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            Transform doorVisual = doorObj.transform.Find("DoorFrame");
            if (doorVisual == null)
            {
                GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "DoorFrame";
                frame.transform.SetParent(doorObj.transform, false);
                frame.transform.localPosition = new Vector3(0f, 1.30f, 0f);
                frame.transform.localScale = new Vector3(1.6f, 2.6f, 0.2f);

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.25f, 0.18f, 0.12f);
                frame.GetComponent<Renderer>().material = mat;
            }

            RoomEscapeDoor doorComp = doorObj.GetComponent<RoomEscapeDoor>();
            if (doorComp == null) doorComp = doorObj.AddComponent<RoomEscapeDoor>();
            doorComp.RequiredKeyId = "Level2_Key";
            doorComp.DoorName = "Sanctum Oak Gateway";

            var doorCol = doorObj.GetComponent<BoxCollider>();
            if (doorCol == null) doorCol = doorObj.AddComponent<BoxCollider>();
            doorCol.center = new Vector3(0f, 1.30f, -0.2f);
            doorCol.size = new Vector3(2.0f, 2.6f, 0.8f);

            // Ensure Managers
            EnsureManagersInScene();

            // Save Scene
            EditorSceneManager.SaveScene(scene, Level2ScenePath);
            Debug.Log($"[CampaignLevelBuilder] Saved Level 2 to {Level2ScenePath}");
        }

        private static void BuildLevel3()
        {
            Debug.Log("[CampaignLevelBuilder] Building Level 3: Witch's Cottage (HARD)...");

            if (!File.Exists(SourceCottagePath))
            {
                Debug.LogWarning($"Source cottage scene not found: {SourceCottagePath}");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SourceCottagePath, OpenSceneMode.Single);

            // Clean up VR Rig & XR Origins in Level 3 so PC desktop player works smoothly
            string[] vrNames = { "XR Origin (VR)", "======== VR MANAGMENT ========", "Locomotion", "Teleportation" };
            foreach (var vn in vrNames)
            {
                var vrGo = GameObject.Find(vn);
                if (vrGo != null) vrGo.SetActive(false);
            }

            var xrOrigins = UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var xr in xrOrigins)
            {
                if (xr != null) xr.gameObject.SetActive(false);
            }

            var masterControllers = UnityEngine.Object.FindObjectsByType<MasterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mc in masterControllers)
            {
                if (mc != null)
                {
                    mc.enabled = false;
                    mc.gameObject.SetActive(false);
                }
            }

            // Set proper StartPoint on open floor in front of altar/desk
            GameObject startPt = GameObject.Find("StartPoint");
            if (startPt == null) startPt = new GameObject("StartPoint");
            startPt.transform.position = new Vector3(-1.81f, 0.05f, -1.25f);
            startPt.transform.rotation = Quaternion.Euler(0f, 105f, 0f);

            // Ensure Managers exist
            EnsureManagersInScene();

            // Save Scene
            EditorSceneManager.SaveScene(scene, Level3ScenePath);
            Debug.Log($"[CampaignLevelBuilder] Saved Level 3 to {Level3ScenePath}");
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = size;
            if (mat != null) wall.GetComponent<Renderer>().material = mat;
        }

        private static void EnsureManagersInScene()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomManager>() == null)
            {
                GameObject mgr = new GameObject("EscapeRoomManager");
                mgr.AddComponent<EscapeRoomManager>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomHUD>() == null)
            {
                GameObject hud = new GameObject("EscapeRoomHUD");
                hud.AddComponent<EscapeRoomHUD>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(Level1ScenePath, true),
                new EditorBuildSettingsScene(Level2ScenePath, true),
                new EditorBuildSettingsScene(Level3ScenePath, true),
                new EditorBuildSettingsScene(SourceCottagePath, true)
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("[CampaignLevelBuilder] Updated EditorBuildSettings with 3 Campaign Levels!");
        }

        [MenuItem("Tools/Escape Room/Play Level 1 - Dining Room (Easy)", false, 10)]
        public static void PlayLevel1()
        {
            LaunchLevel(Level1ScenePath);
        }

        [MenuItem("Tools/Escape Room/Play Level 2 - Living Room (Medium)", false, 11)]
        public static void PlayLevel2()
        {
            LaunchLevel(Level2ScenePath);
        }

        [MenuItem("Tools/Escape Room/Play Level 3 - Witch Cottage (Hard)", false, 12)]
        public static void PlayLevel3()
        {
            LaunchLevel(Level3ScenePath);
        }

        private static void LaunchLevel(string scenePath)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            if (File.Exists(scenePath))
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(scenePath);
                EditorApplication.isPlaying = true;
            }
            else
            {
                BuildAllLevels(false);
                EditorSceneManager.OpenScene(scenePath);
                EditorApplication.isPlaying = true;
            }
        }
    }
}
