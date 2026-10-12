using System.Collections;
using UnityEngine;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Clockwork puzzle strongbox in Level 2 (Medieval Chamber).
    /// Features a 4-position runic dial (I, II, III, IV).
    /// Aligning the dial to IV (position 4) pops the mechanism open and reveals the Royal Castle Key.
    /// </summary>
    public class ClockworkPuzzleBox : MonoBehaviour
    {
        public static ClockworkPuzzleBox CurrentActiveBox { get; private set; }

        public string BoxName = "Medieval Ironbound Chest";
        public bool IsSolved = false;
        public Transform BoxLid;
        public Transform DialKnob;
        public GameObject KeyToReveal;
        public string KeyDisplayName = "Royal Castle Key";

        [Header("Dial Settings")]
        public int CurrentDial = 1; // 1 to 4
        public int TargetDial = 4;  // 4 = IV

        public static readonly string[] RomanNumerals = { "I", "II", "III", "IV" };

        public string CurrentRomanNumeral => RomanNumerals[Mathf.Clamp(CurrentDial - 1, 0, 3)];

        private Quaternion m_InitialKnobRot;

        private void Awake()
        {
            CurrentActiveBox = this;
            if (DialKnob == null)
            {
                DialKnob = transform.Find("DialKnob") ?? transform;
            }
            if (DialKnob != null)
            {
                m_InitialKnobRot = DialKnob.localRotation;
            }
        }

        private void OnEnable()
        {
            CurrentActiveBox = this;
        }

        public void RotateDial(int delta)
        {
            if (IsSolved) return;

            CurrentDial += delta;
            if (CurrentDial > 4) CurrentDial = 1;
            if (CurrentDial < 1) CurrentDial = 4;

            UpdateDialKnob();
            CheckSolve();
        }

        public void SetDial(int value)
        {
            if (IsSolved) return;

            CurrentDial = Mathf.Clamp(value, 1, 4);
            UpdateDialKnob();
            CheckSolve();
        }

        private void UpdateDialKnob()
        {
            if (DialKnob != null)
            {
                float angle = (CurrentDial - 1) * 90f;
                DialKnob.localRotation = m_InitialKnobRot * Quaternion.Euler(0f, 0f, -angle);
            }
        }

        private void CheckSolve()
        {
            if (CurrentDial == TargetDial)
            {
                SolveMechanism();
            }
            else
            {
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.NotifyPuzzleProgress($"*CLICK* Dial turned to {CurrentRomanNumeral}. Align the secret sigil to unlock!");
                }
            }
        }

        public void SolveMechanism()
        {
            if (IsSolved) return;

            IsSolved = true;
            CurrentDial = TargetDial;
            UpdateDialKnob();

            // Open lid animation
            StartCoroutine(OpenLidRoutine());

            // Reveal key inside / above chest
            if (KeyToReveal != null)
            {
                KeyToReveal.SetActive(true);
            }

            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.NotifyPuzzleProgress($"✨ CLICK! Ancient sigil aligns to IV! The chest unlocks, revealing the {KeyDisplayName}!");
                EscapeRoomManager.Instance.CompleteObjective(1);
            }
        }

        private IEnumerator OpenLidRoutine()
        {
            if (BoxLid != null)
            {
                Quaternion startRot = BoxLid.localRotation;
                Quaternion targetRot = startRot * Quaternion.Euler(-80f, 0f, 0f);
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 3f;
                    BoxLid.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }
        }
    }
}
