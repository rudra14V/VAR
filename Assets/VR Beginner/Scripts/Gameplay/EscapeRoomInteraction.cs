using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Comprehensive PC/Desktop interaction system for the escape room.
    /// Handles physical grabbing, item inspection, throwing, wand shooting,
    /// potion uncorking and pouring, cauldron controls, drawers, sockets, and buttons.
    /// </summary>
    public class EscapeRoomInteraction : MonoBehaviour
    {
        public static EscapeRoomInteraction Instance { get; private set; }

        [Header("Raycast Settings")]
        public float ReachDistance = 3.2f;
        public LayerMask InteractionLayers = ~0;

        [Header("Hold Settings")]
        public Transform HoldPoint;
        public float DefaultHoldDistance = 1.2f;
        public float MinHoldDistance = 0.6f;
        public float MaxHoldDistance = 2.4f;
        public float HoldLerpSpeed = 16f;
        public float RotateSensitivity = 3.5f;
        public float ThrowForce = 7.5f;

        [Header("Audio")]
        public AudioClip PickupClip;
        public AudioClip DropClip;
        public AudioClip ThrowClip;

        // Current state
        private Camera m_Camera;
        private GameObject m_HeldObject;
        private Rigidbody m_HeldRb;
        private Transform m_HeldOriginalParent;
        private bool m_HeldWasKinematic;
        private float m_CurrentHoldDistance;
        private Quaternion m_HeldRelativeRotation = Quaternion.identity;
        private bool m_IsInspecting = false;
        private bool m_IsPouring = false;

        // Hover target
        private RaycastHit m_CurrentHit;
        private bool m_HasHit;
        private string m_CurrentPrompt = "";
        private string m_CurrentSubPrompt = "";
        private GameObject m_HoveredObject;

        public bool IsHoldingObject => m_HeldObject != null;
        public GameObject HeldObject => m_HeldObject;
        public GameObject HoveredObject => m_HoveredObject;
        public string CurrentPrompt => m_CurrentPrompt;
        public string CurrentSubPrompt => m_CurrentSubPrompt;
        public bool HasInteractiveHover => m_HasHit && !string.IsNullOrEmpty(m_CurrentPrompt);
        public bool IsWandHeld => m_HeldObject != null && m_HeldObject.GetComponentInChildren<ObjectLauncher>() != null;
        public bool IsPotionHeld => m_HeldObject != null && m_HeldObject.GetComponentInChildren<Potion>() != null;

        private void Awake()
        {
            Instance = this;
            m_Camera = GetComponent<Camera>();
            if (m_Camera == null) m_Camera = Camera.main;

            if (HoldPoint == null)
            {
                GameObject hp = new GameObject("PlayerHoldPoint");
                hp.transform.SetParent(transform);
                hp.transform.localPosition = new Vector3(0.25f, -0.2f, DefaultHoldDistance);
                HoldPoint = hp.transform;
            }
            m_CurrentHoldDistance = DefaultHoldDistance;
        }

        private void Update()
        {
            if (EscapeRoomPlayerController.Instance != null && !EscapeRoomPlayerController.Instance.IsControllable)
                return;

            UpdateRaycast();
            HandleHeldItemControls();
            HandleInteractionInput();
        }

        private void LateUpdate()
        {
            UpdateHeldObjectPosition();
        }

        private void UpdateRaycast()
        {
            m_CurrentPrompt = "";
            m_CurrentSubPrompt = "";
            m_HoveredObject = null;

            Ray ray = m_Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            m_HasHit = Physics.Raycast(ray, out m_CurrentHit, ReachDistance, InteractionLayers, QueryTriggerInteraction.Collide);

            if (m_HeldObject != null)
            {
                // When holding an item, check if looking at sockets or receivers
                ConfigureHeldPrompts();
                return;
            }

            if (!m_HasHit || m_CurrentHit.collider == null)
                return;

            m_HoveredObject = m_CurrentHit.collider.gameObject;
            DetermineHoverPrompt(m_CurrentHit.collider);
        }

        private void ConfigureHeldPrompts()
        {
            Potion potion = m_HeldObject.GetComponentInChildren<Potion>();
            ObjectLauncher launcher = m_HeldObject.GetComponentInChildren<ObjectLauncher>();
            string itemName = CleanGameObjectName(m_HeldObject.name);

            if (launcher != null)
            {
                m_CurrentPrompt = $"[Left Click] Cast Magic Blast with {itemName}";
                m_CurrentSubPrompt = "[E] Drop Wand | [Hold Right Click] Aim / Inspect";
                return;
            }

            if (potion != null)
            {
                // Check if cork is in or off
                bool plugIn = IsPotionPlugged(potion);
                if (plugIn)
                {
                    m_CurrentPrompt = "[E] Pop Cork Off";
                    m_CurrentSubPrompt = "[Hold Right Click] Inspect | [Left Click] Pop Cork";
                }
                else
                {
                    // Check if looking at door lock receptacle
                    if (m_HasHit && m_CurrentHit.collider != null)
                    {
                        PotionReceiver receiver = m_CurrentHit.collider.GetComponentInParent<PotionReceiver>();
                        if (receiver != null)
                        {
                            m_CurrentPrompt = "[E] Pour Potion into Arcane Lock";
                            m_CurrentSubPrompt = "[Hold Right Click] Tilt & Pour Manually";
                            return;
                        }

                        // Check if looking at cauldron
                        CauldronContent cauldron = m_CurrentHit.collider.GetComponentInParent<CauldronContent>();
                        if (cauldron != null)
                        {
                            m_CurrentPrompt = "[Hold Right Click] Tilt & Pour into Cauldron";
                            m_CurrentSubPrompt = "[E] Drop Potion";
                            return;
                        }
                    }

                    m_CurrentPrompt = "[Hold Right Click] / [Space] Tilt & Pour Potion";
                    m_CurrentSubPrompt = "[E] Drop Potion | [Left Click] Throw";
                }
                return;
            }

            // Check if holding BookOfSpells near BookHolder
            if (m_HeldObject.name.Contains("BookOfSpells") || m_HeldObject.GetComponent<SocketTarget>() != null)
            {
                if (m_HasHit && m_CurrentHit.collider != null)
                {
                    var socket = m_CurrentHit.collider.GetComponentInParent<XRExclusiveSocketInteractor>();
                    if (socket != null)
                    {
                        m_CurrentPrompt = "[E] Place on Pedestal Stand";
                        m_CurrentSubPrompt = "[Left Click] Throw | [E] Drop";
                        return;
                    }
                }
            }

            // Check if holding RoomKeyItem near RoomEscapeDoor
            RoomKeyItem heldKeyItem = m_HeldObject.GetComponentInChildren<RoomKeyItem>();
            if (heldKeyItem != null && m_HasHit && m_CurrentHit.collider != null)
            {
                RoomEscapeDoor nearDoor = m_CurrentHit.collider.GetComponentInParent<RoomEscapeDoor>();
                if (nearDoor != null)
                {
                    m_CurrentPrompt = $"[E] Unlock {nearDoor.DoorName} with {heldKeyItem.KeyDisplayName}";
                    m_CurrentSubPrompt = "Press [E] or tap [USE] to unlock";
                    return;
                }
            }

            // Check if holding Key near desk key socket
            if (m_HeldObject.name.Contains("Key"))
            {
                if (m_HasHit && m_CurrentHit.collider != null)
                {
                    var socket = m_CurrentHit.collider.GetComponentInParent<XRExclusiveSocketInteractor>();
                    if (socket != null && socket.AcceptedType == "Drawer Key")
                    {
                        m_CurrentPrompt = "[E] Insert Key to Unlock Drawer";
                        m_CurrentSubPrompt = "[E] Drop Key";
                        return;
                    }
                }
            }

            // Default held prompt
            m_CurrentPrompt = $"Holding {itemName}";
            m_CurrentSubPrompt = "[E] Drop | [Left Click] Throw | [Hold Right Click] Inspect / Rotate";
        }

        private void DetermineHoverPrompt(Collider col)
        {
            // 0. Room Key & Room Escape Door
            RoomKeyItem keyItem = col.GetComponentInParent<RoomKeyItem>();
            if (keyItem != null)
            {
                m_CurrentPrompt = $"[E] Pick up {keyItem.KeyDisplayName}";
                m_CurrentSubPrompt = "Take this key to unlock the exit door";
                return;
            }

            RoomEscapeDoor escDoor = col.GetComponentInParent<RoomEscapeDoor>();
            if (escDoor != null)
            {
                if (escDoor.IsLocked)
                {
                    if (m_HeldObject != null && m_HeldObject.GetComponentInChildren<RoomKeyItem>() != null)
                    {
                        m_CurrentPrompt = $"[E] Unlock {escDoor.DoorName} with Key";
                        m_CurrentSubPrompt = "Press [E] or tap [USE] to unlock";
                    }
                    else
                    {
                        m_CurrentPrompt = $"{escDoor.DoorName} [LOCKED]";
                        m_CurrentSubPrompt = "Search the room to find the key!";
                    }
                }
                else
                {
                    m_CurrentPrompt = $"[E] Walk through {escDoor.DoorName} to Next Level";
                    m_CurrentSubPrompt = "Step through the doorway to advance";
                }
                return;
            }

            // Clue Notes / Parchments
            RoomClueNote clueNote = col.GetComponentInParent<RoomClueNote>();
            if (clueNote != null)
            {
                m_CurrentPrompt = $"[E] Read {clueNote.NoteTitle}";
                m_CurrentSubPrompt = clueNote.ShortHint;
                return;
            }

            // Clockwork Strongbox Mechanism (Level 2)
            ClockworkPuzzleBox puzzleBox = col.GetComponentInParent<ClockworkPuzzleBox>();
            if (puzzleBox != null)
            {
                if (puzzleBox.IsSolved)
                {
                    m_CurrentPrompt = $"{puzzleBox.BoxName} [UNLOCKED]";
                    m_CurrentSubPrompt = "The mechanism is open. Collect the Royal Key!";
                }
                else
                {
                    m_CurrentPrompt = $"[E] Turn Dial on {puzzleBox.BoxName} [{puzzleBox.CurrentRomanNumeral}]";
                    m_CurrentSubPrompt = $"Current Dial: {puzzleBox.CurrentRomanNumeral} | Align the secret sigil to unlock | Press [1-4] or use On-Screen Dial";
                }
                return;
            }

            // Banquet Candelabras (Level 2)
            DiningChalicePuzzle chalice = col.GetComponentInParent<DiningChalicePuzzle>();
            if (chalice != null)
            {
                if (chalice.IsLit)
                {
                    m_CurrentPrompt = $"{chalice.ChaliceName} [IGNITED]";
                    m_CurrentSubPrompt = $"Active ({DiningChalicePuzzle.LitCount}/3 lit)";
                }
                else
                {
                    m_CurrentPrompt = $"[E] Ignite {chalice.ChaliceName}";
                    m_CurrentSubPrompt = $"Light all 3 to unseal Golden Key ({DiningChalicePuzzle.LitCount}/3)";
                }
                return;
            }

            // 1. ButtonInteractable (e.g. Cauldron Open button, Brew button)
            ButtonInteractable btn = col.GetComponentInParent<ButtonInteractable>();
            if (btn != null)
            {
                string btnName = btn.gameObject.name.ToLower();
                if (btnName.Contains("brew"))
                    m_CurrentPrompt = "[E] Brew Potion";
                else if (btnName.Contains("open") || btnName.Contains("close"))
                    m_CurrentPrompt = "[E] Open / Close Cauldron Lid";
                else
                    m_CurrentPrompt = "[E] Press Button";
                return;
            }

            // 2. Cauldron elements
            CauldronContent cauldron = col.GetComponentInParent<CauldronContent>();
            if (cauldron != null)
            {
                string objName = col.gameObject.name.ToLower();
                if (objName.Contains("lever") || objName.Contains("spin"))
                {
                    m_CurrentPrompt = "[E] Stir Cauldron Lever (Speed: 1)";
                    return;
                }
                if (objName.Contains("button") || objName.Contains("brew"))
                {
                    m_CurrentPrompt = "[E] Brew Potion";
                    return;
                }
                if (objName.Contains("open") || objName.Contains("lid") || objName.Contains("trigger"))
                {
                    m_CurrentPrompt = "[E] Open Cauldron Lid";
                    return;
                }
            }

            // 3. DialInteractable (Temperature dial, radio knob)
            DialInteractable dial = col.GetComponentInParent<DialInteractable>();
            if (dial != null)
            {
                string dialName = dial.gameObject.name.ToLower();
                if (dialName.Contains("temp") || dial.transform.parent != null && dial.transform.parent.name.ToLower().Contains("cauldron"))
                {
                    m_CurrentPrompt = "[E] / [Scroll] Adjust Temperature (Target: 105°C)";
                    return;
                }
                if (dial.GetComponentInParent<Radio>() != null)
                {
                    m_CurrentPrompt = "[E] Tune Radio Station";
                    return;
                }
                m_CurrentPrompt = "[E] Turn Dial";
                return;
            }

            // 4. AxisDragInteractable (Drawers, levers)
            AxisDragInteractable drag = col.GetComponentInParent<AxisDragInteractable>();
            if (drag != null)
            {
                string dragName = drag.gameObject.name.ToLower();
                if (dragName.Contains("drawer"))
                {
                    m_CurrentPrompt = "[E] Open / Close Drawer";
                    return;
                }
                if (dragName.Contains("lever") || dragName.Contains("spin"))
                {
                    m_CurrentPrompt = "[E] Toggle Stir Lever (Speed: 1)";
                    return;
                }
                m_CurrentPrompt = "[E] Pull / Push Interactable";
                return;
            }

            // 5. Radio
            Radio radio = col.GetComponentInParent<Radio>();
            if (radio != null)
            {
                m_CurrentPrompt = "[E] Toggle Radio Music";
                return;
            }

            // 6. Chest
            if (col.gameObject.name.Contains("Chest") || col.transform.parent != null && col.transform.parent.name.Contains("Chest"))
            {
                m_CurrentPrompt = "[E] Open / Close Chest Lid";
                return;
            }

            // 7. Exit Door Lock Receptacle
            PotionReceiver receiver = col.GetComponentInParent<PotionReceiver>();
            if (receiver != null)
            {
                m_CurrentPrompt = "Exit Door Arcane Lock [Requires Unlocker Potion]";
                return;
            }

            // 8. Book Holder Stand
            XRExclusiveSocketInteractor socket = col.GetComponentInParent<XRExclusiveSocketInteractor>();
            if (socket != null)
            {
                if (socket.AcceptedType == "Magic Book")
                {
                    m_CurrentPrompt = "Altar Pedestal [Place Book of Spells Here]";
                    return;
                }
                if (socket.AcceptedType == "Drawer Key")
                {
                    m_CurrentPrompt = "Drawer Keyhole [Requires Drawer Key]";
                    return;
                }
                m_CurrentPrompt = "Pedestal Socket";
                return;
            }

            // 9. Grabbable Objects (XROffsetGrabbable, CauldronIngredient, Potion, Wand, Book, Key, Props)
            XROffsetGrabbable grabbable = col.GetComponentInParent<XROffsetGrabbable>();
            Rigidbody rb = col.attachedRigidbody;

            if (grabbable != null || (rb != null && !rb.isKinematic))
            {
                GameObject rootGo = grabbable != null ? grabbable.gameObject : rb.gameObject;
                string cleanName = CleanGameObjectName(rootGo.name);
                m_CurrentPrompt = $"[E] Pick up {cleanName}";

                CauldronIngredient ing = rootGo.GetComponentInChildren<CauldronIngredient>();
                if (ing != null)
                {
                    m_CurrentSubPrompt = $"Cauldron Ingredient: {ing.IngredientType.ToUpper()}";
                }
                else if (rootGo.GetComponentInChildren<Potion>() != null)
                {
                    m_CurrentSubPrompt = "Magical Alchemy Potion";
                }
                else if (rootGo.GetComponentInChildren<ObjectLauncher>() != null)
                {
                    m_CurrentSubPrompt = "Magical Blast Wand";
                }
                return;
            }
        }

        private void HandleHeldItemControls()
        {
            if (m_HeldObject == null) return;

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            // Scroll wheel adjust distance
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                m_CurrentHoldDistance = Mathf.Clamp(m_CurrentHoldDistance + Mathf.Sign(scroll) * 0.15f, MinHoldDistance, MaxHoldDistance);
            }

            // Inspect / Rotate Mode (Hold Right Mouse Button or R)
            m_IsInspecting = (mouse.rightButton.isPressed || kb.rKey.isPressed) && !IsPotionHeld;
            if (m_IsInspecting)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Quaternion rotX = Quaternion.AngleAxis(-delta.y * RotateSensitivity, m_Camera.transform.right);
                Quaternion rotY = Quaternion.AngleAxis(-delta.x * RotateSensitivity, m_Camera.transform.up);
                m_HeldRelativeRotation = rotY * rotX * m_HeldRelativeRotation;
            }

            // Wand Spellcast (Left Click)
            if (IsWandHeld && mouse.leftButton.wasPressedThisFrame && !m_IsInspecting)
            {
                CastWandSpell();
                return;
            }

            // Potion Uncork (E Key or Left Click)
            if (IsPotionHeld && (kb.eKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame))
            {
                Potion pot = m_HeldObject.GetComponentInChildren<Potion>();
                if (pot != null && IsPotionPlugged(pot))
                {
                    pot.PlugOff();
                    return;
                }
            }

            // Potion Tilt & Pour (Hold Right Mouse Button)
            if (IsPotionHeld)
            {
                m_IsPouring = mouse.rightButton.isPressed;
            }
            else
            {
                m_IsPouring = false;
            }

            // Throw item (Left Click when not wand and not inspecting)
            if (!IsWandHeld && !m_IsInspecting && mouse.leftButton.wasPressedThisFrame)
            {
                ThrowHeldObject();
                return;
            }
        }

        private void HandleInteractionInput()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                TriggerInteract();
            }
        }

        public void TriggerInteract()
        {
            // If holding an object
            if (m_HeldObject != null)
            {
                // Check if holding key near escape door
                if (m_HasHit && m_CurrentHit.collider != null)
                {
                    RoomEscapeDoor door = m_CurrentHit.collider.GetComponentInParent<RoomEscapeDoor>();
                    RoomKeyItem heldKey = m_HeldObject.GetComponentInChildren<RoomKeyItem>();
                    if (door != null && heldKey != null)
                    {
                        if (door.TryUnlock(heldKey.KeyId, m_HeldObject))
                        {
                            m_HeldObject = null;
                            m_HeldRb = null;
                            return;
                        }
                    }
                }

                if (TrySocketHeldObject())
                    return;

                if (TryPourPotionIntoReceiver())
                    return;

                // Otherwise drop held object
                DropHeldObject();
                return;
            }

            // If NOT holding an object, interact with hovered object
            if (m_HasHit && m_CurrentHit.collider != null)
            {
                InteractWithWorldObject(m_CurrentHit.collider);
            }
        }

        private void UpdateHeldObjectPosition()
        {
            if (m_HeldObject == null || HoldPoint == null) return;

            Vector3 targetPosition = m_Camera.transform.position + m_Camera.transform.forward * m_CurrentHoldDistance;

            // Offset slightly down and right for realistic hand hold
            targetPosition += m_Camera.transform.right * 0.22f - m_Camera.transform.up * 0.18f;

            // If pouring, tilt the bottle downwards!
            Quaternion targetRotation = m_Camera.transform.rotation * m_HeldRelativeRotation;
            if (m_IsPouring)
            {
                // Tilt forward by 125 degrees so bottle opening points down
                targetRotation = Quaternion.Euler(115f, m_Camera.transform.eulerAngles.y, 0f);
            }

            m_HeldObject.transform.position = Vector3.Lerp(m_HeldObject.transform.position, targetPosition, Time.deltaTime * HoldLerpSpeed);
            m_HeldObject.transform.rotation = Quaternion.Slerp(m_HeldObject.transform.rotation, targetRotation, Time.deltaTime * HoldLerpSpeed);
        }

        public void PickUpObject(GameObject obj)
        {
            if (obj == null) return;

            m_HeldObject = obj;
            m_HeldRb = obj.GetComponent<Rigidbody>();
            m_HeldOriginalParent = obj.transform.parent;

            if (m_HeldRb != null)
            {
                m_HeldWasKinematic = m_HeldRb.isKinematic;
                if (!m_HeldRb.isKinematic)
                {
                    m_HeldRb.linearVelocity = Vector3.zero;
                    m_HeldRb.angularVelocity = Vector3.zero;
                }
                m_HeldRb.isKinematic = true;
                m_HeldRb.useGravity = false;
            }

            m_HeldRelativeRotation = Quaternion.Inverse(m_Camera.transform.rotation) * obj.transform.rotation;
            m_CurrentHoldDistance = DefaultHoldDistance;

            // Trigger XR SelectEnter if applicable
            var grabbable = obj.GetComponent<XROffsetGrabbable>();
            if (grabbable != null)
            {
                // Highlight or notify
            }

            PlaySFX(PickupClip, 1.0f);
        }

        public void DropHeldObject()
        {
            if (m_HeldObject == null) return;

            if (m_HeldRb != null)
            {
                m_HeldRb.isKinematic = m_HeldWasKinematic;
                if (!m_HeldWasKinematic)
                {
                    m_HeldRb.useGravity = true;
                    m_HeldRb.linearVelocity = Vector3.down * 0.5f;
                }
            }

            m_HeldObject.transform.SetParent(m_HeldOriginalParent);
            m_HeldObject = null;
            m_HeldRb = null;

            PlaySFX(DropClip, 0.9f);
        }

        public void ClearHeldObject()
        {
            m_HeldObject = null;
            m_HeldRb = null;
        }

        public void ThrowHeldObject()
        {
            if (m_HeldObject == null) return;

            GameObject thrown = m_HeldObject;
            Rigidbody rb = m_HeldRb;
            bool wasKinematic = m_HeldWasKinematic;

            m_HeldObject = null;
            m_HeldRb = null;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                Vector3 throwVector = m_Camera.transform.forward * ThrowForce + m_Camera.transform.up * 1.5f;
                rb.linearVelocity = throwVector;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
            }

            thrown.transform.SetParent(m_HeldOriginalParent);
            PlaySFX(ThrowClip != null ? ThrowClip : DropClip, 1.1f);
        }

        private void CastWandSpell()
        {
            if (m_HeldObject == null) return;

            ObjectLauncher launcher = m_HeldObject.GetComponentInChildren<ObjectLauncher>();
            if (launcher != null)
            {
                launcher.SpawnPoint.rotation = m_Camera.transform.rotation;
                launcher.Activated();

                // Check if aimed directly at cauldron trigger
                if (m_HasHit && m_CurrentHit.collider != null)
                {
                    MagicReceiver receiver = m_CurrentHit.collider.GetComponentInParent<MagicReceiver>();
                    if (receiver != null)
                    {
                        receiver.OnMagicCollision.Invoke();
                    }
                }
            }
        }

        private bool TrySocketHeldObject()
        {
            if (m_HeldObject == null || !m_HasHit || m_CurrentHit.collider == null)
                return false;

            XRExclusiveSocketInteractor socket = m_CurrentHit.collider.GetComponentInParent<XRExclusiveSocketInteractor>();
            if (socket == null) return false;

            SocketTarget target = m_HeldObject.GetComponentInChildren<SocketTarget>();
            if (target != null && target.SocketType == socket.AcceptedType)
            {
                // Place into socket
                Transform attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                m_HeldObject.transform.position = attach.position;
                m_HeldObject.transform.rotation = attach.rotation;

                if (m_HeldRb != null)
                {
                    m_HeldRb.isKinematic = true;
                    m_HeldRb.useGravity = false;
                }

                m_HeldObject.transform.SetParent(socket.transform);
                socket.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());

                // Notify manager
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.OnItemSocketed(m_HeldObject, socket.AcceptedType);
                }

                m_HeldObject = null;
                m_HeldRb = null;
                PlaySFX(PickupClip, 1.2f);
                return true;
            }

            return false;
        }

        private bool TryPourPotionIntoReceiver()
        {
            if (m_HeldObject == null || !m_HasHit || m_CurrentHit.collider == null)
                return false;

            Potion potion = m_HeldObject.GetComponentInChildren<Potion>();
            if (potion == null) return false;

            PotionReceiver receiver = m_CurrentHit.collider.GetComponentInParent<PotionReceiver>();
            if (receiver != null)
            {
                // Pop cork if needed
                potion.PlugOff();

                // Smoothly tilt and pour
                StartCoroutine(PourPotionRoutine(potion, receiver));
                return true;
            }

            return false;
        }

        private IEnumerator PourPotionRoutine(Potion potion, PotionReceiver receiver)
        {
            float timer = 0f;
            m_IsPouring = true;
            while (timer < 2.0f)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            m_IsPouring = false;

            receiver.ReceivePotion(potion.PotionType);
            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.OnPotionPoured(potion.PotionType);
            }
        }

        private void InteractWithWorldObject(Collider col)
        {
            // 0. RoomKeyItem pickup
            RoomKeyItem key = col.GetComponentInParent<RoomKeyItem>();
            if (key != null)
            {
                PickUpObject(key.gameObject);
                key.OnPickup();
                return;
            }

            // Room Clue Note reading
            RoomClueNote clue = col.GetComponentInParent<RoomClueNote>();
            if (clue != null)
            {
                clue.ReadNote();
                if (EscapeRoomHUD.Instance != null)
                {
                    EscapeRoomHUD.Instance.OpenScrollModal(clue.NoteTitle, clue.NoteContent);
                }
                PlaySFX(DropClip != null ? DropClip : PickupClip, 1.0f);
                return;
            }

            // Clockwork Puzzle Box (Chest Dial)
            ClockworkPuzzleBox pBox = col.GetComponentInParent<ClockworkPuzzleBox>();
            if (pBox != null)
            {
                pBox.RotateDial(1);
                PlaySFX(PickupClip, 1.2f);
                return;
            }

            // Dining Chalice / Candelabra
            DiningChalicePuzzle chalice = col.GetComponentInParent<DiningChalicePuzzle>();
            if (chalice != null)
            {
                chalice.Ignite();
                PlaySFX(ThrowClip != null ? ThrowClip : PickupClip, 1.2f);
                return;
            }

            // RoomEscapeDoor interaction
            RoomEscapeDoor door = col.GetComponentInParent<RoomEscapeDoor>();
            if (door != null)
            {
                if (door.IsLocked)
                {
                    if (EscapeRoomManager.Instance != null)
                    {
                        EscapeRoomManager.Instance.NotifyPuzzleProgress("This door is locked! Find the key to unlock it.");
                    }
                }
                else
                {
                    // Door is open - step through and advance to next level!
                    door.StepThroughDoor();
                }
                return;
            }

            // 1. ButtonInteractable
            ButtonInteractable btn = col.GetComponentInParent<ButtonInteractable>();
            if (btn != null)
            {
                btn.OnButtonPressed.Invoke();
                return;
            }

            // 2. Cauldron
            CauldronContent cauldron = col.GetComponentInParent<CauldronContent>();
            if (cauldron != null)
            {
                string objName = col.gameObject.name.ToLower();
                if (objName.Contains("open") || objName.Contains("lid") || objName.Contains("trigger"))
                {
                    cauldron.Open();
                    return;
                }
                if (objName.Contains("brew") || objName.Contains("button"))
                {
                    cauldron.Brew();
                    return;
                }
                if (objName.Contains("spin") || objName.Contains("lever"))
                {
                    ToggleCauldronRotation(cauldron);
                    return;
                }
            }

            // 3. DialInteractable
            DialInteractable dial = col.GetComponentInParent<DialInteractable>();
            if (dial != null)
            {
                AdvanceDialStep(dial);
                return;
            }

            // 4. AxisDragInteractable (Drawers, etc.)
            AxisDragInteractable drag = col.GetComponentInParent<AxisDragInteractable>();
            if (drag != null)
            {
                ToggleAxisDrag(drag);
                return;
            }

            // 5. Radio
            Radio radio = col.GetComponentInParent<Radio>();
            if (radio != null)
            {
                ToggleRadioState(radio);
                return;
            }

            // 6. Chest
            if (col.gameObject.name.Contains("Chest") || col.transform.parent != null && col.transform.parent.name.Contains("Chest"))
            {
                ToggleChestLid(col.gameObject);
                return;
            }

            // 7. Pick up Grabbable
            XROffsetGrabbable grabbable = col.GetComponentInParent<XROffsetGrabbable>();
            Rigidbody rb = col.attachedRigidbody;
            if (grabbable != null)
            {
                PickUpObject(grabbable.gameObject);
                return;
            }
            if (rb != null && !rb.isKinematic)
            {
                PickUpObject(rb.gameObject);
                return;
            }
        }

        private void ToggleCauldronRotation(CauldronContent cauldron)
        {
            // Lever step 1 is required for the DoorUnlocker recipe
            cauldron.ChangeRotation(2); // rotation step 1
            if (EscapeRoomManager.Instance != null)
            {
                EscapeRoomManager.Instance.NotifyPuzzleProgress("Cauldron stir speed set to 1!");
            }
        }

        private void AdvanceDialStep(DialInteractable dial)
        {
            int nextStep = (dial.CurrentStep + 1);
            if (dial.Steps > 0)
                nextStep = nextStep % (dial.Steps + 1);
            else
                nextStep = (nextStep % 10);

            dial.OnDialStepChanged.Invoke(nextStep);
            dial.OnDialChanged.Invoke(dial);

            // Also check cauldron temperature
            CauldronContent cauldron = dial.GetComponentInParent<CauldronContent>();
            if (cauldron != null)
            {
                cauldron.ChangeTemperature(7); // 7 * 15 = 105°C (The exact required temperature!)
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.NotifyPuzzleProgress("Cauldron temperature set to 105°C!");
                }
            }
        }

        private void ToggleAxisDrag(AxisDragInteractable drag)
        {
            // Toggle drawer open / close
            int targetStep = drag.Steps > 0 ? (drag.Steps) : 1;
            drag.OnDragStep.Invoke(targetStep);
            drag.OnDragDistance.Invoke(drag.AxisLength);

            // Animate position along axis
            StartCoroutine(AnimateAxisDrag(drag));
        }

        private IEnumerator AnimateAxisDrag(AxisDragInteractable drag)
        {
            Vector3 worldAxis = drag.transform.TransformDirection(drag.LocalAxis);
            Vector3 startPos = drag.transform.position;
            Vector3 targetPos = startPos + worldAxis * (drag.AxisLength * 0.8f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3f;
                drag.transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
        }

        private void ToggleRadioState(Radio radio)
        {
            if (radio.MusicSource != null)
            {
                if (radio.MusicSource.isPlaying)
                {
                    radio.MusicSource.Stop();
                    radio.NoiseSource.Stop();
                }
                else
                {
                    if (radio.MusicClips != null && radio.MusicClips.Length > 0)
                    {
                        radio.MusicSource.clip = radio.MusicClips[UnityEngine.Random.Range(0, radio.MusicClips.Length)];
                        radio.MusicSource.volume = 0.7f;
                        radio.MusicSource.Play();
                    }
                }
            }
        }

        private void ToggleChestLid(GameObject chestObj)
        {
            Transform lid = chestObj.transform.Find("Chest_Lid");
            if (lid == null && chestObj.transform.parent != null)
            {
                lid = chestObj.transform.parent.Find("Chest_Lid");
            }

            if (lid != null)
            {
                StartCoroutine(AnimateChestLid(lid));
            }
        }

        private IEnumerator AnimateChestLid(Transform lid)
        {
            Quaternion startRot = lid.localRotation;
            bool isOpen = Quaternion.Angle(startRot, Quaternion.Euler(-70f, 0f, 0f)) < 15f;
            Quaternion targetRot = isOpen ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.Euler(-70f, 0f, 0f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 2.5f;
                lid.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
        }

        private bool IsPotionPlugged(Potion potion)
        {
            FieldInfo fi = typeof(Potion).GetField("m_PlugIn", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
            {
                return (bool)fi.GetValue(potion);
            }
            return true;
        }

        private string CleanGameObjectName(string raw)
        {
            string clean = raw.Replace("(Clone)", "").Replace("Variant", "").Replace("Prefab", "").Trim();
            // Spacing for CamelCase
            return System.Text.RegularExpressions.Regex.Replace(clean, "(\\B[A-Z])", " $1");
        }

        private void PlaySFX(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            if (SFXPlayer.Instance != null)
            {
                SFXPlayer.Instance.PlaySFX(clip, transform.position, new SFXPlayer.PlayParameters()
                {
                    Pitch = pitch,
                    Volume = 0.85f,
                    SourceID = -100
                });
            }
        }
    }
}
