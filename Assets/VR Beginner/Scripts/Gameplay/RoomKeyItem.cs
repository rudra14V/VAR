using UnityEngine;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Interactive key item for Room Levels.
    /// Emits a soft glowing beacon light, can be grabbed with [E] or on-screen [USE] button,
    /// and unlocks matching RoomEscapeDoors.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoomKeyItem : MonoBehaviour
    {
        [Header("Key Properties")]
        public string KeyId = "Level1_Key";
        public string KeyDisplayName = "Golden Dining Key";
        public AudioClip PickupSound;

        [Header("Visual Feedback")]
        public Color GlowColor = new Color(1f, 0.85f, 0.25f, 1f);
        public float BobSpeed = 1.8f;
        public float BobHeight = 0.05f;
        public float RotateSpeed = 35f;

        private Light m_GlowLight;
        private Vector3 m_OriginalPosition;
        private bool m_IsCollected = false;

        public bool IsCollected => m_IsCollected;

        private void Start()
        {
            m_OriginalPosition = transform.position;

            // Ensure a soft glowing point light exists so player can spot the key
            m_GlowLight = GetComponentInChildren<Light>();
            if (m_GlowLight == null)
            {
                GameObject lightGo = new GameObject("KeyGlowLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.1f;
                m_GlowLight = lightGo.AddComponent<Light>();
                m_GlowLight.type = LightType.Point;
                m_GlowLight.color = GlowColor;
                m_GlowLight.intensity = 1.2f;
                m_GlowLight.range = 2.5f;
            }

            // Ensure Rigidbody exists for physics interaction
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        private void Update()
        {
            // Gentle hovering animation if stationary
            if (!m_IsCollected)
            {
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null && rb.isKinematic)
                {
                    float newY = m_OriginalPosition.y + Mathf.Sin(Time.time * BobSpeed) * BobHeight;
                    transform.position = new Vector3(m_OriginalPosition.x, newY, m_OriginalPosition.z);
                    transform.Rotate(Vector3.up, RotateSpeed * Time.deltaTime, Space.World);
                }
            }
        }

        public void OnPickup()
        {
            m_IsCollected = true;
            if (m_GlowLight != null)
            {
                m_GlowLight.intensity = 0.8f;
            }

            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.NotifyPuzzleProgress($"Picked up: {KeyDisplayName}! Now find the exit door.");
                EscapeRoomManager.Instance.CompleteObjective(1); // Collect Key objective
            }
        }
    }
}
