using System;
using UnityEngine;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Interactive parchment note / journal in the escape room.
    /// Provides riddles, lore, and clues when inspected with [E] or [USE].
    /// </summary>
    public class RoomClueNote : MonoBehaviour
    {
        [Header("Note Content")]
        public string NoteTitle = "Detective's Field Journal";
        [TextArea(3, 8)]
        public string NoteContent = "📜 THE CLOCKMAKER'S RIDDLE:\n'Time stopped when the grandfather clock chimed IV (4 o'clock).\nAlign the clockwork strongbox dial to IV to release the hidden latch and reveal the Antique Brass Key!'";
        public string ShortHint = "Look at the clockwork box on the side table!";
        public int ObjectiveIndexToComplete = 0;
        public bool HasBeenRead = false;

        private Vector3 m_BaseLocalPos;
        private Quaternion m_BaseLocalRot;
        private bool m_InitializedPos = false;

        private void Start()
        {
            m_BaseLocalPos = transform.localPosition;
            m_BaseLocalRot = transform.localRotation;
            m_InitializedPos = true;

            // Add a subtle golden point light so the note is visible
            if (GetComponentInChildren<Light>() == null)
            {
                GameObject lGo = new GameObject("NoteGlow");
                lGo.transform.SetParent(transform, false);
                lGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                Light l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.9f, 0.5f);
                l.intensity = 1.2f;
                l.range = 1.8f;
            }
        }

        private void Update()
        {
            if (!m_InitializedPos) return;

            // Gentle smooth floating bob and subtle mystical rotation sway
            float bob = Mathf.Sin(Time.time * 1.8f) * 0.012f;
            float sway = Mathf.Sin(Time.time * 1.2f) * 1.5f;
            transform.localPosition = m_BaseLocalPos + new Vector3(0f, bob, 0f);
            transform.localRotation = m_BaseLocalRot * Quaternion.Euler(0f, sway, 0f);
        }

        public void ReadNote()
        {
            HasBeenRead = true;

            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.NotifyPuzzleProgress(NoteContent);
                EscapeRoomManager.Instance.CompleteObjective(ObjectiveIndexToComplete);
            }

            if (EscapeRoomHUD.Instance != null)
            {
                EscapeRoomHUD.Instance.OpenScrollModal(NoteTitle, NoteContent);
            }
        }
    }
}
