using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Master manager for the 3-level escape room campaign.
    /// Manages Level 1 (Living Room - Easy), Level 2 (Dining Room - Medium), and Level 3 (Witch Cottage - Hard).
    /// Handles level transitions, scene loading, progress tracking, and campaign victory.
    /// </summary>
    public class GameLevelManager : MonoBehaviour
    {
        public static GameLevelManager Instance { get; private set; }

        [System.Serializable]
        public class LevelInfo
        {
            public int LevelNumber;
            public string SceneName;
            public string DisplayName;
            public string Difficulty;
            public string Description;
            public Color DifficultyColor;

            public LevelInfo(int num, string scene, string name, string diff, string desc, Color color)
            {
                LevelNumber = num;
                SceneName = scene;
                DisplayName = name;
                Difficulty = diff;
                Description = desc;
                DifficultyColor = color;
            }
        }

        public static readonly LevelInfo[] Levels = new LevelInfo[]
        {
            new LevelInfo(
                1,
                "Level1_DiningRoom",
                "The Grand Dining Room",
                "EASY",
                "Search the grand banquet hall, ignite the sacred candelabras, and claim the Golden Key to the living room.",
                new Color(0.3f, 0.9f, 0.45f) // Emerald Green
            ),
            new LevelInfo(
                2,
                "Level2_MedievalRoom",
                "The Medieval Chamber",
                "MEDIUM",
                "Explore the stone-walled medieval chamber, read the knight's ancient scroll, solve the ironbound chest dial, and claim the Royal Castle Key.",
                new Color(0.95f, 0.75f, 0.2f) // Amber Gold
            ),
            new LevelInfo(
                3,
                "Level3_WitchCottage",
                "The Witch's Alchemical Cottage",
                "HARD",
                "Decipher the ancient spellbook, wield the Arcane Wand, claim the Emerald Runestone Key, and escape into freedom!",
                new Color(0.95f, 0.35f, 0.35f) // Ruby Red
            )
        };

        [Header("Campaign State")]
        public float TotalCampaignTimer = 0f;
        public bool IsCampaignActive = true;
        public bool IsCampaignComplete = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (IsCampaignActive && !IsCampaignComplete)
            {
                TotalCampaignTimer += Time.deltaTime;
            }
        }

        public int GetCurrentLevelIndex()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (currentScene.Equals(Levels[i].SceneName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            // Fallback heuristics for scene names
            if (currentScene.Contains("dining", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("dening", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("1"))
                return 0;
            if (currentScene.Contains("living", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("Demo", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("2"))
                return 1;
            if (currentScene.Contains("escape", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("cottage", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("witch", StringComparison.OrdinalIgnoreCase) || currentScene.Contains("3"))
                return 2;

            return 0;
        }

        public LevelInfo GetCurrentLevelInfo()
        {
            int index = GetCurrentLevelIndex();
            return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
        }

        public void LoadLevel(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= Levels.Length) return;

            string sceneName = Levels[levelIndex].SceneName;
            Debug.Log($"[GameLevelManager] Loading Level {levelIndex + 1}: {sceneName} ({Levels[levelIndex].Difficulty})");

            // Check if scene is in build settings, fallback to path if needed
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                // Fallback to active alternative names
                if (levelIndex == 0)
                {
                    if (Application.CanStreamedLevelBeLoaded("Level1_DiningRoom"))
                        SceneManager.LoadScene("Level1_DiningRoom");
                    else if (Application.CanStreamedLevelBeLoaded("deningRoomScene"))
                        SceneManager.LoadScene("deningRoomScene");
                    else
                        SceneManager.LoadScene(sceneName);
                }
                else if (levelIndex == 1)
                {
                    if (Application.CanStreamedLevelBeLoaded("Level2_MedievalRoom"))
                        SceneManager.LoadScene("Level2_MedievalRoom");
                    else
                        SceneManager.LoadScene(sceneName);
                }
                else
                {
                    if (Application.CanStreamedLevelBeLoaded("Level3_WitchCottage"))
                        SceneManager.LoadScene("Level3_WitchCottage");
                    else if (Application.CanStreamedLevelBeLoaded("EscapeRoom"))
                        SceneManager.LoadScene("EscapeRoom");
                    else
                        SceneManager.LoadScene(sceneName);
                }
            }
        }

        public void LoadNextLevel()
        {
            int current = GetCurrentLevelIndex();
            if (current + 1 < Levels.Length)
            {
                LoadLevel(current + 1);
            }
            else
            {
                // Campaign victory!
                TriggerCampaignVictory();
            }
        }

        public void RestartCurrentLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void TriggerCampaignVictory()
        {
            IsCampaignComplete = true;
            IsCampaignActive = false;

            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.TriggerEscapeVictory();
            }
        }

        public string GetFormattedCampaignTime()
        {
            int minutes = Mathf.FloorToInt(TotalCampaignTimer / 60f);
            int seconds = Mathf.FloorToInt(TotalCampaignTimer % 60f);
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
