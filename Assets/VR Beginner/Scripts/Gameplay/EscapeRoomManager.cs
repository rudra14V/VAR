using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Master puzzle manager for the escape room.
    /// Tracks all 6 puzzle stages, quest objectives, game timer, sound effects, hints, and victory state.
    /// </summary>
    public class EscapeRoomManager : MonoBehaviour
    {
        public static EscapeRoomManager Instance { get; private set; }

        [System.Serializable]
        public class PuzzleObjective
        {
            public string Title;
            public string Description;
            public string Hint;
            public bool IsCompleted;

            public PuzzleObjective(string title, string description, string hint)
            {
                Title = title;
                Description = description;
                Hint = hint;
                IsCompleted = false;
            }
        }

        [Header("Audio")]
        public AudioClip ObjectiveCompleteClip;
        public AudioClip VictoryClip;
        public AudioClip NotificationClip;

        [Header("Exit Door Trigger")]
        public Transform ExitDoorThreshold;
        public float EscapeTriggerRadius = 2.2f;

        // Objectives list
        public List<PuzzleObjective> Objectives = new List<PuzzleObjective>();

        // Hints System: 1 for Easy, 2 for Medium, 3 for Hard
        [Header("Hints System")]
        public List<string> CurrentLevelHints = new List<string>();
        private int m_RevealedHintCount = 0;
        private string m_ActiveHintText = "";
        private float m_ActiveHintTimer = 0f;

        public int TotalHintsForLevel => CurrentLevelHints.Count;
        public int RevealedHintCount => m_RevealedHintCount;
        public string ActiveHintText => m_ActiveHintText;
        public bool IsHintActive => m_ActiveHintTimer > 0f;

        private float m_GameTimer = 0f;
        private bool m_IsGameActive = true;
        private bool m_HasEscaped = false;
        private string m_RecentNotification = "";
        private float m_NotificationTimer = 0f;

        // References to scene objects
        private CauldronContent m_Cauldron;
        private PotionReceiver m_DoorLock;
        private GameObject m_ExitDoor;

        public float GameTimer => m_GameTimer;
        public bool HasEscaped => m_HasEscaped;
        public string RecentNotification => m_RecentNotification;
        public bool ShowNotification => m_NotificationTimer > 0f;

        private void Awake()
        {
            Instance = this;
            InitializeObjectives();
        }

        private void Start()
        {
            FindSceneReferences();
            m_GameTimer = 0f;
            m_IsGameActive = true;
            m_HasEscaped = false;

            var activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            if (sceneName.Contains("dining", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("dening", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("1"))
            {
                NotifyPuzzleProgress("Level 1: The Grand Dining Room. Discover the Butler's Banquet Protocol to begin!");
            }
            else if (sceneName.Contains("medieval", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("chamber", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("living", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("demo", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("2"))
            {
                NotifyPuzzleProgress("Level 2: The Medieval Chamber. Inspect the Knight's Ancient Scroll on the council table to begin!");
            }
            else
            {
                NotifyPuzzleProgress("Level 3: The Witch's Cottage! Brew the arcane elixir to escape.");
            }
        }

        private void Update()
        {
            if (m_IsGameActive && !m_HasEscaped)
            {
                m_GameTimer += Time.deltaTime;
                CheckExitTrigger();
            }

            if (m_NotificationTimer > 0f)
            {
                m_NotificationTimer -= Time.deltaTime;
            }

            if (m_ActiveHintTimer > 0f)
            {
                m_ActiveHintTimer -= Time.deltaTime;
            }
        }

        public string RequestNextHint()
        {
            if (CurrentLevelHints.Count == 0) return "";
            if (m_RevealedHintCount < CurrentLevelHints.Count)
            {
                m_RevealedHintCount++;
            }

            int index = Mathf.Clamp(m_RevealedHintCount - 1, 0, CurrentLevelHints.Count - 1);
            m_ActiveHintText = $"💡 HINT ({m_RevealedHintCount}/{CurrentLevelHints.Count}):\n{CurrentLevelHints[index]}";
            m_ActiveHintTimer = 11.0f;
            PlayChime(NotificationClip);
            return m_ActiveHintText;
        }

        public void RevealAllHints()
        {
            m_RevealedHintCount = CurrentLevelHints.Count;
            if (CurrentLevelHints.Count > 0)
            {
                int index = CurrentLevelHints.Count - 1;
                m_ActiveHintText = $"💡 HINT ({CurrentLevelHints.Count}/{CurrentLevelHints.Count}):\n{CurrentLevelHints[index]}";
                m_ActiveHintTimer = 11.0f;
                PlayChime(NotificationClip);
            }
        }

        private void InitializeObjectives()
        {
            Objectives.Clear();
            CurrentLevelHints.Clear();
            m_RevealedHintCount = 0;
            m_ActiveHintText = "";
            m_ActiveHintTimer = 0f;

            var activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;

            if (sceneName.Contains("dining", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("dening", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("1"))
            {
                // Level 1: Grand Dining Room (EASY) - 1 HINT
                CurrentLevelHints.Add("Read the Butler's Banquet Protocol note on the western sideboard, then ignite the 3 Sacred Candelabras (North, East, South) along the banquet tables. When all 3 are lit, collect the unsealed Golden Dining Key from the central table pedestal and unlock the North Exit Door!");

                Objectives.Add(new PuzzleObjective(
                    "Discover Butler's Banquet Protocol Note",
                    "Examine the banquet sideboard table to read the protocol riddle.",
                    "Look on the sideboard for the glowing Butler's protocol note. Press [E] or [USE] to read it."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Ignite the 3 Sacred Banquet Candelabras (0/3)",
                    "Interact with all 3 candelabras along the banquet tables to light them.",
                    "Walk along the dining tables. Find the 3 unlit candelabras (North, East, South) and press [E] to ignite each one."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Collect the Unsealed Golden Dining Key",
                    "Retrieve the Golden Key unsealed by the banquet flames.",
                    "With all 3 candelabras ignited, pick up the glowing Golden Key from the central table pedestal."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Unlock Medieval Chamber Passage Door",
                    "Use the Golden Key on the northern double doors to open the passage to Level 2.",
                    "Carry the Golden Key to the North Door, unlock it with [E], and step through to reach the Medieval Chamber!"
                ));
            }
            else if (sceneName.Contains("medieval", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("chamber", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("living", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("demo", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("2"))
            {
                // Level 2: The Medieval Chamber - 2 HINTS
                CurrentLevelHints.Add("HINT 1 of 2: Approach the central council banquet table and read the illuminated Knight's Decree parchment scroll [E] to discover the secret rune sigil.");
                CurrentLevelHints.Add("HINT 2 of 2: Walk over to the ironbound chest beside the fireplace hearth. Turn the runic dial to align the secret sigil [E] to pop the lid and reveal the floating Royal Castle Key! Pick up the key and unlock the northern Sanctum Oak Gateway [E] to advance to Level 3!");

                Objectives.Add(new PuzzleObjective(
                    "Inspect Knight's Ancient Scroll",
                    "Find the illuminated parchment on the oak council table and press [E] to read the decree.",
                    "Look on the council table for the glowing parchment scroll. Aim at it and press [E] or tap [USE]."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Solve Ironbound Chest Dial Mechanism",
                    "Turn the ancient dial on the ironbound chest beside the hearth to align the crest.",
                    "Interact with the dial on the Medieval Chest near the fireplace. Press [E] to turn the lock mechanism."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Collect Royal Castle Key",
                    "Pick up the glowing Royal Castle Key revealed inside the unsealed chest.",
                    "Pick up the glowing royal key with [E] or [USE] button."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Unlock Sanctum Oak Gateway",
                    "Bring the Royal Key to the heavy ironbound chamber door to open the gateway to the Witch's Cottage (Level 3).",
                    "Carry the Royal Key to the exit gateway, press [E] to unlock, then walk through to advance!"
                ));
            }
            else
            {
                // Level 3: Witch's Alchemical Cottage (HARD) - 3 HINTS
                CurrentLevelHints.Add("HINT 1 of 3: Locate the ancient Book of Spells near the study table and place it onto the angled altar stand to ignite the arcane circle.");
                CurrentLevelHints.Add("HINT 2 of 3: Grab the magic Wand from the unlocked drawer. Pop the cork off your potion flask [E], then gather all 3 mystical ingredients (Meat Slice, Raven Feather, Pumpkin) from the shelves and drop them into the bubbling cauldron.");
                CurrentLevelHints.Add("HINT 3 of 3: Blast the cauldron rune or press the Brew button to create the glowing Arcane Elixir. Pick up the elixir and pour it into the magical lock receptacle beside the door to escape!");

                Objectives.Add(new PuzzleObjective(
                    "Find & Place Book of Spells",
                    "Find the ancient Book of Spells and place it on the altar stand.",
                    "Look near the desk tables for the thick leather-bound magic tome. Place it on the angled wooden book stand."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Open the Cauldron",
                    "Open the cauldron lid using the button or by shooting the target with the Wand.",
                    "The Wand is located near the drawers. Left-click to shoot magic blasts at the cauldron's target, or press [E] on the cauldron lid/button."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Gather 3 Ingredients",
                    "Add Meat Slice, Feather, and Pumpkin into the bubbling Cauldron.",
                    "Check the kitchen counters and shelves. Pick up each ingredient with [E] and drop or toss it into the open cauldron."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Calibrate Cauldron",
                    "Set Temperature to 105°C and Stir Lever to Speed 1.",
                    "Interact with the Cauldron's temperature dial to reach 105°C. Flip the spin lever to activate stirring speed 1."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Brew Unlocker Potion",
                    "Press the Brew button on the Cauldron to spawn the Door Unlocker potion.",
                    "Once ingredients are in and temperature/stir are calibrated, press [E] on the Brew button on the front of the cauldron."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Unlock Arcane Exit Door",
                    "Take the brewed potion, pop the cork [F], and pour it into the Exit Door Lock receptacle.",
                    "Pick up the glowing Unlocker potion bottle from the pedestal, pop its cork with [F], and pour it over the lock receptacle on the exit door."
                ));
                Objectives.Add(new PuzzleObjective(
                    "Escape into the Forest!",
                    "Walk through the open exit door to achieve freedom.",
                    "The massive heavy doors will slide open with mystical power. Walk outside into the forest clearing!"
                ));
            }
        }

        private void FindSceneReferences()
        {
            if (m_Cauldron == null)
                m_Cauldron = FindFirstObjectByType<CauldronContent>();

            if (m_DoorLock == null)
                m_DoorLock = FindFirstObjectByType<PotionReceiver>();

            if (ExitDoorThreshold == null)
            {
                GameObject exitDoor = GameObject.Find("ExitDoor");
                if (exitDoor != null)
                {
                    ExitDoorThreshold = exitDoor.transform;
                }
            }

            // Hook cauldron brew event
            if (m_Cauldron != null)
            {
                m_Cauldron.OnBrew.AddListener(OnCauldronBrewed);
            }

            // Hook door lock event
            if (m_DoorLock != null)
            {
                m_DoorLock.OnPotionPoured.AddListener(OnPotionPoured);
            }
        }

        public void CompleteObjective(int index)
        {
            if (index >= 0 && index < Objectives.Count)
            {
                if (!Objectives[index].IsCompleted)
                {
                    Objectives[index].IsCompleted = true;
                    NotifyPuzzleProgress($"Objective Complete: {Objectives[index].Title}!");
                    PlayChime(ObjectiveCompleteClip != null ? ObjectiveCompleteClip : NotificationClip);
                }
            }
        }

        public void NotifyPuzzleProgress(string message)
        {
            m_RecentNotification = message;
            m_NotificationTimer = 4.0f;
            PlayChime(NotificationClip);
        }

        public void OnItemSocketed(GameObject item, string socketType)
        {
            if (socketType == "Magic Book" || item.name.Contains("BookOfSpells"))
            {
                CompleteObjective(0); // Find & place Book of Spells
            }
            else if (socketType == "Drawer Key" || item.name.Contains("Key"))
            {
                NotifyPuzzleProgress("Secret Drawer Unlocked! Check inside for the Wand.");
            }
        }

        public void OnCauldronOpened()
        {
            CompleteObjective(1); // Open Cauldron
        }

        public void OnIngredientAdded(string ingredientType)
        {
            NotifyPuzzleProgress($"Added {ingredientType.ToUpper()} to the cauldron!");
        }

        public void OnCauldronBrewed(CauldronContent.Recipe recipe)
        {
            CompleteObjective(2); // Ingredients gathered
            CompleteObjective(3); // Calibrated
            CompleteObjective(4); // Brewed Potion

            if (recipe != null)
            {
                NotifyPuzzleProgress("Success! Door Unlocker Potion brewed! Find it on the pedestal.");
            }
            else
            {
                NotifyPuzzleProgress("Recipe failed! Make sure Meat, Feather, Pumpkin are added at 105°C and stir 1.");
            }
        }

        public void OnPotionPoured(string potionType)
        {
            if (potionType.Equals("Unlocker", StringComparison.OrdinalIgnoreCase) || potionType.Contains("Unlock"))
            {
                CompleteObjective(5); // Unlock Exit Door
                NotifyPuzzleProgress("The Arcane Door is opening! Walk outside to escape!");

                RoomEscapeDoor escDoor = FindAnyObjectByType<RoomEscapeDoor>();
                if (escDoor != null)
                {
                    escDoor.UnlockDoor();
                }
            }
        }

        private void CheckExitTrigger()
        {
            if (m_HasEscaped) return;

            // Only evaluate in Level 3 (Witch's Cottage) once the arcane lock has been unsealed by potion
            var activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name.ToLower();
            bool isLevel3 = sceneName.Contains("escape") || sceneName.Contains("cottage") || sceneName.Contains("3");
            if (!isLevel3) return;

            // Must have unlocked the exit door (Objective index 5)
            if (Objectives.Count > 5 && !Objectives[5].IsCompleted)
                return;

            // Check if player has reached the exit threshold outside
            var player = EscapeRoomPlayerController.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            // Exit door is around X = 4.05, Z = -1.5. If player walks past X > 4.5 through the open doorway
            if (playerPos.x > 4.4f && Mathf.Abs(playerPos.z - (-1.5f)) < 3.5f)
            {
                TriggerEscapeVictory();
            }
        }

        public void TriggerEscapeVictory()
        {
            if (m_HasEscaped) return;

            m_HasEscaped = true;
            m_IsGameActive = false;
            CompleteObjective(6); // Escape!

            PlayChime(VictoryClip != null ? VictoryClip : ObjectiveCompleteClip);
            NotifyPuzzleProgress("CONGRATULATIONS! YOU ESCAPED THE ROOM!");

            // Unlock cursor for victory UI
            if (EscapeRoomPlayerController.Instance != null)
            {
                EscapeRoomPlayerController.Instance.SetCursorLock(false);
            }
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public string GetFormattedTimer()
        {
            int minutes = Mathf.FloorToInt(m_GameTimer / 60f);
            int seconds = Mathf.FloorToInt(m_GameTimer % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        public int GetCompletedCount()
        {
            int count = 0;
            foreach (var obj in Objectives)
            {
                if (obj.IsCompleted) count++;
            }
            return count;
        }

        private void PlayChime(AudioClip clip)
        {
            if (clip == null) return;
            if (SFXPlayer.Instance != null)
            {
                SFXPlayer.Instance.PlaySFX(clip, transform.position, new SFXPlayer.PlayParameters()
                {
                    Pitch = 1.0f,
                    Volume = 0.95f,
                    SourceID = -200
                });
            }
        }
    }
}
