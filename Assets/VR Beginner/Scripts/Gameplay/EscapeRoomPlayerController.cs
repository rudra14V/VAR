using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// First-person player controller enabling full PC/Desktop gameplay in all escape rooms.
    /// Supports smooth WASD + Virtual UI button movement in all directions,
    /// robust jump physics with coyote time & jump buffer,
    /// mouse look + Free Cursor mode for UI clicking, sprinting, crouching, and audio feedback.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EscapeRoomPlayerController : MonoBehaviour
    {
        public static EscapeRoomPlayerController Instance { get; private set; }

        [Header("Movement Settings")]
        public float WalkSpeed = 3.4f;
        public float SprintSpeed = 5.8f;
        public float CrouchSpeed = 1.8f;
        public float Acceleration = 14f;
        public float JumpHeight = 0.55f;
        public float Gravity = -19.0f;

        [Header("Jump Physics & Responsiveness")]
        public float CoyoteTimeDuration = 0.2f;
        public float JumpBufferDuration = 0.2f;

        [Header("Look Settings")]
        public float MouseSensitivityX = 0.22f;
        public float MouseSensitivityY = 0.22f;
        public float MinPitch = -80f;
        public float MaxPitch = 80f;
        public bool InvertY = false;

        [Header("Crouch & Height")]
        public float StandHeight = 1.0f;
        public float EyeHeight = 1.0f;
        public float CrouchHeight = 0.60f;
        public float HeightLerpSpeed = 10f;

        [Header("Head Bobbing")]
        public bool EnableHeadBob = true;
        public float BobFrequency = 1.8f;
        public float BobHorizontalAmplitude = 0.03f;
        public float BobVerticalAmplitude = 0.045f;

        [Header("Audio")]
        public AudioClip[] FootstepClips;
        public AudioClip JumpClip;
        public AudioClip LandClip;
        public float FootstepInterval = 0.45f;
        public float FootstepVolume = 0.6f;

        [Header("References")]
        public Camera PlayerCamera;
        public Transform CameraHolder;

        // Virtual Inputs (populated by on-screen UI buttons)
        public Vector2 VirtualMovement { get; set; } = Vector2.zero;
        public bool VirtualSprint { get; set; } = false;
        public bool VirtualCrouch { get; set; } = false;

        private CharacterController m_CharacterController;
        private Vector3 m_Velocity;
        private float m_CurrentSpeed;
        private float m_Pitch = 0f;
        private float m_Yaw = 0f;
        private bool m_IsCrouching = false;
        private float m_BobTimer = 0f;
        private float m_FootstepTimer = 0f;
        private Vector3 m_OriginalCameraLocalPos = new Vector3(0f, 1.0f, 0f);
        private AudioSource m_AudioSource;
        private bool m_IsLocked = true;

        // Jump timers
        private float m_CoyoteTimer = 0f;
        private float m_JumpBufferTimer = 0f;
        private bool m_WasGrounded = true;
        private bool m_JumpRequested = false;

        public bool IsControllable { get; set; } = true;
        public bool IsCrouching => m_IsCrouching;
        public bool IsCursorLocked => m_IsLocked;

        public void SetPlayerHeight(float newHeight)
        {
            EyeHeight = Mathf.Clamp(newHeight, 0.50f, 2.0f);
            StandHeight = EyeHeight;
            CrouchHeight = EyeHeight * 0.60f;
            m_OriginalCameraLocalPos = new Vector3(0f, EyeHeight, 0f);

            if (m_CharacterController != null)
            {
                m_CharacterController.height = m_IsCrouching ? CrouchHeight : StandHeight;
                Vector3 center = m_CharacterController.center;
                center.y = m_CharacterController.height * 0.5f;
                m_CharacterController.center = center;
            }

            if (CameraHolder != null && !m_IsCrouching)
            {
                CameraHolder.localPosition = m_OriginalCameraLocalPos;
            }
        }

        private void Awake()
        {
            Instance = this;
            m_CharacterController = GetComponent<CharacterController>();

            // Setup audio source for footsteps and jump
            m_AudioSource = gameObject.AddComponent<AudioSource>();
            m_AudioSource.spatialBlend = 0f;
            m_AudioSource.playOnAwake = false;

            if (PlayerCamera == null)
            {
                PlayerCamera = GetComponentInChildren<Camera>();
            }

            if (CameraHolder == null && PlayerCamera != null)
            {
                CameraHolder = PlayerCamera.transform;
            }

            m_OriginalCameraLocalPos = new Vector3(0f, EyeHeight, 0f);
            if (CameraHolder != null)
            {
                CameraHolder.localPosition = m_OriginalCameraLocalPos;
                CameraHolder.localRotation = Quaternion.identity;
            }

            m_CurrentSpeed = WalkSpeed;
            m_Yaw = transform.eulerAngles.y;
            m_Pitch = 0f; // Level gaze horizon, not tilted down or up
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
        }

        private void Start()
        {
            // Ensure camera eye height and rotation are level
            m_OriginalCameraLocalPos = new Vector3(0f, EyeHeight, 0f);
            if (CameraHolder != null)
            {
                CameraHolder.localPosition = m_OriginalCameraLocalPos;
                CameraHolder.localRotation = Quaternion.identity;
            }
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);

            // Default to locked cursor and ensure player is controllable
            SetCursorLock(true);
            IsControllable = true;
        }

        private void Update()
        {
            HandleCursorToggle();

            if (!IsControllable)
                return;

            HandleLook();
            HandleCrouch();
            HandleMovement();
            HandleHeadBob();
        }

        public void SetCursorLock(bool locked)
        {
            m_IsLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void ToggleCursorLock()
        {
            SetCursorLock(!m_IsLocked);
        }

        private void HandleCursorToggle()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Alt key toggles cursor mode
            if (kb.altKey.wasPressedThisFrame)
            {
                SetCursorLock(!m_IsLocked);
            }
        }

        private void HandleLook()
        {
            var mouse = Mouse.current;
            if (mouse == null || CameraHolder == null) return;

            // If cursor is unlocked and user clicks inside the viewport (not over UI buttons), re-lock cursor to turn around!
            if (!m_IsLocked && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                Vector2 mousePos = mouse.position.ReadValue();
                float guiY = Screen.height - mousePos.y;
                float guiX = mousePos.x;
                bool isOverTopBar = (guiY < 65f && guiX < 360f) || (guiX > Screen.width * 0.5f - 230f && guiX < Screen.width * 0.5f + 230f && guiY < 60f);
                bool isOverDPad = (guiX < 230f && guiY > Screen.height - 250f);
                bool isOverAction = (guiX > Screen.width - 260f && guiY > Screen.height - 200f);
                if (!isOverTopBar && !isOverDPad && !isOverAction)
                {
                    SetCursorLock(true);
                }
            }

            // Rotate camera if cursor is locked OR if player is holding Right Mouse Button or Left Mouse Button in free cursor mode
            bool shouldRotate = m_IsLocked || mouse.rightButton.isPressed || mouse.leftButton.isPressed;
            if (!shouldRotate) return;

            Vector2 mouseDelta = mouse.delta.ReadValue();
            if (mouseDelta.sqrMagnitude > 0.0001f)
            {
                float mouseX = mouseDelta.x * MouseSensitivityX;
                float mouseY = mouseDelta.y * MouseSensitivityY * (InvertY ? 1f : -1f);

                m_Yaw += mouseX;
                m_Pitch -= mouseY;
                m_Pitch = Mathf.Clamp(m_Pitch, MinPitch, MaxPitch);

                transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
                CameraHolder.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
            }
        }

        private void HandleCrouch()
        {
            var kb = Keyboard.current;
            bool keyboardCrouch = kb != null && (kb.fKey.isPressed || kb.cKey.isPressed || kb.leftCtrlKey.isPressed);
            m_IsCrouching = keyboardCrouch || VirtualCrouch;

            float targetHeight = m_IsCrouching ? CrouchHeight : StandHeight;
            m_CharacterController.height = Mathf.Lerp(m_CharacterController.height, targetHeight, Time.deltaTime * HeightLerpSpeed);

            Vector3 center = m_CharacterController.center;
            center.y = m_CharacterController.height * 0.5f;
            m_CharacterController.center = center;

            if (CameraHolder != null)
            {
                Vector3 targetCamPos = m_OriginalCameraLocalPos;
                if (m_IsCrouching)
                {
                    targetCamPos.y = EyeHeight - (StandHeight - CrouchHeight) * 0.75f;
                }
                CameraHolder.localPosition = Vector3.Lerp(CameraHolder.localPosition, targetCamPos, Time.deltaTime * HeightLerpSpeed);
            }
        }

        public void TriggerJump()
        {
            m_JumpRequested = true;
        }

        private bool CheckIsGrounded()
        {
            if (m_CharacterController.isGrounded) return true;

            // Fallback raycast slightly above feet - filtering out player's own colliders
            Vector3 origin = transform.position + Vector3.up * 0.15f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 0.35f, ~LayerMask.GetMask("Ignore Raycast"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider != null && hits[i].collider != m_CharacterController && !hits[i].collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
            return false;
        }

        private void HandleMovement()
        {
            var kb = Keyboard.current;

            // Combine Keyboard input + Virtual UI button input
            float inputX = VirtualMovement.x;
            float inputZ = VirtualMovement.y;

            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) inputZ += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) inputZ -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) inputX += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) inputX -= 1f;
            }

            inputX = Mathf.Clamp(inputX, -1f, 1f);
            inputZ = Mathf.Clamp(inputZ, -1f, 1f);

            Vector3 moveDirection = (transform.right * inputX + transform.forward * inputZ);
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            // Sprinting / Walking / Crouching speeds
            bool isSprinting = (kb != null && kb.leftShiftKey.isPressed || VirtualSprint) && !m_IsCrouching && inputZ > 0.1f;
            float targetSpeed = m_IsCrouching ? CrouchSpeed : (isSprinting ? SprintSpeed : WalkSpeed);

            if (moveDirection.sqrMagnitude > 0.01f)
            {
                m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, targetSpeed, Time.deltaTime * Acceleration);
            }
            else
            {
                m_CurrentSpeed = Mathf.Lerp(m_CurrentSpeed, 0f, Time.deltaTime * Acceleration * 1.5f);
            }

            Vector3 horizontalMove = moveDirection * m_CurrentSpeed;

            // Ground checking with Coyote Time & Jump Buffer
            bool isGrounded = CheckIsGrounded();

            if (isGrounded)
            {
                m_CoyoteTimer = CoyoteTimeDuration;

                if (!m_WasGrounded && m_Velocity.y < -4f)
                {
                    PlayLandSound();
                }

                if (m_Velocity.y < 0f)
                {
                    m_Velocity.y = -2f; // Sticky downforce
                }
            }
            else
            {
                m_CoyoteTimer -= Time.deltaTime;
                m_Velocity.y += Gravity * Time.deltaTime;
            }

            // Jump input check (Space key or Virtual Jump Button)
            bool spacePressed = kb != null && kb.spaceKey.wasPressedThisFrame;
            if (spacePressed || m_JumpRequested)
            {
                m_JumpBufferTimer = JumpBufferDuration;
                m_JumpRequested = false;
            }
            else
            {
                m_JumpBufferTimer -= Time.deltaTime;
            }

            // Execute jump if buffered and within coyote time window
            if (m_JumpBufferTimer > 0f && m_CoyoteTimer > 0f && !m_IsCrouching)
            {
                m_Velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                m_JumpBufferTimer = 0f;
                m_CoyoteTimer = 0f;
                PlayJumpSound();
            }

            m_WasGrounded = isGrounded;

            // Final move
            Vector3 finalMove = (horizontalMove + Vector3.up * m_Velocity.y) * Time.deltaTime;
            m_CharacterController.Move(finalMove);

            // Anti-fall out of bounds safety check (auto-reground if fell into void)
            if (transform.position.y < -3.0f)
            {
                m_Velocity = Vector3.zero;
                TeleportTo(new Vector3(transform.position.x, 0.15f, transform.position.z), transform.rotation);
            }
            // Anti-roof clamp: Ensure player never glitches on top of ceiling or roof
            else if (transform.position.y > 2.0f)
            {
                m_Velocity = Vector3.zero;
                TeleportTo(new Vector3(transform.position.x, 0.05f, transform.position.z), transform.rotation);
            }

            // Footsteps
            if (isGrounded && moveDirection.sqrMagnitude > 0.05f)
            {
                m_FootstepTimer += Time.deltaTime * (m_CurrentSpeed / WalkSpeed);
                if (m_FootstepTimer >= FootstepInterval)
                {
                    m_FootstepTimer = 0f;
                    PlayFootstep();
                }
            }
            else
            {
                m_FootstepTimer = FootstepInterval * 0.75f;
            }
        }

        private void HandleHeadBob()
        {
            if (!EnableHeadBob || CameraHolder == null) return;

            Vector2 horizontalVel = new Vector2(m_CharacterController.velocity.x, m_CharacterController.velocity.z);
            if (m_CharacterController.isGrounded && horizontalVel.magnitude > 0.1f)
            {
                m_BobTimer += Time.deltaTime * BobFrequency * (m_CurrentSpeed / WalkSpeed);
                float hBob = Mathf.Cos(m_BobTimer) * BobHorizontalAmplitude;
                float vBob = Mathf.Sin(m_BobTimer * 2f) * BobVerticalAmplitude;

                Vector3 p = CameraHolder.localPosition;
                p.x = m_OriginalCameraLocalPos.x + hBob;
                p.y = (m_IsCrouching ? m_OriginalCameraLocalPos.y - (StandHeight - CrouchHeight) * 0.8f : m_OriginalCameraLocalPos.y) + vBob;
                CameraHolder.localPosition = p;
            }
            else if (!m_IsCrouching)
            {
                Vector3 p = CameraHolder.localPosition;
                p.x = Mathf.Lerp(p.x, m_OriginalCameraLocalPos.x, Time.deltaTime * 8f);
                CameraHolder.localPosition = p;
            }
        }

        private void SafePlayOneShot(AudioClip clip, float volume, float pitch = 1.0f)
        {
            if (clip == null || m_AudioSource == null) return;
            m_AudioSource.pitch = pitch;
            m_AudioSource.PlayOneShot(clip, volume);
        }

        private void PlayFootstep()
        {
            if (FootstepClips == null || FootstepClips.Length == 0) return;
            List<AudioClip> valid = new List<AudioClip>();
            for (int i = 0; i < FootstepClips.Length; i++)
            {
                if (FootstepClips[i] != null) valid.Add(FootstepClips[i]);
            }
            if (valid.Count == 0) return;

            AudioClip clip = valid[UnityEngine.Random.Range(0, valid.Count)];
            SafePlayOneShot(clip, FootstepVolume * (m_IsCrouching ? 0.4f : 1.0f), UnityEngine.Random.Range(0.9f, 1.15f));
        }

        private void PlayJumpSound()
        {
            AudioClip clip = JumpClip;
            if (clip == null && FootstepClips != null)
            {
                for (int i = 0; i < FootstepClips.Length; i++)
                {
                    if (FootstepClips[i] != null) { clip = FootstepClips[i]; break; }
                }
            }
            if (clip != null)
            {
                SafePlayOneShot(clip, 0.75f, UnityEngine.Random.Range(1.1f, 1.25f));
            }
        }

        private void PlayLandSound()
        {
            AudioClip clip = LandClip;
            if (clip == null && FootstepClips != null)
            {
                for (int i = 0; i < FootstepClips.Length; i++)
                {
                    if (FootstepClips[i] != null) { clip = FootstepClips[i]; break; }
                }
            }
            if (clip != null)
            {
                SafePlayOneShot(clip, 0.75f, UnityEngine.Random.Range(0.85f, 0.95f));
            }
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            m_CharacterController.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            m_Yaw = rotation.eulerAngles.y;
            m_Pitch = 0f;
            if (CameraHolder != null)
            {
                CameraHolder.localPosition = new Vector3(0f, EyeHeight, 0f);
                CameraHolder.localRotation = Quaternion.identity;
            }
            m_Velocity = Vector3.zero;
            m_CharacterController.enabled = true;
        }
    }
}
