using System.Collections;
using UnityEngine;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Interactive exit door for escape rooms.
    /// Can be unlocked with a RoomKeyItem or matching puzzle condition.
    /// Once unlocked, swings open smoothly, clears physical passage colliders,
    /// allows pressing [E] or physically walking through to advance to the next level.
    /// </summary>
    public class RoomEscapeDoor : MonoBehaviour
    {
        [Header("Door Configuration")]
        public string RequiredKeyId = "Level1_Key";
        public string DoorName = "Exit Door";
        public Transform DoorHinge;
        public float OpenAngle = -85f;
        public float OpenSpeed = 2.5f;

        [Header("Audio")]
        public AudioClip UnlockSound;
        public AudioClip DoorOpenSound;
        public AudioClip LevelPassSound;

        [Header("State")]
        public bool IsLocked = true;
        public bool IsOpen = false;

        private Quaternion m_ClosedRotation;
        private Quaternion m_TargetRotation;
        private bool m_TransitionTriggered = false;
        private BoxCollider m_TriggerCollider;

        private void Start()
        {
            if (DoorHinge == null)
            {
                DoorHinge = transform;
            }

            m_ClosedRotation = DoorHinge.localRotation;
            m_TargetRotation = m_ClosedRotation * Quaternion.Euler(0f, OpenAngle, 0f);

            // Ensure there is a dedicated trigger collider for player passage
            m_TriggerCollider = GetComponent<BoxCollider>();
            if (m_TriggerCollider == null)
            {
                m_TriggerCollider = gameObject.AddComponent<BoxCollider>();
            }
            m_TriggerCollider.isTrigger = true;
            if (m_TriggerCollider.size.magnitude < 1.0f)
            {
                m_TriggerCollider.size = new Vector3(2.2f, 2.8f, 2.4f);
                m_TriggerCollider.center = new Vector3(0f, 1.4f, 0.4f);
            }
        }

        private void Update()
        {
            if (m_TransitionTriggered) return;

            var player = EscapeRoomPlayerController.Instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;
            Vector3 doorPos = transform.position;
            float dist = Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(doorPos.x, 0f, doorPos.z));

            // 1. If door is locked, check if player approached with the matching key to unlock seamlessly
            if (IsLocked)
            {
                var interaction = EscapeRoomInteraction.Instance;
                if (interaction != null && interaction.HeldObject != null && dist < 2.2f)
                {
                    var heldKey = interaction.HeldObject.GetComponentInChildren<RoomKeyItem>();
                    if (heldKey != null && (string.IsNullOrEmpty(RequiredKeyId) || heldKey.KeyId == RequiredKeyId))
                    {
                        TryUnlock(heldKey.KeyId, interaction.HeldObject);
                        interaction.ClearHeldObject();
                    }
                }
                return;
            }

            // 2. Proximity & traversal check: if player walks into or through the open doorway, advance!
            if (IsOpen && dist < 1.8f)
            {
                StepThroughDoor();
            }
        }

        public bool TryUnlock(string keyId, GameObject keyObject = null)
        {
            if (!IsLocked) return true;

            if (string.IsNullOrEmpty(RequiredKeyId) || keyId == RequiredKeyId)
            {
                UnlockDoor(keyObject);
                return true;
            }

            return false;
        }

        public void UnlockDoor(GameObject keyObject = null)
        {
            if (!IsLocked) return;

            IsLocked = false;
            IsOpen = true;

            if (keyObject != null)
            {
                Destroy(keyObject, 0.2f);
            }

            // CRITICAL: Clear all solid colliders on the door leaf/children so player CANNOT get blocked!
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                // Leave the trigger collider intact; set all other door leaf colliders to trigger
                // or disable them so the doorway is 100% physically passable with no collision friction
                if (col != m_TriggerCollider)
                {
                    col.isTrigger = true;
                }
            }

            if (UnlockSound != null && SFXPlayer.Instance != null)
            {
                SFXPlayer.Instance.PlaySFX(UnlockSound, transform.position, new SFXPlayer.PlayParameters() { Volume = 0.9f });
            }

            StartCoroutine(AnimateDoorOpen());

            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.CompleteObjective(2); // Unlock Door objective
                EscapeRoomManager.Instance.NotifyPuzzleProgress($"SUCCESS! {DoorName} Unlocked! Step through to go out!");
            }
        }

        private IEnumerator AnimateDoorOpen()
        {
            if (DoorOpenSound != null && SFXPlayer.Instance != null)
            {
                SFXPlayer.Instance.PlaySFX(DoorOpenSound, transform.position, new SFXPlayer.PlayParameters() { Volume = 0.85f });
            }

            float t = 0f;
            Quaternion startRot = DoorHinge.localRotation;

            while (t < 1f)
            {
                t += Time.deltaTime * OpenSpeed;
                DoorHinge.localRotation = Quaternion.Slerp(startRot, m_TargetRotation, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            DoorHinge.localRotation = m_TargetRotation;
        }

        public void StepThroughDoor()
        {
            if (m_TransitionTriggered || IsLocked) return;

            m_TransitionTriggered = true;
            StartCoroutine(TriggerLevelCompleteRoutine());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_TransitionTriggered || IsLocked) return;

            // Check if player entered the unlocked door trigger
            if (other.GetComponent<EscapeRoomPlayerController>() != null || other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                StepThroughDoor();
            }
        }

        private IEnumerator TriggerLevelCompleteRoutine()
        {
            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.CompleteObjective(3); // Step through objective
                EscapeRoomManager.Instance.NotifyPuzzleProgress("OUT OF THE ROOM! Loading next area...");
            }

            // Play celebratory passage sound if available
            if (LevelPassSound != null && SFXPlayer.Instance != null)
            {
                SFXPlayer.Instance.PlaySFX(LevelPassSound, transform.position, new SFXPlayer.PlayParameters() { Volume = 1.0f });
            }

            yield return new WaitForSeconds(0.6f);

            if (GameLevelManager.Instance != null)
            {
                GameLevelManager.Instance.LoadNextLevel();
            }
            else
            {
                // Fallback
                int activeIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                UnityEngine.SceneManagement.SceneManager.LoadScene(activeIndex + 1);
            }
        }
    }
}
