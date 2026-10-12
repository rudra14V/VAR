using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Automatic initializer for Escape Room Player Mode across all 3 levels.
    /// Runs seamlessly on scene load to set up the First Person Player,
    /// room boundary colliders, interaction system, audio, and puzzle managers.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class EscapeRoomBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitGlobal()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void AutoBootstrap()
        {
            EnsureBootstrapInScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureBootstrapInScene();
        }

        public static void EnsureBootstrapInScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            string name = activeScene.name.ToLower();
            if (!name.Contains("escape") && !name.Contains("room") && !name.Contains("level") &&
                !name.Contains("dining") && !name.Contains("living") && !name.Contains("demo") &&
                !name.Contains("witch") && !name.Contains("cottage"))
            {
                if (GameObject.Find("======== VR MANAGMENT ========") == null && GameObject.Find("Cauldron") == null)
                    return;
            }

            if (FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject bootGo = new GameObject("EscapeRoomBootstrap");
                bootGo.AddComponent<EscapeRoomBootstrap>();
            }
        }

        private void Awake()
        {
            SetupRoomBoundaries();
            SetupManagers();
            SetupPlayer();
            SetupLevelPuzzles();
            EnsureMeshColliders();
        }

        private void SetupManagers()
        {
            // GameLevelManager
            if (FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glmGo = new GameObject("GameLevelManager");
                glmGo.AddComponent<GameLevelManager>();
            }

            // EscapeRoomManager
            if (FindFirstObjectByType<EscapeRoomManager>() == null)
            {
                GameObject mgrGo = new GameObject("EscapeRoomManager");
                var mgr = mgrGo.AddComponent<EscapeRoomManager>();

                // Load audio cues
                mgr.ObjectiveCompleteClip = LoadAudioClip("SFXPotionReady");
                mgr.VictoryClip = LoadAudioClip("SFXEscape");
                mgr.NotificationClip = LoadAudioClip("SFXBookOpens");
            }

            // EscapeRoomHUD
            if (FindFirstObjectByType<EscapeRoomHUD>() == null)
            {
                GameObject hudGo = new GameObject("EscapeRoomHUD");
                hudGo.AddComponent<EscapeRoomHUD>();
            }

            // DayNightCycle
            if (FindFirstObjectByType<DayNightCycle>() == null)
            {
                GameObject dncGo = new GameObject("DayNightCycle");
                dncGo.AddComponent<DayNightCycle>();
            }
        }

        private void SetupPlayer()
        {
            if (FindFirstObjectByType<EscapeRoomPlayerController>() != null)
                return;

            // Find starting position based on scene
            Vector3 spawnPos = new Vector3(0f, 0.05f, 0f);
            Quaternion spawnRot = Quaternion.identity;

            var activeScene = SceneManager.GetActiveScene();
            string sName = activeScene.name.ToLower();

            if (sName.Contains("dining") || sName.Contains("dening") || (sName.Contains("1") && !sName.Contains("living")))
            {
                // Level 1: Inside the Grand Dining Room hall
                spawnPos = new Vector3(0f, 0.15f, -3.5f);
                spawnRot = Quaternion.Euler(0f, 0f, 0f);
            }
            else if (sName.Contains("medieval") || sName.Contains("chamber") || sName.Contains("living") || sName.Contains("demo") || sName.Contains("2"))
            {
                // Level 2: Inside the Medieval Chamber on the stone floor
                spawnPos = new Vector3(0f, 0.05f, -2.6f);
                spawnRot = Quaternion.Euler(0f, 0f, 0f);
            }
            else
            {
                // Level 3: Inside the Witch's Cottage on open floor in front of altar/desk and cauldron
                spawnPos = new Vector3(-2.2f, 0.05f, -0.2f);
                spawnRot = Quaternion.Euler(0f, 45f, 0f);
            }

            // CRITICAL FOR ALL LEVELS & PC MODE:
            // Deactivate ALL existing VR Rigs, XR Origins, and MasterControllers so they do NOT collide or conflict!
            var xrOrigins = FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var xr in xrOrigins)
            {
                if (xr != null) xr.gameObject.SetActive(false);
            }

            var masterControllers = FindObjectsByType<MasterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mc in masterControllers)
            {
                if (mc != null)
                {
                    mc.enabled = false;
                    mc.gameObject.SetActive(false);
                }
            }

            // Specifically deactivate any VR manager hierarchy objects
            string[] vrRootsToDisable = { "======== VR MANAGMENT ========", "XR Origin (VR)", "Locomotion", "Teleportation" };
            foreach (var rootName in vrRootsToDisable)
            {
                GameObject vrGo = GameObject.Find(rootName);
                if (vrGo != null) vrGo.SetActive(false);
            }

            // Deactivate ALL existing non-player cameras and Cinemachine virtual cameras
            Camera[] existingCams = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in existingCams)
            {
                if (c != null && !c.name.Contains("PlayerCamera"))
                {
                    c.enabled = false;
                    c.gameObject.SetActive(false);
                }
            }

            // Stop and disable any PlayableDirectors (Cinemachine timelines)
            var directors = FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in directors)
            {
                if (d != null)
                {
                    d.Stop();
                    d.enabled = false;
                }
            }

            // Create Player GameObject with clean zero-pitch / zero-roll orientation
            GameObject player = new GameObject("EscapeRoomPlayer");
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.Euler(0f, spawnRot.eulerAngles.y, 0f);

            // CharacterController with compact 1.0m height and 0.26m radius
            // Seamless fit for low-poly interiors, inspecting low tables, locks, and desks
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.radius = 0.26f;
            cc.height = 1.0f;
            cc.center = new Vector3(0f, 0.50f, 0f);
            cc.stepOffset = 0.25f;
            cc.slopeLimit = 45f;

            // Camera Holder at 1.0m height - level horizon
            GameObject camHolder = new GameObject("CameraHolder");
            camHolder.transform.SetParent(player.transform, false);
            camHolder.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            camHolder.transform.localRotation = Quaternion.identity;

            // Player Camera
            GameObject camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(camHolder.transform, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;
            camGo.tag = "MainCamera";

            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.depth = 10f; // High priority over any remaining cameras

            // Ensure no duplicate AudioListeners exist in the scene
            var existingListeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var al in existingListeners)
            {
                if (al != null) al.enabled = false;
            }
            camGo.AddComponent<AudioListener>();

            // Universal Additional Camera Data for URP
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // Player Controller
            EscapeRoomPlayerController controller = player.AddComponent<EscapeRoomPlayerController>();
            controller.PlayerCamera = cam;
            controller.CameraHolder = camHolder.transform;
            controller.StandHeight = 1.0f;
            controller.EyeHeight = 1.0f;
            controller.CrouchHeight = 0.60f;
            controller.JumpHeight = 0.40f;

            // Footstep audio clips - ensure only non-null clips
            List<AudioClip> footList = new List<AudioClip>();
            string[] clipCandidates = { "SFXImpactBooks01", "SFXImpactLog01", "SFXImpactLog02", "SFXImpactDrawer01" };
            foreach (var cn in clipCandidates)
            {
                var c = LoadAudioClip(cn);
                if (c != null) footList.Add(c);
            }
            controller.FootstepClips = footList.ToArray();
            controller.JumpClip = LoadAudioClip("SFXImpactDrawer02");
            controller.LandClip = LoadAudioClip("SFXImpactDrawer01");

            // Player Interaction
            EscapeRoomInteraction interaction = camGo.AddComponent<EscapeRoomInteraction>();
            interaction.PickupClip = LoadAudioClip("SFXImpactDrawer02");
            interaction.DropClip = LoadAudioClip("SFXKeyDrop");
            interaction.ThrowClip = LoadAudioClip("SFXWandShoot");

            Debug.Log($"[EscapeRoomBootstrap] First-Person Player Mode successfully initialized at {spawnPos} in {SceneManager.GetActiveScene().name}!");
        }

        private void SetupRoomBoundaries()
        {
            // Ensure boundary walls exist so player stays strictly inside rooms
            GameObject boundaries = GameObject.Find("RoomBoundaries");
            if (boundaries == null)
            {
                boundaries = new GameObject("RoomBoundaries");

                var activeScene = SceneManager.GetActiveScene();
                string sName = activeScene.name.ToLower();

                if (sName.Contains("dining") || sName.Contains("dening") || (sName.Contains("1") && !sName.Contains("living")))
                {
                    // Floor boundary
                    CreateBoxBoundary(boundaries.transform, "SafetyFloor", new Vector3(0f, -0.05f, 0f), new Vector3(20f, 0.1f, 20f));
                    // Ceiling boundary
                    CreateBoxBoundary(boundaries.transform, "Ceiling", new Vector3(0f, 5.0f, 0f), new Vector3(16f, 0.3f, 16f));
                    // Walls - North wall is split to leave unobstructed doorway from x = -1.2m to x = +1.2m
                    CreateBoxBoundary(boundaries.transform, "Wall_North_L", new Vector3(-4.5f, 2.5f, 6.2f), new Vector3(6.6f, 5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_North_R", new Vector3(4.5f, 2.5f, 6.2f), new Vector3(6.6f, 5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_North_Top", new Vector3(0f, 4.0f, 6.2f), new Vector3(2.4f, 2.0f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_South", new Vector3(0f, 2.5f, -6.2f), new Vector3(16f, 5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_East", new Vector3(6.2f, 2.5f, 0f), new Vector3(0.4f, 5f, 16f));
                    CreateBoxBoundary(boundaries.transform, "Wall_West", new Vector3(-6.2f, 2.5f, 0f), new Vector3(0.4f, 5f, 16f));
                }
                else if (sName.Contains("medieval") || sName.Contains("chamber") || sName.Contains("living") || sName.Contains("demo") || sName.Contains("2"))
                {
                    // Level 2: Medieval Chamber boundaries (8m x 8m room)
                    // Floor boundary
                    CreateBoxBoundary(boundaries.transform, "Floor_Bound", new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 12f));
                    // Ceiling boundary
                    CreateBoxBoundary(boundaries.transform, "Ceiling_Bound", new Vector3(0f, 4.2f, 0f), new Vector3(12f, 0.2f, 12f));
                    // Perimeter Walls with spacious open doorway at North (z = 4.0m)
                    CreateBoxBoundary(boundaries.transform, "Wall_West", new Vector3(-4.2f, 2.0f, 0f), new Vector3(0.4f, 4.5f, 10f));
                    CreateBoxBoundary(boundaries.transform, "Wall_East", new Vector3(4.2f, 2.0f, 0f), new Vector3(0.4f, 4.5f, 10f));
                    CreateBoxBoundary(boundaries.transform, "Wall_South", new Vector3(0f, 2.0f, -4.2f), new Vector3(10f, 4.5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_North_L", new Vector3(-3.0f, 2.0f, 4.2f), new Vector3(3.8f, 4.5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_North_R", new Vector3(3.0f, 2.0f, 4.2f), new Vector3(3.8f, 4.5f, 0.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_North_Top", new Vector3(0f, 3.8f, 4.2f), new Vector3(2.2f, 1.5f, 0.4f));
                }
                else if (sName.Contains("escape") || sName.Contains("cottage") || sName.Contains("3"))
                {
                    // Universal Safety Floor under cottage
                    CreateBoxBoundary(boundaries.transform, "SafetyFloor", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 30f));

                    // Ceiling Barrier
                    CreateBoxBoundary(boundaries.transform, "CottageCeiling", new Vector3(-1.5f, 3.8f, 0f), new Vector3(16f, 0.3f, 16f));

                    // Back Wall (West, near kitchen)
                    CreateBoxBoundary(boundaries.transform, "Wall_West", new Vector3(-8.2f, 2f, 0f), new Vector3(0.5f, 5f, 10f));

                    // Front Wall (East, near door) - with spacious open doorway for door (z = -2.3 to -0.3)
                    CreateBoxBoundary(boundaries.transform, "Wall_East_North", new Vector3(4.8f, 2f, 2.2f), new Vector3(0.5f, 5f, 5f));
                    CreateBoxBoundary(boundaries.transform, "Wall_East_South", new Vector3(4.8f, 2f, -4.5f), new Vector3(0.5f, 5f, 4.4f));
                    CreateBoxBoundary(boundaries.transform, "Wall_East_Top", new Vector3(4.8f, 3.8f, -1.3f), new Vector3(0.5f, 1.5f, 2.0f));

                    // North Wall
                    CreateBoxBoundary(boundaries.transform, "Wall_North", new Vector3(-1.5f, 2f, 3.8f), new Vector3(15f, 5f, 0.5f));

                    // South Wall
                    CreateBoxBoundary(boundaries.transform, "Wall_South", new Vector3(-1.5f, 2f, -3.8f), new Vector3(15f, 5f, 0.5f));
                }
                else
                {
                    // Generic safety floor
                    CreateBoxBoundary(boundaries.transform, "SafetyFloor", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 30f));
                }
            }
        }

        private void SetupLevelPuzzles()
        {
            var activeScene = SceneManager.GetActiveScene();
            string sName = activeScene.name.ToLower();

            // Level 1: Grand Dining Room (EASY)
            if (sName.Contains("dining") || sName.Contains("dening") || (sName.Contains("1") && !sName.Contains("living")))
            {
                // Deactivate any pre-existing blocking solid NorthWall from base scene
                GameObject oldNorth = GameObject.Find("NorthWall");
                if (oldNorth != null) oldNorth.SetActive(false);

                // Ensure room walls enclose the dining room with open exit doorway
                GameObject enclosure = GameObject.Find("DiningRoomEnclosure");
                if (enclosure == null)
                {
                    enclosure = new GameObject("DiningRoomEnclosure");
                    var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    wallMat.color = new Color(0.20f, 0.22f, 0.28f);

                    // Split North Wall into Left, Right, and Lintel to leave open door passage from x = -1.2 to x = +1.2
                    CreateWall(enclosure.transform, "NorthWall_Left", new Vector3(-4.1f, 2.5f, 6.0f), new Vector3(5.8f, 5f, 0.4f), wallMat);
                    CreateWall(enclosure.transform, "NorthWall_Right", new Vector3(4.1f, 2.5f, 6.0f), new Vector3(5.8f, 5f, 0.4f), wallMat);
                    CreateWall(enclosure.transform, "NorthWall_Lintel", new Vector3(0f, 3.9f, 6.0f), new Vector3(2.4f, 2.2f, 0.4f), wallMat);

                    CreateWall(enclosure.transform, "SouthWall", new Vector3(0f, 2.5f, -6.0f), new Vector3(14f, 5f, 0.4f), wallMat);
                    CreateWall(enclosure.transform, "EastWall", new Vector3(6.0f, 2.5f, 0f), new Vector3(0.4f, 5f, 14f), wallMat);
                    CreateWall(enclosure.transform, "WestWall", new Vector3(-6.0f, 2.5f, 0f), new Vector3(0.4f, 5f, 14f), wallMat);
                    CreateWall(enclosure.transform, "Ceiling", new Vector3(0f, 5.0f, 0f), new Vector3(14f, 0.3f, 14f), wallMat);
                }

                // Construct scenic Grand Castle Corridor & Outside Mountain Terrace Vista beyond the doorway
                SetupCastleCorridorAndOutsideVista();

                // Chandelier light
                if (GameObject.Find("DiningChandelier") == null)
                {
                    GameObject chGo = new GameObject("DiningChandelier");
                    chGo.transform.position = new Vector3(0f, 2.6f, 0f);
                    Light l = chGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.92f, 0.78f);
                    l.intensity = 2.2f;
                    l.range = 14f;
                }

                // 1. Butler's Banquet Protocol Note on sideboard
                if (FindFirstObjectByType<RoomClueNote>() == null)
                {
                    GameObject noteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    noteGo.name = "ButlerBanquetProtocolNote";
                    noteGo.transform.position = new Vector3(-2.6f, 0.85f, -1.0f);
                    noteGo.transform.localScale = new Vector3(0.28f, 0.02f, 0.22f);
                    noteGo.transform.rotation = Quaternion.Euler(0f, -25f, 0f);

                    var noteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    noteMat.color = new Color(0.95f, 0.88f, 0.70f);
                    noteGo.GetComponent<Renderer>().material = noteMat;

                    var noteComp = noteGo.AddComponent<RoomClueNote>();
                    noteComp.NoteTitle = "Butler's Banquet Protocol";
                    noteComp.NoteContent = "📜 THE BUTLER'S FEAST PROTOCOL:\n'The Master's Golden Key is sealed within the Grand Banquet Hearth.\nTo release the seal, ignite the Three Sacred Candelabras along the banquet tables: North (Flame), East (Wine), and South (Shadow)!'\nUse the Golden Key to unlock the passage to the Modern Living Room!";
                    noteComp.ShortHint = "Ignite the 3 candelabras along the tables!";
                    noteComp.ObjectiveIndexToComplete = 0;
                }

                // 2. Three Sacred Banquet Candelabras
                if (FindFirstObjectByType<DiningChalicePuzzle>() == null)
                {
                    DiningChalicePuzzle.ResetPuzzle();
                    CreateCandelabra("North Candelabra (Flame)", 0, new Vector3(0.6f, 0.84f, 1.8f), new Color(1f, 0.5f, 0.15f));
                    CreateCandelabra("East Candelabra (Wine)", 1, new Vector3(1.2f, 0.84f, 0.0f), new Color(0.9f, 0.2f, 0.4f));
                    CreateCandelabra("South Candelabra (Shadow)", 2, new Vector3(0.6f, 0.84f, -1.8f), new Color(0.4f, 0.6f, 1f));
                }

                // 3. Golden Dining Key (Starts sealed until 3 candelabras are lit)
                GameObject keyGo = null;
                var existingKey = FindFirstObjectByType<RoomKeyItem>(FindObjectsInactive.Include);
                if (existingKey == null)
                {
                    keyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    keyGo.name = "GoldenDiningKey";
                    keyGo.transform.position = new Vector3(0.0f, 0.95f, 0.0f);
                    keyGo.transform.localScale = new Vector3(0.09f, 0.025f, 0.20f);
                    keyGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(1f, 0.82f, 0.15f);
                    keyGo.GetComponent<Renderer>().material = mat;

                    var keyComp = keyGo.AddComponent<RoomKeyItem>();
                    keyComp.KeyId = "Level1_Key";
                    keyComp.KeyDisplayName = "Golden Dining Key";
                    keyComp.GlowColor = new Color(1f, 0.75f, 0.2f, 1f);

                    // Key starts inactive until all 3 candelabras are lit!
                    keyGo.SetActive(false);
                }
                else
                {
                    keyGo = existingKey.gameObject;
                    var kc = keyGo.GetComponent<RoomKeyItem>();
                    if (kc != null)
                    {
                        kc.KeyId = "Level1_Key";
                        kc.KeyDisplayName = "Golden Dining Key";
                    }
                }

                // 4. Passage Door at North Wall leading to Level 2 (Properly aligned to North Wall)
                AlignLevel1Door();
            }
            // Level 2: Modern Living Room (MEDIUM)
            // Level 2: Medieval Chamber (MEDIUM)
            else if (sName.Contains("medieval") || sName.Contains("chamber") || sName.Contains("living") || sName.Contains("demo") || sName.Contains("2"))
            {
                // Align Ceiling (Y=3.0) and Floor tiles (Y=0.0) with zero gaps & remove windows
                AlignMedievalArchitecture();

                // Ensure Directional Light
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                bool hasDir = false;
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional && l.enabled) { hasDir = true; break; }
                }
                if (!hasDir)
                {
                    GameObject dirGo = new GameObject("MedievalRoomSun");
                    Light l = dirGo.AddComponent<Light>();
                    l.type = LightType.Directional;
                    l.color = new Color(1f, 0.94f, 0.85f);
                    l.intensity = 1.2f;
                    dirGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }

                // 1. Knight's Ancient Scroll on Table at (0.0f, 1.01f, -1.0f) - table surface is at Y=0.973m
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
                noteComp.NoteContent = "📜 KNIGHT'S ANCIENT DECREE:\n'Hark, traveler! The gateway to the Witch's Alchemical Sanctum is locked.\nThe Royal Castle Key lies secured within the Ironbound Chest beside the hearth.\nAlign the chest's runic dial with the Guardian's Fourth Sigil — IV — to unseal the chest and claim the key!'";
                noteComp.ShortHint = "Inspect the ironbound chest beside the fireplace hearth!";
                noteComp.ObjectiveIndexToComplete = 0;

                // Scroll Glow Light
                if (GameObject.Find("KnightsScrollLight") == null)
                {
                    GameObject sLight = new GameObject("KnightsScrollLight");
                    sLight.transform.position = new Vector3(0.0f, 1.30f, -1.0f);
                    Light sl = sLight.AddComponent<Light>();
                    sl.type = LightType.Point;
                    sl.color = new Color(1f, 0.85f, 0.45f);
                    sl.intensity = 1.8f;
                    sl.range = 3.0f;
                }

                // 2. Royal Castle Key - Positioned to float above the chest at (2.8f, 1.25f, -2.8f)
                GameObject keyGo = GameObject.Find("RoyalCastleKey");
                if (keyGo == null)
                {
                    keyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    keyGo.name = "RoyalCastleKey";
                }
                keyGo.transform.position = new Vector3(2.8f, 1.25f, -2.8f);
                keyGo.transform.localScale = new Vector3(0.12f, 0.03f, 0.24f);
                keyGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

                var keyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                keyMat.color = new Color(1.0f, 0.85f, 0.25f);
                keyGo.GetComponent<Renderer>().material = keyMat;

                // Ensure pickup collider is large and unobstructed
                var keySphereCol = keyGo.GetComponent<SphereCollider>();
                if (keySphereCol == null) keySphereCol = keyGo.AddComponent<SphereCollider>();
                keySphereCol.radius = 0.45f;
                keySphereCol.isTrigger = false;

                var keyComp = keyGo.GetComponent<RoomKeyItem>();
                if (keyComp == null) keyComp = keyGo.AddComponent<RoomKeyItem>();
                keyComp.KeyId = "Level2_Key";
                keyComp.KeyDisplayName = "Royal Castle Key";
                keyComp.GlowColor = new Color(1.0f, 0.85f, 0.2f, 1f);

                // Key Glow Light
                if (GameObject.Find("RoyalKeyLight") == null)
                {
                    GameObject kLight = new GameObject("RoyalKeyLight");
                    kLight.transform.SetParent(keyGo.transform, false);
                    Light kl = kLight.AddComponent<Light>();
                    kl.type = LightType.Point;
                    kl.color = new Color(1.0f, 0.85f, 0.3f);
                    kl.intensity = 2.0f;
                    kl.range = 3.0f;
                }

                // 3. Runic Dial Mechanism mounted prominently on the front panel of the chest
                // Chest is at (2.8, 0, -2.8); front face is at ~ (2.55, 0.82, -2.55)
                GameObject boxGo = GameObject.Find("MedievalChestLock");
                if (boxGo == null)
                {
                    boxGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    boxGo.name = "MedievalChestLock";
                }
                boxGo.transform.position = new Vector3(2.55f, 0.82f, -2.55f);
                boxGo.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                boxGo.transform.rotation = Quaternion.Euler(45f, -45f, 0f);

                var boxMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                boxMat.color = new Color(0.85f, 0.65f, 0.25f);
                boxGo.GetComponent<Renderer>().material = boxMat;

                // Add large interaction collider so player cannot miss clicking it
                var boxCol = boxGo.GetComponent<BoxCollider>();
                if (boxCol == null) boxCol = boxGo.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.9f, 0.9f, 0.9f);

                var pBox = boxGo.GetComponent<ClockworkPuzzleBox>();
                if (pBox == null) pBox = boxGo.AddComponent<ClockworkPuzzleBox>();
                pBox.BoxName = "Chest Runic Dial";
                pBox.KeyDisplayName = "Royal Castle Key";
                pBox.KeyToReveal = keyGo;
                pBox.BoxLid = boxGo.transform;

                // Initially hide key until dial is solved
                if (!pBox.IsSolved)
                {
                    keyGo.SetActive(false);
                }
                else
                {
                    keyGo.SetActive(true);
                }

                // 4. Sanctum Oak Gateway Door leading to Level 3 at North Wall (0f, 0f, 4.05f)
                AlignLevel2Door();
            }
            // Level 3: The Witch's Cottage (HARD)
            else
            {
                // 1. Grimoire Clue Note on the central altar desk
                if (FindFirstObjectByType<RoomClueNote>() == null)
                {
                    GameObject noteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    noteGo.name = "AlchemistsGrimoireNote";
                    noteGo.transform.position = new Vector3(-1.15f, 0.90f, -1.45f);
                    noteGo.transform.localScale = new Vector3(0.30f, 0.02f, 0.24f);
                    noteGo.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

                    var noteMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    noteMat.color = new Color(0.85f, 0.92f, 0.75f); // mystic parchment
                    noteGo.GetComponent<Renderer>().material = noteMat;

                    var noteComp = noteGo.GetComponent<RoomClueNote>();
                    noteComp.NoteTitle = "Grand Alchemist's Grimoire";
                    noteComp.NoteContent = "🧙‍♀️ ALCHEMIST'S MASTER RITE:\n" +
                        "'To dispel the sanctuary seals and achieve freedom:\n" +
                        "1. Locate the Arcane Wand on the rack and channel its energy.\n" +
                        "2. Claim the Emerald Runestone Key from the altar desk.\n" +
                        "3. Unseal the heavy Cottage Exit Doors to step forth into the enchanted forest!'\n\n" +
                        "Controls: [E] Pick up & interact. [Left-Click] Cast arcane projectile. [Right-Click] Throw.";
                    noteComp.ShortHint = "Read the Grimoire on the desk and collect the Emerald Key!";
                    noteComp.ObjectiveIndexToComplete = 0;
                }

                // 2. The Arcane Wand - ensure grabbable collider and continuous physics
                GameObject wandObj = GameObject.Find("Wand");
                if (wandObj != null)
                {
                    var col = wandObj.GetComponent<Collider>();
                    if (col == null)
                    {
                        var box = wandObj.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.12f, 0.45f, 0.12f);
                    }
                    var rb = wandObj.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = wandObj.AddComponent<Rigidbody>();
                        rb.mass = 1.0f;
                        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    }
                }

                // 3. Emerald Runestone Key for Level 3
                var existingKey3 = FindFirstObjectByType<RoomKeyItem>(FindObjectsInactive.Include);
                if (existingKey3 == null)
                {
                    GameObject keyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    keyGo.name = "EmeraldRunestoneKey";
                    keyGo.transform.position = new Vector3(-0.85f, 0.95f, -1.15f);
                    keyGo.transform.localScale = new Vector3(0.09f, 0.025f, 0.20f);
                    keyGo.transform.rotation = Quaternion.Euler(0f, 60f, 0f);

                    var keyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    keyMat.color = new Color(0.2f, 0.95f, 0.45f);
                    keyGo.GetComponent<Renderer>().material = keyMat;

                    var keyComp = keyGo.AddComponent<RoomKeyItem>();
                    keyComp.KeyId = "Level3_Key";
                    keyComp.KeyDisplayName = "Emerald Runestone Key";
                    keyComp.GlowColor = new Color(0.1f, 1f, 0.4f, 1f);
                }

                // 4. Cottage Exit Door with RoomEscapeDoor leading into the enchanted forest
                AlignLevel3Door();

                // Level 3 window cleanup: "in level-03 remove the windows and make it normal"
                GameObject[] allGo3 = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var go in allGo3)
                {
                    if (go != null && (go.name.Contains("CastleWindow") || go.name.Contains("WindowSky") || go.name.Contains("WindowLightBeam")))
                    {
                        Destroy(go);
                    }
                }
            }
        }

        private void CreateCandelabra(string name, int index, Vector3 pos, Color flameColor)
        {
            GameObject canGo = new GameObject(name);
            canGo.transform.position = pos;

            // Stand
            GameObject baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGo.name = "Stem";
            baseGo.transform.SetParent(canGo.transform, false);
            baseGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            baseGo.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);

            var baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            baseMat.color = new Color(0.75f, 0.65f, 0.35f); // brass
            baseGo.GetComponent<Renderer>().material = baseMat;

            // Flame Sphere
            GameObject flameGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flameGo.name = "Flame";
            flameGo.transform.SetParent(canGo.transform, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            flameGo.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);

            var flameMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            flameMat.color = new Color(0.4f, 0.4f, 0.4f); // unlit grey initially
            var flameRenderer = flameGo.GetComponent<Renderer>();
            flameRenderer.material = flameMat;

            // Point Light (initially disabled)
            GameObject lightGo = new GameObject("FlameLight");
            lightGo.transform.SetParent(canGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = flameColor;
            l.range = 3.5f;
            l.intensity = 2.0f;
            l.enabled = false;

            // Add interaction collider
            var col = canGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.2f, 0f);
            col.size = new Vector3(0.25f, 0.45f, 0.25f);

            var puzzleComp = canGo.AddComponent<DiningChalicePuzzle>();
            puzzleComp.ChaliceName = name;
            puzzleComp.ChaliceIndex = index;
            puzzleComp.ChaliceLight = l;
            puzzleComp.FlameRenderer = flameRenderer;
            puzzleComp.LitColor = flameColor;
        }

        private void CreateWall(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = size;
            if (mat != null) wall.GetComponent<Renderer>().material = mat;
        }

        private void AlignMedievalArchitecture()
        {
            // The stone room is 8m x 8m with walls 3.0m tall.
            // SM_Ceiling_4x4 and Floor_4x4 span [minZ: -0.1157m, maxZ: 3.8843m] (size 4m).
            // Align them at runtime so ceiling sits flush at Y=3.00m and floors at Y=0.00m.
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

            // Level 2 window cleanup: "in level-02 remove windows"
            GameObject[] allGo2 = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo2)
            {
                if (go != null && (go.name.Contains("CastleWindow") || go.name.Contains("WindowSky") || go.name.Contains("WindowLightBeam")))
                {
                    Destroy(go);
                }
            }
        }

        private void AlignLevel1Door()
        {
            // 1. Structure the North Wall with an open doorway at center
            GameObject enclosure = GameObject.Find("RoomEnclosure");
            Material wallMat = null;
            if (enclosure != null)
            {
                Transform oldNorth = enclosure.transform.Find("NorthWall");
                if (oldNorth != null)
                {
                    var ren = oldNorth.GetComponent<Renderer>();
                    if (ren != null) wallMat = ren.sharedMaterial;
                    oldNorth.gameObject.SetActive(false);
                }

                if (wallMat == null)
                {
                    wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    wallMat.color = new Color(0.20f, 0.22f, 0.28f);
                }

                if (enclosure.transform.Find("NorthWall_Left") == null)
                {
                    GameObject wLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wLeft.name = "NorthWall_Left";
                    wLeft.transform.SetParent(enclosure.transform, false);
                    wLeft.transform.localPosition = new Vector3(-3.85f, 2.5f, 6.5f);
                    wLeft.transform.localScale = new Vector3(5.5f, 5.0f, 0.4f);
                    wLeft.GetComponent<Renderer>().material = wallMat;
                }

                if (enclosure.transform.Find("NorthWall_Right") == null)
                {
                    GameObject wRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wRight.name = "NorthWall_Right";
                    wRight.transform.SetParent(enclosure.transform, false);
                    wRight.transform.localPosition = new Vector3(3.85f, 2.5f, 6.5f);
                    wRight.transform.localScale = new Vector3(5.5f, 5.0f, 0.4f);
                    wRight.GetComponent<Renderer>().material = wallMat;
                }

                if (enclosure.transform.Find("NorthWall_Top") == null)
                {
                    GameObject wTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wTop.name = "NorthWall_Top";
                    wTop.transform.SetParent(enclosure.transform, false);
                    wTop.transform.localPosition = new Vector3(0.0f, 3.85f, 6.5f);
                    wTop.transform.localScale = new Vector3(2.2f, 2.3f, 0.4f);
                    wTop.GetComponent<Renderer>().material = wallMat;
                }
            }

            // 2. Properly position and align the Exit Doorway leading to Level 2
            GameObject doorGo = GameObject.Find("DiningRoomExitDoor");
            if (doorGo == null) doorGo = new GameObject("DiningRoomExitDoor");

            // Flush-aligned with the North Wall at Z = 6.45m
            doorGo.transform.position = new Vector3(0f, 0f, 6.45f);
            doorGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            Transform oldFrame = doorGo.transform.Find("DoorFrame");
            if (oldFrame != null)
            {
                Destroy(oldFrame.gameObject);
            }

            var frameMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            frameMat.color = new Color(0.18f, 0.16f, 0.14f);
            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.35f, 0.22f, 0.12f);

            if (doorGo.transform.Find("Post_L") == null)
            {
                GameObject postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postL.name = "Post_L";
                postL.transform.SetParent(doorGo.transform, false);
                postL.transform.localPosition = new Vector3(-0.95f, 1.35f, 0f);
                postL.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postL.GetComponent<Renderer>().material = frameMat;

                GameObject postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postR.name = "Post_R";
                postR.transform.SetParent(doorGo.transform, false);
                postR.transform.localPosition = new Vector3(0.95f, 1.35f, 0f);
                postR.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postR.GetComponent<Renderer>().material = frameMat;

                GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Lintel";
                lintel.transform.SetParent(doorGo.transform, false);
                lintel.transform.localPosition = new Vector3(0f, 2.7f, 0f);
                lintel.transform.localScale = new Vector3(2.08f, 0.18f, 0.35f);
                lintel.GetComponent<Renderer>().material = frameMat;
            }

            Transform hingeT = doorGo.transform.Find("DoorHinge");
            if (hingeT == null)
            {
                GameObject hingeGo = new GameObject("DoorHinge");
                hingeGo.transform.SetParent(doorGo.transform, false);
                hingeGo.transform.localPosition = new Vector3(-0.85f, 0f, 0f);
                hingeT = hingeGo.transform;

                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "DoorLeaf";
                leaf.transform.SetParent(hingeT, false);
                leaf.transform.localPosition = new Vector3(0.85f, 1.3f, 0f);
                leaf.transform.localScale = new Vector3(1.70f, 2.6f, 0.08f);
                leaf.GetComponent<Renderer>().material = woodMat;

                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                handle.name = "DoorHandle";
                handle.transform.SetParent(leaf.transform, false);
                handle.transform.localPosition = new Vector3(0.40f, -0.05f, -0.07f);
                handle.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                goldMat.color = new Color(0.95f, 0.80f, 0.20f);
                handle.GetComponent<Renderer>().material = goldMat;
            }

            var doorComp = doorGo.GetComponent<RoomEscapeDoor>();
            if (doorComp == null) doorComp = doorGo.AddComponent<RoomEscapeDoor>();
            doorComp.RequiredKeyId = "Level1_Key";
            doorComp.DoorName = "Living Room Passage Door";
            doorComp.DoorHinge = hingeT;
            doorComp.OpenAngle = -85f;
            doorComp.OpenSpeed = 2.5f;

            var trig = doorGo.GetComponent<BoxCollider>();
            if (trig == null) trig = doorGo.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(2.2f, 2.8f, 2.5f);
            trig.center = new Vector3(0f, 1.4f, 0.6f);
        }

        private void AlignLevel2Door()
        {
            GameObject doorGo = GameObject.Find("MedievalChamberExitDoor");
            if (doorGo == null) doorGo = new GameObject("MedievalChamberExitDoor");

            doorGo.transform.position = new Vector3(0f, 0f, 4.05f);
            doorGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            // Clean up any old frames if present
            Transform oldFrame = doorGo.transform.Find("DoorFrame");
            if (oldFrame != null) Destroy(oldFrame.gameObject);

            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.24f, 0.22f, 0.20f);
            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.32f, 0.20f, 0.12f);

            // Left Post, Right Post, Top Lintel
            if (doorGo.transform.Find("Post_L") == null)
            {
                GameObject postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postL.name = "Post_L";
                postL.transform.SetParent(doorGo.transform, false);
                postL.transform.localPosition = new Vector3(-0.95f, 1.35f, 0f);
                postL.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postL.GetComponent<Renderer>().material = stoneMat;

                GameObject postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postR.name = "Post_R";
                postR.transform.SetParent(doorGo.transform, false);
                postR.transform.localPosition = new Vector3(0.95f, 1.35f, 0f);
                postR.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postR.GetComponent<Renderer>().material = stoneMat;

                GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Lintel";
                lintel.transform.SetParent(doorGo.transform, false);
                lintel.transform.localPosition = new Vector3(0f, 2.7f, 0f);
                lintel.transform.localScale = new Vector3(2.08f, 0.18f, 0.35f);
                lintel.GetComponent<Renderer>().material = stoneMat;
            }

            Transform hingeT = doorGo.transform.Find("DoorHinge");
            if (hingeT == null)
            {
                GameObject hingeGo = new GameObject("DoorHinge");
                hingeGo.transform.SetParent(doorGo.transform, false);
                hingeGo.transform.localPosition = new Vector3(-0.85f, 0f, 0f);
                hingeT = hingeGo.transform;

                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "DoorLeaf";
                leaf.transform.SetParent(hingeT, false);
                leaf.transform.localPosition = new Vector3(0.85f, 1.3f, 0f);
                leaf.transform.localScale = new Vector3(1.70f, 2.6f, 0.08f);
                leaf.GetComponent<Renderer>().material = woodMat;

                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                handle.name = "DoorHandle";
                handle.transform.SetParent(leaf.transform, false);
                handle.transform.localPosition = new Vector3(0.40f, -0.05f, -0.07f);
                handle.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                goldMat.color = new Color(0.95f, 0.80f, 0.20f);
                handle.GetComponent<Renderer>().material = goldMat;
            }

            var doorComp = doorGo.GetComponent<RoomEscapeDoor>();
            if (doorComp == null) doorComp = doorGo.AddComponent<RoomEscapeDoor>();
            doorComp.RequiredKeyId = "Level2_Key";
            doorComp.DoorName = "Sanctum Oak Gateway";
            doorComp.DoorHinge = hingeT;
            doorComp.OpenAngle = -85f;
            doorComp.OpenSpeed = 2.5f;

            var trig = doorGo.GetComponent<BoxCollider>();
            if (trig == null) trig = doorGo.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(2.2f, 2.8f, 2.5f);
            trig.center = new Vector3(0f, 1.4f, 0.6f);

            // Construct scenic corridor beyond the gateway
            SetupLevel2SanctumCorridorAndVista();
        }

        private void SetupLevel2SanctumCorridorAndVista()
        {
            if (GameObject.Find("MedievalSanctumCorridorVista") != null) return;

            GameObject corridor = new GameObject("MedievalSanctumCorridorVista");
            corridor.transform.position = Vector3.zero;

            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.25f, 0.27f, 0.33f);

            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.22f, 0.17f, 0.12f);

            // 1. Corridor Stone Flooring (z = 3.8m to 14.5m)
            CreateWall(corridor.transform, "CorridorFloor", new Vector3(0f, -0.02f, 9.2f), new Vector3(3.6f, 0.1f, 10.8f), stoneMat);

            // 2. Side Walls
            CreateWall(corridor.transform, "CorridorWall_Left", new Vector3(-1.8f, 2.2f, 9.2f), new Vector3(0.35f, 4.4f, 10.8f), stoneMat);
            CreateWall(corridor.transform, "CorridorWall_Right", new Vector3(1.8f, 2.2f, 9.2f), new Vector3(0.35f, 4.4f, 10.8f), stoneMat);

            // 3. Ceiling
            CreateWall(corridor.transform, "CorridorCeiling", new Vector3(0f, 3.8f, 9.2f), new Vector3(3.6f, 0.3f, 10.8f), woodMat);

            // 4. Torch Sconces
            CreateTorchSconce(corridor.transform, new Vector3(-1.6f, 2.1f, 6.5f));
            CreateTorchSconce(corridor.transform, new Vector3(1.6f, 2.1f, 6.5f));
            CreateTorchSconce(corridor.transform, new Vector3(-1.6f, 2.1f, 11.5f));
            CreateTorchSconce(corridor.transform, new Vector3(1.6f, 2.1f, 11.5f));

            // 5. Open Archway Balustrade
            CreateWall(corridor.transform, "TerraceBalustrade", new Vector3(0f, 0.65f, 14.5f), new Vector3(5.0f, 1.3f, 0.4f), stoneMat);

            // 6. Sky Vista Quad at z = 15.5m
            GameObject skyVista = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyVista.name = "OutdoorSkyPanorama";
            skyVista.transform.SetParent(corridor.transform, false);
            skyVista.transform.localPosition = new Vector3(0f, 3.8f, 15.5f);
            skyVista.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            skyVista.transform.localScale = new Vector3(20f, 12f, 1f);

            var col = skyVista.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var skyMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            skyMat.color = new Color(0.55f, 0.78f, 1.0f);
            var r = skyVista.GetComponent<Renderer>();
            r.material = skyMat;

            if (DayNightCycle.Instance != null && !DayNightCycle.Instance.WindowBackdrops.Contains(r))
            {
                DayNightCycle.Instance.WindowBackdrops.Add(r);
            }
        }

        private void AlignLevel3Door()
        {
            // Clear any pre-existing blocking solid mesh colliders on the base ExitDoor
            GameObject baseExit = GameObject.Find("ExitDoor");
            if (baseExit != null)
            {
                Collider[] cols = baseExit.GetComponentsInChildren<Collider>(true);
                foreach (var c in cols)
                {
                    c.isTrigger = true; // allow walking through without blocking
                }
            }

            GameObject doorGo = GameObject.Find("CottageExitDoor");
            if (doorGo == null) doorGo = new GameObject("CottageExitDoor");

            doorGo.transform.position = new Vector3(4.05f, 0.0f, -1.35f);
            doorGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.22f, 0.20f, 0.18f);
            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.20f, 0.32f, 0.22f); // mystic dark pine

            // Posts & Lintel
            if (doorGo.transform.Find("Post_L") == null)
            {
                GameObject postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postL.name = "Post_L";
                postL.transform.SetParent(doorGo.transform, false);
                postL.transform.localPosition = new Vector3(-0.95f, 1.35f, 0f);
                postL.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postL.GetComponent<Renderer>().material = stoneMat;

                GameObject postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                postR.name = "Post_R";
                postR.transform.SetParent(doorGo.transform, false);
                postR.transform.localPosition = new Vector3(0.95f, 1.35f, 0f);
                postR.transform.localScale = new Vector3(0.18f, 2.7f, 0.35f);
                postR.GetComponent<Renderer>().material = stoneMat;

                GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Lintel";
                lintel.transform.SetParent(doorGo.transform, false);
                lintel.transform.localPosition = new Vector3(0f, 2.7f, 0f);
                lintel.transform.localScale = new Vector3(2.08f, 0.18f, 0.35f);
                lintel.GetComponent<Renderer>().material = stoneMat;
            }

            Transform hingeT = doorGo.transform.Find("DoorHinge");
            if (hingeT == null)
            {
                GameObject hingeGo = new GameObject("DoorHinge");
                hingeGo.transform.SetParent(doorGo.transform, false);
                hingeGo.transform.localPosition = new Vector3(-0.85f, 0f, 0f);
                hingeT = hingeGo.transform;

                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "DoorLeaf";
                leaf.transform.SetParent(hingeT, false);
                leaf.transform.localPosition = new Vector3(0.85f, 1.3f, 0f);
                leaf.transform.localScale = new Vector3(1.70f, 2.6f, 0.08f);
                leaf.GetComponent<Renderer>().material = woodMat;

                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                handle.name = "DoorHandle";
                handle.transform.SetParent(leaf.transform, false);
                handle.transform.localPosition = new Vector3(0.40f, -0.05f, -0.07f);
                handle.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                goldMat.color = new Color(0.35f, 0.95f, 0.55f);
                handle.GetComponent<Renderer>().material = goldMat;
            }

            var doorComp = doorGo.GetComponent<RoomEscapeDoor>();
            if (doorComp == null) doorComp = doorGo.AddComponent<RoomEscapeDoor>();
            doorComp.RequiredKeyId = "Level3_Key";
            doorComp.DoorName = "Witch's Sanctuary Escape Door";
            doorComp.DoorHinge = hingeT;
            doorComp.OpenAngle = -85f;
            doorComp.OpenSpeed = 2.5f;

            var trig = doorGo.GetComponent<BoxCollider>();
            if (trig == null) trig = doorGo.AddComponent<BoxCollider>();
            trig.isTrigger = true;
            trig.size = new Vector3(2.5f, 2.8f, 2.5f);
            trig.center = new Vector3(0f, 1.4f, 0.6f);

            // Construct outdoor porch & forest pathway
            SetupLevel3ForestPorchAndVista();
        }

        private void SetupLevel3ForestPorchAndVista()
        {
            if (GameObject.Find("CottageOutdoorPorchVista") != null) return;

            GameObject porch = new GameObject("CottageOutdoorPorchVista");
            porch.transform.position = Vector3.zero;

            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.28f, 0.28f, 0.30f);

            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.24f, 0.18f, 0.12f);

            // 1. Outdoor Stone Porch Floor extending outside (x = 3.8m to 12.0m, z = -3.8m to 1.2m)
            CreateWall(porch.transform, "PorchFloor", new Vector3(7.5f, -0.02f, -1.35f), new Vector3(7.5f, 0.1f, 4.0f), stoneMat);

            // 2. Porch Wooden Railing & Posts
            CreateWall(porch.transform, "PorchRailNorth", new Vector3(7.5f, 0.60f, 0.6f), new Vector3(7.5f, 0.9f, 0.15f), woodMat);
            CreateWall(porch.transform, "PorchRailSouth", new Vector3(7.5f, 0.60f, -3.3f), new Vector3(7.5f, 0.9f, 0.15f), woodMat);

            // 3. Lantern Posts lighting the way outside
            CreateTorchSconce(porch.transform, new Vector3(5.5f, 1.8f, 0.4f));
            CreateTorchSconce(porch.transform, new Vector3(5.5f, 1.8f, -3.1f));
            CreateTorchSconce(porch.transform, new Vector3(9.5f, 1.8f, 0.4f));
            CreateTorchSconce(porch.transform, new Vector3(9.5f, 1.8f, -3.1f));

            // 4. Outdoor Moonbeam / Sunbeam pouring onto the porch
            GameObject porchSunGo = new GameObject("PorchSunBeam");
            porchSunGo.transform.SetParent(porch.transform, false);
            porchSunGo.transform.position = new Vector3(8.0f, 5.0f, -1.35f);
            porchSunGo.transform.rotation = Quaternion.Euler(45f, -90f, 0f);
            Light sl = porchSunGo.AddComponent<Light>();
            sl.type = LightType.Spot;
            sl.color = new Color(0.9f, 0.95f, 1f);
            sl.intensity = 2.8f;
            sl.range = 14f;
            sl.spotAngle = 60f;
        }

        private void EnsureMeshColliders()
        {
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                if (mr.GetComponent<Collider>() == null && mr.gameObject.GetComponent<MeshFilter>() != null && mr.gameObject.GetComponent<MeshFilter>().sharedMesh != null)
                {
                    var col = mr.gameObject.AddComponent<MeshCollider>();
                    col.convex = false;
                }
            }
        }

        private void CreateBoxBoundary(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        private AudioClip LoadAudioClip(string clipName)
        {
            AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var c in allClips)
            {
                if (c != null && c.name == clipName)
                    return c;
            }
            foreach (var c in allClips)
            {
                if (c != null && c.name.IndexOf(clipName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c;
            }
            return null;
        }

        private void SetupCastleCorridorAndOutsideVista()
        {
            if (GameObject.Find("CastleCorridorVista") != null) return;

            GameObject corridor = new GameObject("CastleCorridorVista");
            corridor.transform.position = Vector3.zero;

            var stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            stoneMat.color = new Color(0.25f, 0.27f, 0.33f);

            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.color = new Color(0.20f, 0.16f, 0.12f);

            // 1. Corridor Stone Flooring (extends from z = 5.8m doorway to z = 15.2m terrace)
            CreateWall(corridor.transform, "CorridorFloor", new Vector3(0f, -0.02f, 10.5f), new Vector3(3.6f, 0.1f, 9.4f), stoneMat);

            // 2. Corridor Side Walls
            CreateWall(corridor.transform, "CorridorWall_Left", new Vector3(-1.8f, 2.2f, 10.5f), new Vector3(0.35f, 4.4f, 9.4f), stoneMat);
            CreateWall(corridor.transform, "CorridorWall_Right", new Vector3(1.8f, 2.2f, 10.5f), new Vector3(0.35f, 4.4f, 9.4f), stoneMat);

            // 3. Corridor Ceiling
            CreateWall(corridor.transform, "CorridorCeiling", new Vector3(0f, 3.8f, 10.5f), new Vector3(3.6f, 0.3f, 9.4f), woodMat);

            // 4. Torch Sconces lighting the corridor
            CreateTorchSconce(corridor.transform, new Vector3(-1.6f, 2.1f, 8.0f));
            CreateTorchSconce(corridor.transform, new Vector3(1.6f, 2.1f, 8.0f));
            CreateTorchSconce(corridor.transform, new Vector3(-1.6f, 2.1f, 12.5f));
            CreateTorchSconce(corridor.transform, new Vector3(1.6f, 2.1f, 12.5f));

            // 5. Open Archway & Castle Balustrade Terrace at end of hallway (z = 15.2m)
            CreateWall(corridor.transform, "TerraceBalustrade", new Vector3(0f, 0.65f, 15.2f), new Vector3(5.0f, 1.3f, 0.4f), stoneMat);

            // 6. Scenic Outdoor Horizon & Sky Vista Quad at z = 16.5m
            GameObject skyVista = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyVista.name = "OutdoorSkyPanorama";
            skyVista.transform.SetParent(corridor.transform, false);
            skyVista.transform.localPosition = new Vector3(0f, 3.8f, 16.5f);
            skyVista.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            skyVista.transform.localScale = new Vector3(20f, 12f, 1f);

            var col = skyVista.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var skyMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            skyMat.color = new Color(0.6f, 0.82f, 1.0f);
            var r = skyVista.GetComponent<Renderer>();
            r.material = skyMat;

            // Register with DayNightCycle so it receives real-time daylight/sunset/moonlight transitions!
            if (DayNightCycle.Instance != null && !DayNightCycle.Instance.WindowBackdrops.Contains(r))
            {
                DayNightCycle.Instance.WindowBackdrops.Add(r);
            }

            // 7. Outdoor Light pouring through the doorway
            GameObject sunLightGo = new GameObject("OutdoorDoorwaySunBeam");
            sunLightGo.transform.SetParent(corridor.transform, false);
            sunLightGo.transform.position = new Vector3(0f, 3.8f, 15.0f);
            sunLightGo.transform.rotation = Quaternion.Euler(20f, 180f, 0f);
            Light sl = sunLightGo.AddComponent<Light>();
            sl.type = LightType.Spot;
            sl.color = new Color(1f, 0.96f, 0.88f);
            sl.intensity = 3.5f;
            sl.range = 16f;
            sl.spotAngle = 65f;
        }

        private void CreateTorchSconce(Transform parent, Vector3 localPos)
        {
            GameObject torch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            torch.name = "TorchSconce";
            torch.transform.SetParent(parent, false);
            torch.transform.localPosition = localPos;
            torch.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);

            var tMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            tMat.color = new Color(0.2f, 0.15f, 0.1f);
            torch.GetComponent<Renderer>().material = tMat;

            var col = torch.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Flame Sphere
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            flame.transform.SetParent(torch.transform, false);
            flame.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            flame.transform.localScale = new Vector3(1.4f, 1.8f, 1.4f);

            var fMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
            fMat.color = new Color(1f, 0.65f, 0.2f);
            flame.GetComponent<Renderer>().material = fMat;

            var fCol = flame.GetComponent<Collider>();
            if (fCol != null) Destroy(fCol);

            // Light
            GameObject lGo = new GameObject("TorchLight");
            lGo.transform.SetParent(torch.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light tl = lGo.AddComponent<Light>();
            tl.type = LightType.Point;
            tl.color = new Color(1f, 0.62f, 0.22f);
            tl.intensity = 1.6f;
            tl.range = 6.0f;
        }
    }
}
