using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRBeginner.Gameplay;

namespace VRBeginner.Editor
{
    public class EscapeRoomSetupEditor : EditorWindow
    {
        [MenuItem("Tools/Escape Room/Setup Player Mode in Active Scene", false, 5)]
        public static void SetupPlayerModeInScene()
        {
            var scene = SceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Setup Managers
            GameLevelManager glm = Object.FindFirstObjectByType<GameLevelManager>();
            if (glm == null)
            {
                GameObject glmGo = new GameObject("GameLevelManager");
                glm = glmGo.AddComponent<GameLevelManager>();
                Undo.RegisterCreatedObjectUndo(glmGo, "Create GameLevelManager");
            }

            EscapeRoomManager mgr = Object.FindFirstObjectByType<EscapeRoomManager>();
            if (mgr == null)
            {
                GameObject mgrGo = new GameObject("EscapeRoomManager");
                mgr = mgrGo.AddComponent<EscapeRoomManager>();
                Undo.RegisterCreatedObjectUndo(mgrGo, "Create EscapeRoomManager");
            }

            EscapeRoomHUD hud = Object.FindFirstObjectByType<EscapeRoomHUD>();
            if (hud == null)
            {
                GameObject hudGo = new GameObject("EscapeRoomHUD");
                hud = hudGo.AddComponent<EscapeRoomHUD>();
                Undo.RegisterCreatedObjectUndo(hudGo, "Create EscapeRoomHUD");
            }

            EscapeRoomBootstrap boot = Object.FindFirstObjectByType<EscapeRoomBootstrap>();
            if (boot == null)
            {
                GameObject bootGo = new GameObject("EscapeRoomBootstrap");
                boot = bootGo.AddComponent<EscapeRoomBootstrap>();
                Undo.RegisterCreatedObjectUndo(bootGo, "Create EscapeRoomBootstrap");
            }

            // 2. Ensure Room Safety Floor
            GameObject boundaries = GameObject.Find("RoomBoundaries");
            if (boundaries == null)
            {
                boundaries = new GameObject("RoomBoundaries");
                Undo.RegisterCreatedObjectUndo(boundaries, "Create RoomBoundaries");

                CreateBox(boundaries.transform, "SafetyFloor", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 30f));

                if (scene.name.ToLower().Contains("escape") || scene.name.ToLower().Contains("cottage") || scene.name.ToLower().Contains("3"))
                {
                    CreateBox(boundaries.transform, "Wall_West", new Vector3(-8.2f, 2f, 0f), new Vector3(0.5f, 5f, 10f));
                    CreateBox(boundaries.transform, "Wall_East_North", new Vector3(4.8f, 2f, 2.2f), new Vector3(0.5f, 5f, 5f));
                    CreateBox(boundaries.transform, "Wall_East_South", new Vector3(4.8f, 2f, -3.8f), new Vector3(0.5f, 5f, 4f));
                    CreateBox(boundaries.transform, "Wall_North", new Vector3(-1.5f, 2f, 3.8f), new Vector3(15f, 5f, 0.5f));
                    CreateBox(boundaries.transform, "Wall_South", new Vector3(-1.5f, 2f, -3.8f), new Vector3(15f, 5f, 0.5f));
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog("Escape Room Setup", 
                "Player Mode, On-Screen Movement/Jump Buttons, and Menu have been successfully set up in this scene!\n\n" +
                "You can now hit PLAY to walk around with WASD or On-Screen D-Pad, Jump, and access the Menu!", 
                "Awesome!");
        }

        private static void CreateBox(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        [MenuItem("Tools/Escape Room/Controls & Campaign Guide", false, 1)]
        public static void OpenGuide()
        {
            EditorUtility.DisplayDialog("3-Level Escape Room Campaign Guide",
                "HOW TO PLAY:\n\n" +
                "• WASD / On-Screen D-Pad: Move in all directions\n" +
                "• Spacebar / [JUMP] Button: Jump (improved with coyote time & buffer)\n" +
                "• [☰ MENU] Button / Tab: Open In-Game Menu (Controls, Things To Do, Level Select)\n" +
                "• [🔓 CURSOR / 🔒 AIM]: Toggle between Free Mouse and Camera Aim\n" +
                "• E / [USE] Button: Pick up items, unlock doors, press buttons\n" +
                "• Left Click: Throw held item / Shoot magic wand\n" +
                "• Hold Right Click: Rotate/Aim camera or inspect items\n\n" +
                "CAMPAIGN PROGRESSION:\n" +
                "1. Level 1: Vintage Living Room (EASY) - Find Antique Brass Key -> Unlock Dining Passage\n" +
                "2. Level 2: Grand Dining Room (MEDIUM) - Find Golden Dining Key -> Unlock Secret Sanctum Door\n" +
                "3. Level 3: Witch's Cottage (HARD) - Alchemy, Spellbook, Wand, Cauldron, Brew Elixir -> Escape to Forest!\n\n" +
                "Hit PLAY to begin!",
                "Got it!");
        }
    }
}
