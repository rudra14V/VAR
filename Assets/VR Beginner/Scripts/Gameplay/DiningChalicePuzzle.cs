using System;
using UnityEngine;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Interactive banquet candelabra / chalice mechanism in Level 2 (Dining Room).
    /// Lighting all 3 sacred candelabras along the banquet tables unseals the Golden Dining Key!
    /// </summary>
    public class DiningChalicePuzzle : MonoBehaviour
    {
        public string ChaliceName = "Sacred Banquet Candelabra";
        public int ChaliceIndex = 0; // 0, 1, 2
        public bool IsLit = false;
        public Light ChaliceLight;
        public Renderer FlameRenderer;
        public Color LitColor = new Color(1f, 0.72f, 0.2f);

        private static int s_LitCount = 0;
        private static bool s_PuzzleComplete = false;

        public static int LitCount => s_LitCount;
        public static bool IsPuzzleComplete => s_PuzzleComplete;

        public static void ResetPuzzle()
        {
            s_LitCount = 0;
            s_PuzzleComplete = false;
        }

        private void Awake()
        {
            if (ChaliceIndex == 0)
            {
                ResetPuzzle();
            }
        }

        public void Ignite()
        {
            if (IsLit) return;

            IsLit = true;
            s_LitCount++;

            if (ChaliceLight != null)
            {
                ChaliceLight.enabled = true;
                ChaliceLight.color = LitColor;
                ChaliceLight.intensity = 2.4f;
            }

            if (FlameRenderer != null)
            {
                FlameRenderer.material.color = LitColor;
            }

            if (s_LitCount >= 3)
            {
                s_PuzzleComplete = true;
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.NotifyPuzzleProgress("✨ ALL 3 CANDELABRAS IGNITED! The Master's Hearth unseals the Golden Dining Key!");
                    EscapeRoomManager.Instance.CompleteObjective(1);
                }

                // Reveal Golden Key
                RoomKeyItem[] keys = FindObjectsByType<RoomKeyItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var k in keys)
                {
                    if (k.KeyId == "Level1_Key" || k.KeyId == "Level2_Key" || k.name.Contains("GoldenDiningKey") || k.KeyDisplayName.Contains("Golden"))
                    {
                        k.gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.NotifyPuzzleProgress($"🔥 Candelabra {s_LitCount}/3 Ignited! ({3 - s_LitCount} remaining along the banquet tables)");
                }
            }
        }
    }
}
