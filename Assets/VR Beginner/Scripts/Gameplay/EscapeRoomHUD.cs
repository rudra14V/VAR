using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Rich, immersive HUD for the 3-Level Escape Room Game.
    /// Provides on-screen directional buttons to move in all directions, jump button,
    /// persistent Menu symbol button, contextual interaction prompts, quest tracker,
    /// comprehensive 3-tab Menu (Controls, Things Need To Do, Others & Level Select),
    /// and Campaign Victory screens with zero external asset dependencies.
    /// </summary>
    public class EscapeRoomHUD : MonoBehaviour
    {
        public static EscapeRoomHUD Instance { get; private set; }

        public enum MenuTab
        {
            Controls,
            ThingsNeedToDo,
            Others
        }

        [Header("Settings")]
        public bool ShowObjectives = false;
        public bool ShowControlsHelper = true;
        public bool ShowOnScreenButtons = true;

        private bool m_ShowPauseMenu = false;
        private MenuTab m_CurrentTab = MenuTab.ThingsNeedToDo;
        private Vector2 m_MenuScrollPos = Vector2.zero;

        // Smooth Parchment Scroll Inspection Modal
        private bool m_ShowScrollModal = false;
        private float m_ScrollModalAlpha = 0f;
        private string m_ScrollModalTitle = "";
        private string m_ScrollModalContent = "";

        // Textures & Styles
        private Texture2D m_SolidTexture;
        private Texture2D m_BoxTexture;
        private Texture2D m_HeaderTexture;
        private Texture2D m_CheckActiveTex;
        private Texture2D m_CheckInactiveTex;
        private Texture2D m_ButtonNormalTex;
        private Texture2D m_ButtonHoverTex;
        private Texture2D m_ButtonPressedTex;
        private Texture2D m_TabActiveTex;
        private Texture2D m_TabInactiveTex;

        private GUIStyle m_CardStyle;
        private GUIStyle m_HeaderStyle;
        private GUIStyle m_ObjectiveTitleStyle;
        private GUIStyle m_ObjectiveItemStyle;
        private GUIStyle m_ObjectiveDoneStyle;
        private GUIStyle m_PromptStyle;
        private GUIStyle m_SubPromptStyle;
        private GUIStyle m_NotificationStyle;
        private GUIStyle m_TimerStyle;
        private GUIStyle m_ButtonStyle;
        private GUIStyle m_TabButtonStyle;
        private GUIStyle m_ActiveTabButtonStyle;
        private GUIStyle m_VirtualDpadStyle;
        private GUIStyle m_VirtualActionStyle;
        private GUIStyle m_MenuSymbolStyle;
        private GUIStyle m_VictoryTitleStyle;
        private GUIStyle m_VictorySubStyle;
        private bool m_StylesInitialized = false;

        // Settings state
        private float m_AudioVolume = 1.0f;
        private float m_Sensitivity = 0.12f;
        private float m_PlayerHeight = 1.0f;

        private void Awake()
        {
            Instance = this;
            m_AudioVolume = AudioListener.volume;
            if (EscapeRoomPlayerController.Instance != null)
            {
                m_Sensitivity = EscapeRoomPlayerController.Instance.MouseSensitivityX;
                m_PlayerHeight = EscapeRoomPlayerController.Instance.EyeHeight;
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Close Scroll Modal if open
            if (m_ShowScrollModal)
            {
                if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    CloseScrollModal();
                    return;
                }
            }

            // Toggle Menu with Tab, Escape, or M
            if (kb.tabKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame || 
               (kb.escapeKey.wasPressedThisFrame && (EscapeRoomManager.Instance == null || !EscapeRoomManager.Instance.HasEscaped)))
            {
                ToggleMenu();
            }

            // Toggle objectives log with O key
            if (kb.oKey.wasPressedThisFrame)
            {
                ShowObjectives = !ShowObjectives;
            }

            // Request next hint with H key
            if (kb.hKey.wasPressedThisFrame)
            {
                if (EscapeRoomManager.Instance != null)
                {
                    EscapeRoomManager.Instance.RequestNextHint();
                }
            }

            // Level 2 Chest Dial Quick Keys (1, 2, 3, 4, Q) when directly at the chest
            var box = ClockworkPuzzleBox.CurrentActiveBox;
            if (box != null && !box.IsSolved && EscapeRoomPlayerController.Instance != null)
            {
                float dist = Vector3.Distance(EscapeRoomPlayerController.Instance.transform.position, box.transform.position);
                bool isHovered = EscapeRoomInteraction.Instance != null && EscapeRoomInteraction.Instance.HoveredObject == box.gameObject;
                if (dist <= 2.0f || isHovered)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) box.SetDial(1);
                    if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) box.SetDial(2);
                    if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) box.SetDial(3);
                    if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) box.SetDial(4);
                    if (kb.qKey.wasPressedThisFrame) box.RotateDial(-1);
                }
            }
        }

        public void OpenScrollModal(string title, string content)
        {
            m_ScrollModalTitle = title;
            m_ScrollModalContent = content;
            m_ShowScrollModal = true;
            if (EscapeRoomPlayerController.Instance != null)
            {
                EscapeRoomPlayerController.Instance.SetCursorLock(false);
            }
        }

        public void CloseScrollModal()
        {
            m_ShowScrollModal = false;
            if (EscapeRoomPlayerController.Instance != null && !m_ShowPauseMenu)
            {
                EscapeRoomPlayerController.Instance.SetCursorLock(true);
            }
        }

        public void ToggleMenu()
        {
            m_ShowPauseMenu = !m_ShowPauseMenu;
            m_MenuScrollPos = Vector2.zero;

            if (EscapeRoomPlayerController.Instance != null)
            {
                if (m_ShowPauseMenu)
                {
                    EscapeRoomPlayerController.Instance.SetCursorLock(false);
                    EscapeRoomPlayerController.Instance.IsControllable = false;
                }
                else
                {
                    EscapeRoomPlayerController.Instance.IsControllable = true;
                    EscapeRoomPlayerController.Instance.SetCursorLock(true);
                }
            }
        }

        private void InitStyles()
        {
            if (m_StylesInitialized) return;

            m_SolidTexture = MakeTex(2, 2, new Color(0.08f, 0.09f, 0.12f, 0.90f));
            m_BoxTexture = MakeTex(2, 2, new Color(0.11f, 0.13f, 0.17f, 0.95f));
            m_HeaderTexture = MakeTex(2, 2, new Color(0.18f, 0.15f, 0.26f, 0.98f));
            m_CheckActiveTex = MakeTex(14, 14, new Color(0.2f, 0.88f, 0.45f, 1f));
            m_CheckInactiveTex = MakeTex(14, 14, new Color(0.35f, 0.38f, 0.45f, 0.6f));
            m_ButtonNormalTex = MakeTex(2, 2, new Color(0.16f, 0.20f, 0.28f, 0.92f));
            m_ButtonHoverTex = MakeTex(2, 2, new Color(0.25f, 0.32f, 0.46f, 0.95f));
            m_ButtonPressedTex = MakeTex(2, 2, new Color(0.85f, 0.65f, 0.15f, 0.95f));
            m_TabActiveTex = MakeTex(2, 2, new Color(0.22f, 0.28f, 0.42f, 0.98f));
            m_TabInactiveTex = MakeTex(2, 2, new Color(0.10f, 0.12f, 0.16f, 0.85f));

            m_CardStyle = new GUIStyle();
            m_CardStyle.normal.background = m_BoxTexture;
            m_CardStyle.padding = new RectOffset(16, 16, 12, 12);

            m_HeaderStyle = new GUIStyle();
            m_HeaderStyle.fontSize = 15;
            m_HeaderStyle.fontStyle = FontStyle.Bold;
            m_HeaderStyle.normal.textColor = new Color(0.96f, 0.85f, 0.45f, 1f);

            m_TimerStyle = new GUIStyle();
            m_TimerStyle.fontSize = 14;
            m_TimerStyle.fontStyle = FontStyle.Bold;
            m_TimerStyle.alignment = TextAnchor.MiddleRight;
            m_TimerStyle.normal.textColor = new Color(0.6f, 0.9f, 1.0f, 1f);

            m_ObjectiveTitleStyle = new GUIStyle();
            m_ObjectiveTitleStyle.fontSize = 13;
            m_ObjectiveTitleStyle.fontStyle = FontStyle.Bold;
            m_ObjectiveTitleStyle.normal.textColor = Color.white;

            m_ObjectiveItemStyle = new GUIStyle();
            m_ObjectiveItemStyle.fontSize = 13;
            m_ObjectiveItemStyle.wordWrap = true;
            m_ObjectiveItemStyle.normal.textColor = new Color(0.88f, 0.92f, 0.96f, 1f);

            m_ObjectiveDoneStyle = new GUIStyle();
            m_ObjectiveDoneStyle.fontSize = 13;
            m_ObjectiveDoneStyle.wordWrap = true;
            m_ObjectiveDoneStyle.fontStyle = FontStyle.Italic;
            m_ObjectiveDoneStyle.normal.textColor = new Color(0.40f, 0.90f, 0.55f, 0.85f);

            m_PromptStyle = new GUIStyle();
            m_PromptStyle.fontSize = 17;
            m_PromptStyle.fontStyle = FontStyle.Bold;
            m_PromptStyle.alignment = TextAnchor.MiddleCenter;
            m_PromptStyle.normal.textColor = new Color(1f, 0.92f, 0.45f, 1f);

            m_SubPromptStyle = new GUIStyle();
            m_SubPromptStyle.fontSize = 12;
            m_SubPromptStyle.fontStyle = FontStyle.Normal;
            m_SubPromptStyle.alignment = TextAnchor.MiddleCenter;
            m_SubPromptStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f, 0.9f);

            m_NotificationStyle = new GUIStyle();
            m_NotificationStyle.fontSize = 15;
            m_NotificationStyle.fontStyle = FontStyle.Bold;
            m_NotificationStyle.alignment = TextAnchor.MiddleCenter;
            m_NotificationStyle.normal.textColor = new Color(0.35f, 1.0f, 0.7f, 1f);

            m_ButtonStyle = new GUIStyle(GUI.skin.button);
            m_ButtonStyle.fontSize = 13;
            m_ButtonStyle.fontStyle = FontStyle.Bold;
            m_ButtonStyle.normal.textColor = Color.white;
            m_ButtonStyle.hover.textColor = new Color(1f, 0.9f, 0.4f, 1f);

            m_TabButtonStyle = new GUIStyle(GUI.skin.button);
            m_TabButtonStyle.fontSize = 14;
            m_TabButtonStyle.fontStyle = FontStyle.Bold;
            m_TabButtonStyle.normal.textColor = new Color(0.75f, 0.8f, 0.9f, 1f);

            m_ActiveTabButtonStyle = new GUIStyle(GUI.skin.button);
            m_ActiveTabButtonStyle.fontSize = 14;
            m_ActiveTabButtonStyle.fontStyle = FontStyle.Bold;
            m_ActiveTabButtonStyle.normal.textColor = new Color(1f, 0.88f, 0.35f, 1f);

            m_VirtualDpadStyle = new GUIStyle(GUI.skin.button);
            m_VirtualDpadStyle.fontSize = 18;
            m_VirtualDpadStyle.fontStyle = FontStyle.Bold;
            m_VirtualDpadStyle.alignment = TextAnchor.MiddleCenter;
            m_VirtualDpadStyle.normal.textColor = Color.white;

            m_VirtualActionStyle = new GUIStyle(GUI.skin.button);
            m_VirtualActionStyle.fontSize = 14;
            m_VirtualActionStyle.fontStyle = FontStyle.Bold;
            m_VirtualActionStyle.alignment = TextAnchor.MiddleCenter;
            m_VirtualActionStyle.normal.textColor = Color.white;

            m_MenuSymbolStyle = new GUIStyle(GUI.skin.button);
            m_MenuSymbolStyle.fontSize = 22;
            m_MenuSymbolStyle.fontStyle = FontStyle.Bold;
            m_MenuSymbolStyle.alignment = TextAnchor.MiddleCenter;
            m_MenuSymbolStyle.normal.textColor = new Color(1f, 0.9f, 0.4f, 1f);

            m_VictoryTitleStyle = new GUIStyle();
            m_VictoryTitleStyle.fontSize = 30;
            m_VictoryTitleStyle.fontStyle = FontStyle.Bold;
            m_VictoryTitleStyle.alignment = TextAnchor.MiddleCenter;
            m_VictoryTitleStyle.normal.textColor = new Color(1.0f, 0.88f, 0.35f, 1f);

            m_VictorySubStyle = new GUIStyle();
            m_VictorySubStyle.fontSize = 15;
            m_VictorySubStyle.alignment = TextAnchor.MiddleCenter;

            if (GUI.skin != null && GUI.skin.verticalScrollbar != null)
            {
                GUI.skin.verticalScrollbar.normal.background = m_BoxTexture;
                GUI.skin.verticalScrollbarThumb.normal.background = m_ButtonHoverTex;
            }

            m_StylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Victory Screen
            if (EscapeRoomManager.Instance != null && EscapeRoomManager.Instance.HasEscaped)
            {
                RenderVictoryScreen();
                return;
            }

            // 2. Comprehensive 3-Tab Pause / Game Menu
            if (m_ShowPauseMenu)
            {
                RenderPauseMenu();
                return;
            }

            // 3. In-Game HUD Elements
            RenderTopNavigationBar();
            RenderCrosshair();
            RenderInteractionPrompt();
            RenderObjectivesLog();
            RenderNotification();
            RenderHintBanner();
            RenderControlsBar();

            // 4. On-Screen Action Buttons (Jump, Use, Run, Crouch)
            // (The 4 directional movement arrow buttons are removed from the game screen as requested)
            if (ShowOnScreenButtons)
            {
                RenderOnScreenActionButtons();
            }

            // 5. Level 2 Chest Dial Controller
            RenderChestDialController();

            // 6. Smooth Parchment Scroll Inspection Modal
            if (m_ShowScrollModal || m_ScrollModalAlpha > 0.001f)
            {
                m_ScrollModalAlpha = Mathf.MoveTowards(m_ScrollModalAlpha, m_ShowScrollModal ? 1.0f : 0.0f, Time.deltaTime * 3.5f);
                RenderScrollModal();
            }
        }

        private void RenderTopNavigationBar()
        {
            // Top banner displaying Level info, Menu symbol button, and Cursor mode toggle
            float navY = 16f;

            // Persistent Menu Button - Displays ONLY three lines (☰) as requested
            Rect menuBtnRect = new Rect(20f, navY, 44f, 36f);
            GUI.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            GUI.DrawTexture(menuBtnRect, m_BoxTexture);
            GUI.color = new Color(1.0f, 0.82f, 0.35f, 0.85f);
            DrawRectBorder(menuBtnRect, 1.5f);
            GUI.color = Color.white;

            if (GUI.Button(menuBtnRect, "☰", m_MenuSymbolStyle))
            {
                m_CurrentTab = MenuTab.ThingsNeedToDo;
                ToggleMenu();
            }

            // Free Cursor / Lock Aim Toggle Button
            var player = EscapeRoomPlayerController.Instance;
            bool isLocked = player != null && player.IsCursorLocked;
            Rect cursorBtnRect = new Rect(72f, navY, 126f, 36f);
            GUI.color = new Color(0.12f, 0.15f, 0.20f, 0.95f);
            GUI.DrawTexture(cursorBtnRect, m_BoxTexture);
            GUI.color = isLocked ? new Color(0.4f, 0.8f, 1f, 0.8f) : new Color(0.4f, 1f, 0.6f, 0.8f);
            DrawRectBorder(cursorBtnRect, 1.5f);
            GUI.color = Color.white;

            string cursorLabel = isLocked ? "🔒 Aim [Locked]" : "🔓 Cursor [Free]";
            if (GUI.Button(cursorBtnRect, cursorLabel, m_ButtonStyle))
            {
                if (player != null) player.ToggleCursorLock();
            }

            // In-game Hint Button [H]
            var mgr = EscapeRoomManager.Instance;
            int totalHints = mgr != null ? mgr.TotalHintsForLevel : 0;
            int revealedHints = mgr != null ? mgr.RevealedHintCount : 0;
            Rect hintBtnRect = new Rect(206f, navY, 138f, 36f);
            GUI.color = new Color(0.20f, 0.15f, 0.08f, 0.95f);
            GUI.DrawTexture(hintBtnRect, m_BoxTexture);
            GUI.color = new Color(1.0f, 0.82f, 0.30f, 0.90f);
            DrawRectBorder(hintBtnRect, 1.5f);
            GUI.color = Color.white;

            string hintLabel = $"💡 HINT ({revealedHints}/{totalHints}) [H]";
            if (GUI.Button(hintBtnRect, hintLabel, m_ButtonStyle))
            {
                if (mgr != null) mgr.RequestNextHint();
            }

            // Level Title Badge (Center Top, adjusted with generous padding)
            // Time is removed from the upper side per user request
            string levelTitle = "LEVEL 1: GRAND DINING ROOM";

            if (GameLevelManager.Instance != null)
            {
                var info = GameLevelManager.Instance.GetCurrentLevelInfo();
                levelTitle = $"LEVEL {info.LevelNumber}: {info.DisplayName.ToUpper()}";
            }

            float badgeWidth = 330f;
            float badgeX = Mathf.Max((Screen.width - badgeWidth) * 0.5f, 356f);
            Rect badgeRect = new Rect(badgeX, navY, badgeWidth, 36f);

            GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
            GUI.DrawTexture(badgeRect, m_SolidTexture);
            GUI.color = new Color(0.45f, 0.70f, 0.95f, 0.65f);
            DrawRectBorder(badgeRect, 1.2f);

            GUI.color = Color.white;
            GUI.Label(badgeRect, levelTitle, m_HeaderStyle);
        }

        private void RenderOnScreenMovementButtons()
        {
            var player = EscapeRoomPlayerController.Instance;
            if (player == null) return;

            float padSize = 52f;
            float gap = 6f;
            float originX = 36f;
            float originY = Screen.height - (padSize * 3 + gap * 2) - 44f;

            // Background container badge
            Rect padBg = new Rect(originX - 10f, originY - 10f, padSize * 3 + gap * 2 + 20f, padSize * 3 + gap * 2 + 20f);
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.65f);
            GUI.DrawTexture(padBg, m_BoxTexture);
            GUI.color = new Color(0.4f, 0.6f, 0.8f, 0.35f);
            DrawRectBorder(padBg, 1.2f);
            GUI.color = Color.white;

            Vector2 moveDir = Vector2.zero;

            // Forward Button [W]
            Rect fwdRect = new Rect(originX + padSize + gap, originY, padSize, padSize);
            if (GUI.RepeatButton(fwdRect, "W", m_VirtualDpadStyle)) moveDir.y += 1f;

            // Left Button [A]
            Rect leftRect = new Rect(originX, originY + padSize + gap, padSize, padSize);
            if (GUI.RepeatButton(leftRect, "A", m_VirtualDpadStyle)) moveDir.x -= 1f;

            // Center Label [WASD]
            Rect centerRect = new Rect(originX + padSize + gap, originY + padSize + gap, padSize, padSize);
            GUI.color = new Color(0.5f, 0.6f, 0.75f, 0.8f);
            GUI.Label(centerRect, "WASD", m_SubPromptStyle);
            GUI.color = Color.white;

            // Right Button [D]
            Rect rightRect = new Rect(originX + (padSize + gap) * 2, originY + padSize + gap, padSize, padSize);
            if (GUI.RepeatButton(rightRect, "D", m_VirtualDpadStyle)) moveDir.x += 1f;

            // Backward Button [S]
            Rect backRect = new Rect(originX + padSize + gap, originY + (padSize + gap) * 2, padSize, padSize);
            if (GUI.RepeatButton(backRect, "S", m_VirtualDpadStyle)) moveDir.y -= 1f;

            player.VirtualMovement = moveDir;
        }

        private void RenderOnScreenActionButtons()
        {
            var player = EscapeRoomPlayerController.Instance;
            if (player == null) return;

            float btnW = 95f;
            float btnH = 46f;
            float gap = 10f;
            float rightX = Screen.width - btnW * 2 - gap - 36f;
            float startY = Screen.height - (btnH * 2 + gap) - 44f;

            // JUMP BUTTON (Prominent glowing amber button)
            Rect jumpRect = new Rect(rightX + btnW + gap, startY + btnH + gap, btnW, btnH);
            GUI.color = new Color(0.18f, 0.22f, 0.12f, 0.90f);
            GUI.DrawTexture(jumpRect, m_BoxTexture);
            GUI.color = new Color(1.0f, 0.85f, 0.25f, 0.90f);
            DrawRectBorder(jumpRect, 2.0f);
            GUI.color = Color.white;

            if (GUI.Button(jumpRect, "⤊ JUMP", m_VirtualActionStyle))
            {
                player.TriggerJump();
            }

            // USE / INTERACT BUTTON
            Rect useRect = new Rect(rightX, startY + btnH + gap, btnW, btnH);
            GUI.color = new Color(0.12f, 0.18f, 0.26f, 0.90f);
            GUI.DrawTexture(useRect, m_BoxTexture);
            GUI.color = new Color(0.35f, 0.85f, 1.0f, 0.85f);
            DrawRectBorder(useRect, 1.8f);
            GUI.color = Color.white;

            if (GUI.Button(useRect, "✋ USE [E]", m_VirtualActionStyle))
            {
                if (EscapeRoomInteraction.Instance != null)
                {
                    EscapeRoomInteraction.Instance.TriggerInteract();
                }
            }

            // SPRINT BUTTON
            Rect sprintRect = new Rect(rightX, startY, btnW, btnH);
            bool sprintActive = player.VirtualSprint;
            GUI.color = sprintActive ? new Color(0.25f, 0.4f, 0.15f, 0.95f) : new Color(0.12f, 0.15f, 0.20f, 0.85f);
            GUI.DrawTexture(sprintRect, m_BoxTexture);
            GUI.color = sprintActive ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(0.5f, 0.7f, 0.85f, 0.6f);
            DrawRectBorder(sprintRect, 1.2f);
            GUI.color = Color.white;

            if (GUI.Button(sprintRect, sprintActive ? "🏃 RUN ON" : "🏃 SPRINT", m_VirtualActionStyle))
            {
                player.VirtualSprint = !player.VirtualSprint;
            }

            // CROUCH BUTTON
            Rect crouchRect = new Rect(rightX + btnW + gap, startY, btnW, btnH);
            bool crouchActive = player.VirtualCrouch;
            GUI.color = crouchActive ? new Color(0.35f, 0.25f, 0.15f, 0.95f) : new Color(0.12f, 0.15f, 0.20f, 0.85f);
            GUI.DrawTexture(crouchRect, m_BoxTexture);
            GUI.color = crouchActive ? new Color(1f, 0.7f, 0.3f, 1f) : new Color(0.5f, 0.7f, 0.85f, 0.6f);
            DrawRectBorder(crouchRect, 1.2f);
            GUI.color = Color.white;

            if (GUI.Button(crouchRect, crouchActive ? "⭳ DUCK ON" : "⭳ CROUCH", m_VirtualActionStyle))
            {
                player.VirtualCrouch = !player.VirtualCrouch;
            }
        }

        private void RenderCrosshair()
        {
            var player = EscapeRoomPlayerController.Instance;
            var interaction = EscapeRoomInteraction.Instance;

            // When player is in free cursor mode, DO NOT draw custom cursor overlays.
            // The user requested standard, normal OS mouse pointer!
            if (player != null && !player.IsCursorLocked)
            {
                return;
            }

            // In FPS Locked Aim mode: draw a subtle, clean precision dot reticle at screen center
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool hasTarget = interaction != null && interaction.HasInteractiveHover;
            bool isWand = interaction != null && interaction.IsWandHeld;

            Color dotColor = hasTarget ? new Color(1f, 0.88f, 0.25f, 0.95f) : (isWand ? new Color(0.3f, 0.85f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.75f));
            DrawCircle(screenCenter.x, screenCenter.y, hasTarget ? 2.5f : 1.8f, dotColor);

            if (isWand)
            {
                DrawRing(screenCenter.x, screenCenter.y, 14f, 1.2f, new Color(0.3f, 0.85f, 1f, 0.65f));
            }
        }

        private void RenderInteractionPrompt()
        {
            var interaction = EscapeRoomInteraction.Instance;
            if (interaction == null) return;

            string prompt = interaction.CurrentPrompt;
            string subPrompt = interaction.CurrentSubPrompt;

            if (string.IsNullOrEmpty(prompt)) return;

            float boxWidth = Mathf.Clamp(prompt.Length * 11f + 60f, 280f, 540f);
            float boxHeight = string.IsNullOrEmpty(subPrompt) ? 38f : 58f;
            float boxX = (Screen.width - boxWidth) * 0.5f;
            float boxY = Screen.height * 0.5f + 44f;

            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), m_SolidTexture);

            GUI.color = new Color(0.85f, 0.75f, 0.35f, 0.75f);
            DrawRectBorder(new Rect(boxX, boxY, boxWidth, boxHeight), 1.5f);

            GUI.color = Color.white;
            GUI.Label(new Rect(boxX, boxY + 6f, boxWidth, 24f), prompt, m_PromptStyle);

            if (!string.IsNullOrEmpty(subPrompt))
            {
                GUI.Label(new Rect(boxX, boxY + 30f, boxWidth, 20f), subPrompt, m_SubPromptStyle);
            }
        }

        private void RenderObjectivesLog()
        {
            if (!ShowObjectives) return;
            var mgr = EscapeRoomManager.Instance;
            if (mgr == null) return;

            float panelWidth = 330f;
            float panelHeight = 44f + mgr.Objectives.Count * 28f;
            float panelX = Screen.width - panelWidth - 20f;
            float panelY = 20f;

            GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), m_BoxTexture);

            GUI.color = new Color(0.18f, 0.15f, 0.28f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, 34f), m_HeaderTexture);

            GUI.color = new Color(0.45f, 0.4f, 0.65f, 0.55f);
            DrawRectBorder(new Rect(panelX, panelY, panelWidth, panelHeight), 1.5f);

            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + 14f, panelY + 7f, panelWidth - 28f, 22f), $"QUESTS ({mgr.GetCompletedCount()}/{mgr.Objectives.Count})", m_HeaderStyle);

            float currentY = panelY + 42f;
            for (int i = 0; i < mgr.Objectives.Count; i++)
            {
                var obj = mgr.Objectives[i];
                float checkY = currentY + 4f;

                if (obj.IsCompleted)
                {
                    GUI.color = new Color(0.25f, 0.9f, 0.45f, 1f);
                    GUI.DrawTexture(new Rect(panelX + 14f, checkY, 14f, 14f), m_CheckActiveTex);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(panelX + 34f, currentY, panelWidth - 46f, 24f), $"<s>{obj.Title}</s>", m_ObjectiveDoneStyle);
                }
                else
                {
                    GUI.color = new Color(0.4f, 0.45f, 0.55f, 0.7f);
                    GUI.DrawTexture(new Rect(panelX + 14f, checkY, 14f, 14f), m_CheckInactiveTex);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(panelX + 34f, currentY, panelWidth - 46f, 24f), obj.Title, m_ObjectiveItemStyle);
                }

                currentY += 28f;
            }
        }

        private void RenderNotification()
        {
            var mgr = EscapeRoomManager.Instance;
            if (mgr == null || !mgr.ShowNotification) return;

            float notifWidth = 560f;
            float notifHeight = 44f;
            float notifX = (Screen.width - notifWidth) * 0.5f;
            float notifY = 64f;

            GUI.color = new Color(0.08f, 0.16f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(notifX, notifY, notifWidth, notifHeight), m_BoxTexture);

            GUI.color = new Color(0.25f, 0.88f, 0.5f, 0.85f);
            DrawRectBorder(new Rect(notifX, notifY, notifWidth, notifHeight), 1.5f);

            GUI.color = Color.white;
            GUI.Label(new Rect(notifX, notifY + 10f, notifWidth, 24f), mgr.RecentNotification, m_NotificationStyle);
        }

        private void RenderHintBanner()
        {
            var mgr = EscapeRoomManager.Instance;
            if (mgr == null || !mgr.IsHintActive) return;

            float bannerWidth = Mathf.Min(800f, Screen.width - 40f);
            float bannerHeight = 84f;
            float bannerX = (Screen.width - bannerWidth) * 0.5f;
            float bannerY = 60f;

            GUI.color = new Color(0.12f, 0.09f, 0.04f, 0.96f);
            GUI.DrawTexture(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), m_BoxTexture);

            GUI.color = new Color(1.0f, 0.82f, 0.30f, 0.95f);
            DrawRectBorder(new Rect(bannerX, bannerY, bannerWidth, bannerHeight), 2.0f);

            GUI.color = new Color(1f, 0.95f, 0.75f, 1f);
            GUI.Label(new Rect(bannerX + 16f, bannerY + 10f, bannerWidth - 32f, bannerHeight - 20f), mgr.ActiveHintText, m_ObjectiveItemStyle);
        }

        private void RenderControlsBar()
        {
            if (!ShowControlsHelper) return;

            float barWidth = 760f;
            float barHeight = 28f;
            float barX = (Screen.width - barWidth) * 0.5f;
            float barY = Screen.height - barHeight - 12f;

            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.80f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), m_SolidTexture);

            GUI.color = new Color(0.7f, 0.75f, 0.85f, 0.85f);
            GUI.Label(new Rect(barX, barY + 4f, barWidth, 20f),
                "WASD: Move  |  Space: Jump  |  F: Crouch  |  E: Pick/Use  |  H: Hint  |  Left-Click: Cast/Throw  |  Top Button: Menu",
                m_SubPromptStyle);
        }

        private void RenderPauseMenu()
        {
            // Dark Frosted Overlay
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_SolidTexture);

            float menuWidth = Mathf.Min(780f, Screen.width - 40f);
            float menuHeight = Mathf.Min(600f, Screen.height - 40f);
            float menuX = (Screen.width - menuWidth) * 0.5f;
            float menuY = (Screen.height - menuHeight) * 0.5f;

            GUI.color = new Color(0.10f, 0.12f, 0.17f, 0.98f);
            GUI.DrawTexture(new Rect(menuX, menuY, menuWidth, menuHeight), m_BoxTexture);

            GUI.color = new Color(0.85f, 0.75f, 0.4f, 0.85f);
            DrawRectBorder(new Rect(menuX, menuY, menuWidth, menuHeight), 2.5f);

            // Title Header
            GUI.color = Color.white;
            GUI.Label(new Rect(menuX, menuY + 16f, menuWidth, 34f), "ESCAPE ROOM ADVENTURE", m_VictoryTitleStyle);

            // 3 TAB BUTTONS: Controls, Quests, Others
            float tabY = menuY + 58f;
            float tabWidth = (menuWidth - 60f) / 3f;

            // Tab 1: Controls
            GUI.color = m_CurrentTab == MenuTab.Controls ? Color.white : new Color(0.8f, 0.85f, 0.95f, 0.85f);
            if (GUI.Button(new Rect(menuX + 30f, tabY, tabWidth, 36f), "🎮  CONTROLS", m_CurrentTab == MenuTab.Controls ? m_ActiveTabButtonStyle : m_TabButtonStyle))
            {
                if (m_CurrentTab != MenuTab.Controls) { m_CurrentTab = MenuTab.Controls; m_MenuScrollPos = Vector2.zero; }
            }

            // Tab 2: Quests (Things Need To Do)
            GUI.color = m_CurrentTab == MenuTab.ThingsNeedToDo ? Color.white : new Color(0.8f, 0.85f, 0.95f, 0.85f);
            if (GUI.Button(new Rect(menuX + 30f + tabWidth, tabY, tabWidth, 36f), "📜  QUESTS & TASKS", m_CurrentTab == MenuTab.ThingsNeedToDo ? m_ActiveTabButtonStyle : m_TabButtonStyle))
            {
                if (m_CurrentTab != MenuTab.ThingsNeedToDo) { m_CurrentTab = MenuTab.ThingsNeedToDo; m_MenuScrollPos = Vector2.zero; }
            }

            // Tab 3: Others & Levels
            GUI.color = m_CurrentTab == MenuTab.Others ? Color.white : new Color(0.8f, 0.85f, 0.95f, 0.85f);
            if (GUI.Button(new Rect(menuX + 30f + tabWidth * 2f, tabY, tabWidth, 36f), "⚙️  SETTINGS & LEVELS", m_CurrentTab == MenuTab.Others ? m_ActiveTabButtonStyle : m_TabButtonStyle))
            {
                if (m_CurrentTab != MenuTab.Others) { m_CurrentTab = MenuTab.Others; m_MenuScrollPos = Vector2.zero; }
            }

            // Content Area Box with Scroll View
            Rect contentBox = new Rect(menuX + 30f, tabY + 44f, menuWidth - 60f, menuHeight - 165f);
            GUI.color = new Color(0.07f, 0.09f, 0.13f, 0.90f);
            GUI.DrawTexture(contentBox, m_BoxTexture);
            GUI.color = new Color(0.35f, 0.40f, 0.55f, 0.45f);
            DrawRectBorder(contentBox, 1.2f);
            GUI.color = Color.white;

            float scrollInnerW = contentBox.width - 24f;
            float scrollInnerH = GetTabContentHeight(m_CurrentTab, scrollInnerW);
            Rect viewRect = new Rect(0f, 0f, scrollInnerW, scrollInnerH);

            m_MenuScrollPos = GUI.BeginScrollView(contentBox, m_MenuScrollPos, viewRect, false, true);
            Rect tabArea = new Rect(0f, 0f, scrollInnerW, scrollInnerH);

            switch (m_CurrentTab)
            {
                case MenuTab.Controls:
                    RenderTabControls(tabArea);
                    break;
                case MenuTab.ThingsNeedToDo:
                    RenderTabThingsNeedToDo(tabArea);
                    break;
                case MenuTab.Others:
                    RenderTabOthers(tabArea);
                    break;
            }

            GUI.EndScrollView();

            // Bottom Buttons (Resume Game & Restart Level)
            float btnW = 170f;
            float btnH = 40f;
            float bottomY = menuY + menuHeight - 52f;

            if (GUI.Button(new Rect(menuX + 40f, bottomY, btnW, btnH), "▶  Resume Game", m_ButtonStyle))
            {
                m_ShowPauseMenu = false;
                if (EscapeRoomPlayerController.Instance != null)
                {
                    EscapeRoomPlayerController.Instance.IsControllable = true;
                    EscapeRoomPlayerController.Instance.SetCursorLock(true);
                }
            }

            if (GUI.Button(new Rect(menuX + menuWidth - 40f - btnW, bottomY, btnW, btnH), "🔄  Restart Level", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null)
                    GameLevelManager.Instance.RestartCurrentLevel();
                else
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private float GetTabContentHeight(MenuTab tab, float width)
        {
            switch (tab)
            {
                case MenuTab.Controls:
                    return 540f;
                case MenuTab.ThingsNeedToDo:
                    float h = 48f; // Title & difficulty
                    var mgr = EscapeRoomManager.Instance;
                    int objCount = (mgr != null && mgr.Objectives != null) ? mgr.Objectives.Count : 4;
                    h += objCount * 30f + 16f;
                    int hintCount = (mgr != null && mgr.CurrentLevelHints != null) ? mgr.CurrentLevelHints.Count : 3;
                    h += 34f + Mathf.Max(60f, hintCount * 38f + 16f) + 20f;
                    h += 32f + 160f + 30f; // Walkthrough box
                    return Mathf.Max(580f, h);
                case MenuTab.Others:
                    return 560f;
                default:
                    return 540f;
            }
        }

        private void RenderTabControls(Rect area)
        {
            float x = 20f;
            float y = 16f;
            float w = area.width - 40f;

            GUI.Label(new Rect(x, y, w, 26f), "🎮  COMPLETE GAME CONTROLS", m_HeaderStyle);
            y += 32f;

            // Keyboard & Mouse card
            float keyCardH = 210f;
            Rect keyCard = new Rect(x, y, w, keyCardH);
            GUI.color = new Color(0.10f, 0.13f, 0.18f, 0.90f);
            GUI.DrawTexture(keyCard, m_BoxTexture);
            GUI.color = new Color(0.4f, 0.6f, 0.8f, 0.4f);
            DrawRectBorder(keyCard, 1.2f);
            GUI.color = Color.white;

            string keyboardText =
                "• [W, A, S, D] or Arrow Keys : Walk in all directions smoothly\n" +
                "• [SPACEBAR] : Jump (responsive with coyote-time & jump buffer)\n" +
                "• [F] or [C] : Crouch / Duck under obstacles\n" +
                "• [LEFT SHIFT] : Sprint / Run faster\n" +
                "• [E] : Pick Up items / Unlock doors / Step through exit / Interact\n" +
                "• [LEFT CLICK] : Throw held item / Cast wand spell\n" +
                "• [HOLD RIGHT CLICK] : Rotate camera in free mode / Tilt potion flask\n" +
                "• [TAB] or [ESC] or Top [☰] Button : Open / Close Game Menu & Quests";

            GUI.color = new Color(0.88f, 0.93f, 1f, 1f);
            GUI.Label(new Rect(x + 14f, y + 12f, w - 28f, keyCardH - 24f), keyboardText, m_ObjectiveItemStyle);
            y += keyCardH + 18f;

            // Touch UI card
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, 26f), "📱  TOUCH & ON-SCREEN BUTTONS", m_HeaderStyle);
            y += 30f;

            float touchCardH = 150f;
            Rect touchCard = new Rect(x, y, w, touchCardH);
            GUI.color = new Color(0.10f, 0.13f, 0.18f, 0.90f);
            GUI.DrawTexture(touchCard, m_BoxTexture);
            GUI.color = new Color(0.4f, 0.6f, 0.8f, 0.4f);
            DrawRectBorder(touchCard, 1.2f);
            GUI.color = Color.white;

            string uiText =
                "• Bottom-Left D-Pad [W, A, S, D] : Hold virtual buttons to move smoothly in all directions.\n" +
                "• Bottom-Right [⤊ JUMP] : Tap to jump anytime.\n" +
                "• Bottom-Right [✋ USE] : Tap to pick up items, interact with puzzles, or unlock doors.\n" +
                "• Top-Left [☰ MENU] : Access Quests, Walkthrough, Controls, and Settings.\n" +
                "• Top-Left [🔒 AIM / 🔓 CURSOR] : Toggle between FPS aim and free mouse pointer.";

            GUI.color = new Color(0.80f, 0.90f, 1f, 1f);
            GUI.Label(new Rect(x + 14f, y + 12f, w - 28f, touchCardH - 24f), uiText, m_ObjectiveItemStyle);
            y += touchCardH + 18f;
        }

        private void RenderTabThingsNeedToDo(Rect area)
        {
            float x = 20f;
            float y = 14f;
            float w = area.width - 40f;

            string currentRoomName = "Current Room";
            string difficulty = "EASY";
            Color diffCol = Color.green;

            if (GameLevelManager.Instance != null)
            {
                var info = GameLevelManager.Instance.GetCurrentLevelInfo();
                currentRoomName = info.DisplayName;
                difficulty = info.Difficulty;
                diffCol = info.DifficultyColor;
            }

            GUI.Label(new Rect(x, y, w - 160f, 26f), $"OBJECTIVES: {currentRoomName.ToUpper()}", m_HeaderStyle);
            GUI.color = diffCol;
            GUI.Label(new Rect(x + w - 150f, y, 150f, 26f), $"DIFFICULTY: {difficulty}", m_TimerStyle);
            GUI.color = Color.white;
            y += 32f;

            // Objectives list
            var mgr = EscapeRoomManager.Instance;
            if (mgr != null && mgr.Objectives.Count > 0)
            {
                for (int i = 0; i < mgr.Objectives.Count; i++)
                {
                    var obj = mgr.Objectives[i];
                    string prefix = obj.IsCompleted ? "✔ [DONE] " : $"[{i + 1}] ";
                    GUIStyle style = obj.IsCompleted ? m_ObjectiveDoneStyle : m_ObjectiveItemStyle;
                    GUI.color = obj.IsCompleted ? new Color(0.35f, 0.95f, 0.55f, 1f) : Color.white;

                    GUI.Label(new Rect(x + 6f, y, w - 12f, 24f), prefix + obj.Title, style);
                    y += 28f;
                }
            }
            y += 14f;

            // Hints Section
            int totalHints = mgr != null ? mgr.TotalHintsForLevel : 0;
            int revealedCount = mgr != null ? mgr.RevealedHintCount : 0;

            GUI.color = new Color(1f, 0.85f, 0.3f, 1f);
            GUI.Label(new Rect(x, y, w - 190f, 26f), $"💡 LEVEL HINTS ({revealedCount}/{totalHints} Revealed for {difficulty}):", m_HeaderStyle);

            if (mgr != null && revealedCount < totalHints)
            {
                if (GUI.Button(new Rect(x + w - 180f, y - 2f, 180f, 28f), "🔍 Reveal Next Hint [H]", m_ButtonStyle))
                {
                    mgr.RequestNextHint();
                }
            }
            else if (mgr != null && totalHints > 0)
            {
                GUI.color = new Color(0.4f, 0.95f, 0.55f, 0.95f);
                GUI.Label(new Rect(x + w - 170f, y, 170f, 24f), "✔ All Hints Revealed", m_SubPromptStyle);
            }
            GUI.color = Color.white;
            y += 32f;

            float hintBoxH = Mathf.Max(60f, (mgr != null ? mgr.CurrentLevelHints.Count : 1) * 36f + 16f);
            Rect hintBox = new Rect(x, y, w, hintBoxH);
            GUI.color = new Color(0.12f, 0.10f, 0.06f, 0.95f);
            GUI.DrawTexture(hintBox, m_BoxTexture);
            GUI.color = new Color(1.0f, 0.82f, 0.30f, 0.85f);
            DrawRectBorder(hintBox, 1.2f);

            float hintY = hintBox.y + 10f;
            if (mgr != null && mgr.CurrentLevelHints.Count > 0)
            {
                for (int h = 0; h < mgr.CurrentLevelHints.Count; h++)
                {
                    bool isUnlocked = (h < revealedCount);
                    string hintHeader = $"Hint {h + 1}/{totalHints}: ";
                    if (isUnlocked)
                    {
                        GUI.color = new Color(1f, 0.95f, 0.82f, 1f);
                        GUI.Label(new Rect(hintBox.x + 14f, hintY, hintBox.width - 28f, 32f), $"{hintHeader}{mgr.CurrentLevelHints[h]}", m_ObjectiveItemStyle);
                    }
                    else
                    {
                        GUI.color = new Color(0.65f, 0.65f, 0.72f, 0.75f);
                        GUI.Label(new Rect(hintBox.x + 14f, hintY, hintBox.width - 28f, 32f), $"🔒 {hintHeader}[Press H or click 'Reveal Next Hint' above to unlock]", m_ObjectiveItemStyle);
                    }
                    hintY += 36f;
                }
            }
            y += hintBoxH + 18f;

            // Walkthrough Guide Box
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, 26f), "📜  ROOM PUZZLE WALKTHROUGH GUIDE:", m_HeaderStyle);
            y += 30f;

            string walkthroughText = "";
            string currentSceneName = SceneManager.GetActiveScene().name.ToLower();

            if (currentSceneName.Contains("dining") || currentSceneName.Contains("dening") || currentSceneName.Contains("1"))
            {
                walkthroughText =
                    "• STEP 1: Read the Butler's Banquet Protocol on the west sideboard [E].\n" +
                    "• STEP 2: Ignite all 3 unlit Sacred Candelabras along the tables (North, East, South) with [E].\n" +
                    "• STEP 3: Grab the unsealed Golden Dining Key unlatched on the center pedestal.\n" +
                    "• STEP 4: Unlock the North Double Door with [E] and step through into the corridor to advance to Level 2!";
            }
            else if (currentSceneName.Contains("medieval") || currentSceneName.Contains("chamber") || currentSceneName.Contains("living") || currentSceneName.Contains("2"))
            {
                walkthroughText =
                    "• STEP 1: Read the glowing Knight's Decree parchment scroll on the council table [E].\n" +
                    "• STEP 2: Inspect the ironbound chest beside the fireplace hearth and turn the runic dial to IV [E/Q].\n" +
                    "• STEP 3: Collect the floating Royal Castle Key revealed above the unsealed chest.\n" +
                    "• STEP 4: Unlock the North Sanctum Oak Gateway with [E] and walk out through the archway to advance to Level 3!";
            }
            else
            {
                walkthroughText =
                    "• STEP 1: Read the Grand Alchemist's Grimoire on the central altar desk [E].\n" +
                    "• STEP 2: Collect the Emerald Runestone Key from the desk [E] (or brew the unsealing potion in the cauldron).\n" +
                    "• STEP 3: Approach the Sanctuary Exit Door and unlock it with [E].\n" +
                    "• STEP 4: Step through the open doorway onto the forest terrace to achieve freedom and victory!";
            }

            float guideBoxH = 140f;
            Rect guideBox = new Rect(x, y, w, guideBoxH);
            GUI.color = new Color(0.10f, 0.14f, 0.22f, 0.95f);
            GUI.DrawTexture(guideBox, m_BoxTexture);
            GUI.color = new Color(0.45f, 0.75f, 0.95f, 0.85f);
            DrawRectBorder(guideBox, 1.2f);

            GUI.color = new Color(0.92f, 0.96f, 1f, 1f);
            GUI.Label(new Rect(guideBox.x + 14f, guideBox.y + 12f, guideBox.width - 28f, guideBox.height - 24f), walkthroughText, m_ObjectiveItemStyle);
            y += guideBoxH + 18f;
        }

        private void RenderTabOthers(Rect area)
        {
            float x = 20f;
            float y = 14f;
            float w = area.width - 40f;

            GUI.Label(new Rect(x, y, w, 26f), "🏰  CAMPAIGN LEVEL SELECT", m_HeaderStyle);
            y += 32f;

            float lvlBtnW = (w - 24f) / 3f;

            // Level 1 Button
            if (GUI.Button(new Rect(x, y, lvlBtnW, 52f), "Level 1: Dining Room\n[ EASY ]", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null) GameLevelManager.Instance.LoadLevel(0);
                else SceneManager.LoadScene("Level1_DiningRoom");
                m_ShowPauseMenu = false;
            }

            // Level 2 Button
            if (GUI.Button(new Rect(x + lvlBtnW + 12f, y, lvlBtnW, 52f), "Level 2: Medieval Chamber\n[ MEDIUM ]", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null) GameLevelManager.Instance.LoadLevel(1);
                else SceneManager.LoadScene("Level2_MedievalRoom");
                m_ShowPauseMenu = false;
            }

            // Level 3 Button
            if (GUI.Button(new Rect(x + (lvlBtnW + 12f) * 2f, y, lvlBtnW, 52f), "Level 3: Witch Cottage\n[ HARD ]", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null) GameLevelManager.Instance.LoadLevel(2);
                else SceneManager.LoadScene("Level3_WitchCottage");
                m_ShowPauseMenu = false;
            }

            y += 66f;

            // Skip to Next Level Button
            if (GUI.Button(new Rect(x, y, w, 38f), "⏭  Skip to Next Level", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null) GameLevelManager.Instance.LoadNextLevel();
                m_ShowPauseMenu = false;
            }

            y += 54f;

            GUI.Label(new Rect(x, y, w, 26f), "⚙️  AUDIO & GAMEPLAY SETTINGS", m_HeaderStyle);
            y += 32f;

            // Sound Volume Slider
            GUI.Label(new Rect(x, y, 200f, 26f), $"Master Audio Volume: {Mathf.RoundToInt(m_AudioVolume * 100f)}%", m_ObjectiveItemStyle);
            m_AudioVolume = GUI.HorizontalSlider(new Rect(x + 220f, y + 5f, w - 230f, 22f), m_AudioVolume, 0f, 1f);
            AudioListener.volume = m_AudioVolume;
            y += 38f;

            // Mouse Sensitivity Slider
            GUI.Label(new Rect(x, y, 200f, 26f), $"Mouse Sensitivity: {m_Sensitivity:F2}", m_ObjectiveItemStyle);
            m_Sensitivity = GUI.HorizontalSlider(new Rect(x + 220f, y + 5f, w - 230f, 22f), m_Sensitivity, 0.04f, 0.40f);
            if (EscapeRoomPlayerController.Instance != null)
            {
                EscapeRoomPlayerController.Instance.MouseSensitivityX = m_Sensitivity;
                EscapeRoomPlayerController.Instance.MouseSensitivityY = m_Sensitivity;
            }
            y += 38f;

            // Player Height Slider
            GUI.Label(new Rect(x, y, 200f, 26f), $"Player Eye Height: {m_PlayerHeight:F2}m", m_ObjectiveItemStyle);
            float newHeight = GUI.HorizontalSlider(new Rect(x + 220f, y + 5f, w - 230f, 22f), m_PlayerHeight, 0.50f, 1.80f);
            if (Mathf.Abs(newHeight - m_PlayerHeight) > 0.01f)
            {
                m_PlayerHeight = newHeight;
                if (EscapeRoomPlayerController.Instance != null)
                {
                    EscapeRoomPlayerController.Instance.SetPlayerHeight(m_PlayerHeight);
                }
            }
            y += 44f;

            // On-Screen Buttons Visibility Toggle
            ShowOnScreenButtons = GUI.Toggle(new Rect(x, y, 320f, 26f), ShowOnScreenButtons, "  Show On-Screen Movement/Jump Buttons");

            // Quit Application Button
            if (GUI.Button(new Rect(x + w - 160f, y - 2f, 160f, 34f), "🚪  Quit Game", m_ButtonStyle))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
            y += 44f;
        }

        private void RenderVictoryScreen()
        {
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_SolidTexture);

            float winWidth = 680f;
            float winHeight = 460f;
            float winX = (Screen.width - winWidth) * 0.5f;
            float winY = (Screen.height - winHeight) * 0.5f;

            GUI.color = new Color(0.09f, 0.12f, 0.18f, 0.98f);
            GUI.DrawTexture(new Rect(winX, winY, winWidth, winHeight), m_BoxTexture);

            GUI.color = new Color(1.0f, 0.85f, 0.35f, 0.90f);
            DrawRectBorder(new Rect(winX, winY, winWidth, winHeight), 2.5f);

            var mgr = EscapeRoomManager.Instance;
            string timeStr = mgr != null ? mgr.GetFormattedTimer() : "00:00";
            string campaignTime = GameLevelManager.Instance != null ? GameLevelManager.Instance.GetFormattedCampaignTime() : timeStr;

            GUI.color = Color.white;
            GUI.Label(new Rect(winX, winY + 32f, winWidth, 42f), "🏆  CAMPAIGN COMPLETE! YOU ESCAPED!  🏆", m_VictoryTitleStyle);

            GUI.color = new Color(0.9f, 0.95f, 1f, 0.9f);
            GUI.Label(new Rect(winX, winY + 82f, winWidth, 30f), "Outstanding! You conquered all 3 rooms: Living Room, Dining Room, and Cottage!", m_VictorySubStyle);

            // Campaign Summary Card
            float statsY = winY + 135f;
            GUI.color = new Color(0.14f, 0.18f, 0.24f, 0.92f);
            GUI.DrawTexture(new Rect(winX + 50f, statsY, winWidth - 100f, 160f), m_BoxTexture);

            GUI.color = new Color(0.5f, 0.8f, 1f, 0.6f);
            DrawRectBorder(new Rect(winX + 50f, statsY, winWidth - 100f, 160f), 1f);

            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 70f, statsY + 18f, 260f, 26f), "Total Campaign Escape Time:", m_HeaderStyle);
            GUI.Label(new Rect(winX + winWidth - 220f, statsY + 18f, 150f, 26f), campaignTime, m_TimerStyle);

            GUI.Label(new Rect(winX + 70f, statsY + 54f, 260f, 26f), "Level 1: The Vintage Living Room", m_ObjectiveItemStyle);
            GUI.color = new Color(0.35f, 0.9f, 0.45f);
            GUI.Label(new Rect(winX + winWidth - 220f, statsY + 54f, 150f, 26f), "✔ CLEARED (Easy)", m_TimerStyle);

            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 70f, statsY + 84f, 260f, 26f), "Level 2: The Grand Dining Room", m_ObjectiveItemStyle);
            GUI.color = new Color(0.95f, 0.75f, 0.2f);
            GUI.Label(new Rect(winX + winWidth - 220f, statsY + 84f, 150f, 26f), "✔ CLEARED (Medium)", m_TimerStyle);

            GUI.color = Color.white;
            GUI.Label(new Rect(winX + 70f, statsY + 114f, 260f, 26f), "Level 3: The Witch's Cottage", m_ObjectiveItemStyle);
            GUI.color = new Color(0.95f, 0.35f, 0.35f);
            GUI.Label(new Rect(winX + winWidth - 220f, statsY + 114f, 150f, 26f), "✔ CLEARED (Hard)", m_TimerStyle);

            // Bottom Buttons
            float btnW = 180f;
            float btnH = 44f;
            if (GUI.Button(new Rect(winX + 110f, winY + 340f, btnW, btnH), "🔄 Play Again (Level 1)", m_ButtonStyle))
            {
                if (GameLevelManager.Instance != null) GameLevelManager.Instance.LoadLevel(0);
                else SceneManager.LoadScene(0);
            }

            if (GUI.Button(new Rect(winX + winWidth - 110f - btnW, winY + 340f, btnW, btnH), "📋 Level Select Menu", m_ButtonStyle))
            {
                if (mgr != null) mgr.RestartGame();
            }
        }

        private void DrawCircle(float cx, float cy, float radius, Color color)
        {
            GUI.color = color;
            float d = radius * 2f;
            GUI.DrawTexture(new Rect(cx - radius, cy - radius, d, d), m_SolidTexture);
        }

        private void DrawRing(float cx, float cy, float radius, float thickness, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new Rect(cx - radius, cy - thickness * 0.5f, radius * 2f, thickness), m_SolidTexture);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - radius, thickness, radius * 2f), m_SolidTexture);
        }

        private void DrawRectBorder(Rect r, float thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), m_SolidTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), m_SolidTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), m_SolidTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), m_SolidTexture);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void RenderChestDialController()
        {
            var box = ClockworkPuzzleBox.CurrentActiveBox;
            if (box == null) return;

            var player = EscapeRoomPlayerController.Instance;
            Vector3 playerPos = player != null ? player.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            float dist = Vector3.Distance(playerPos, box.transform.position);

            var interaction = EscapeRoomInteraction.Instance;
            bool isLookingAtChest = interaction != null && interaction.HoveredObject == box.gameObject;

            // Only display when player directly approaches the trunk / chest!
            if (dist > 2.0f && !isLookingAtChest) return;

            // Panel dimensions and position (bottom center)
            float panelW = 500f;
            float panelH = 112f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = Screen.height - panelH - 36f;

            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            // Background
            GUI.color = new Color(0.10f, 0.12f, 0.17f, 0.95f);
            GUI.DrawTexture(panelRect, m_BoxTexture);
            GUI.color = box.IsSolved ? new Color(0.3f, 1.0f, 0.5f, 0.90f) : new Color(1.0f, 0.82f, 0.35f, 0.90f);
            DrawRectBorder(panelRect, 2.0f);
            GUI.color = Color.white;

            // Header Title - does not give away the solution!
            string title = box.IsSolved ? "⚜ CHEST UNLOCKED — ROYAL CASTLE KEY REVEALED! ⚜" : "⚜ CHEST RUNIC LOCK — ALIGN THE RUNIC SIGIL ⚜";
            GUI.Label(new Rect(panelX + 10f, panelY + 6f, panelW - 20f, 22f), title, m_HeaderStyle);

            if (box.IsSolved)
            {
                GUI.color = new Color(0.35f, 1.0f, 0.65f);
                GUI.Label(new Rect(panelX + 10f, panelY + 40f, panelW - 20f, 30f), "✔ Mechanism Unlocked! Collect the Royal Castle Key inside the chest [E]!", m_PromptStyle);
                GUI.color = Color.white;
                return;
            }

            // Row 1: Rotation Controls [ ◀ Turn Dial (Q) ]  [ Dial Position: IV ]  [ Turn Dial ▶ (E) ]
            float row1Y = panelY + 32f;
            float btnW = 140f;
            float btnH = 32f;

            // Prev Button
            Rect prevRect = new Rect(panelX + 16f, row1Y, btnW, btnH);
            if (GUI.Button(prevRect, "◀ Turn Dial [Q]", m_ButtonStyle))
            {
                box.RotateDial(-1);
            }

            // Current Dial Indicator Badge
            Rect dialBadgeRect = new Rect(panelX + 16f + btnW + 10f, row1Y, panelW - (btnW * 2f + 52f), btnH);
            GUI.color = new Color(0.16f, 0.19f, 0.26f, 0.95f);
            GUI.DrawTexture(dialBadgeRect, m_SolidTexture);
            GUI.color = new Color(1f, 0.85f, 0.35f, 0.85f);
            DrawRectBorder(dialBadgeRect, 1.5f);
            GUI.color = Color.white;
            GUI.Label(dialBadgeRect, $"DIAL: {box.CurrentRomanNumeral}", m_HeaderStyle);

            // Next Button
            Rect nextRect = new Rect(panelX + panelW - btnW - 16f, row1Y, btnW, btnH);
            if (GUI.Button(nextRect, "Turn Dial ▶ [E]", m_ButtonStyle))
            {
                box.RotateDial(1);
            }

            // Row 2: Direct Number Dial Selection [1 (I)] [2 (II)] [3 (III)] [4 (IV)]
            float row2Y = panelY + 70f;
            float qBtnW = (panelW - 32f - 30f) / 4f;

            for (int i = 1; i <= 4; i++)
            {
                int val = i;
                Rect numRect = new Rect(panelX + 16f + (i - 1) * (qBtnW + 10f), row2Y, qBtnW, 30f);
                bool isCurrent = box.CurrentDial == val;

                // Clean button labels without spoiling IV
                string label = $"{i} ({ClockworkPuzzleBox.RomanNumerals[i - 1]})";
                GUIStyle style = isCurrent ? m_ActiveTabButtonStyle : m_ButtonStyle;

                if (GUI.Button(numRect, label, style))
                {
                    box.SetDial(val);
                }
            }
        }

        private void RenderScrollModal()
        {
            if (m_ScrollModalAlpha <= 0.001f) return;

            // Smooth backdrop overlay
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.72f * m_ScrollModalAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), m_SolidTexture);

            // Parchment scroll dimensions
            float scrollW = Mathf.Clamp(Screen.width * 0.58f, 480f, 680f);
            float scrollH = Mathf.Clamp(Screen.height * 0.58f, 320f, 460f);
            float scrollX = (Screen.width - scrollW) * 0.5f;
            float scrollY = (Screen.height - scrollH) * 0.5f;
            Rect scrollRect = new Rect(scrollX, scrollY, scrollW, scrollH);

            // Aged illuminated parchment background
            GUI.color = new Color(0.95f, 0.90f, 0.78f, m_ScrollModalAlpha);
            GUI.DrawTexture(scrollRect, m_BoxTexture);

            // Ornate medieval parchment border
            GUI.color = new Color(0.48f, 0.36f, 0.18f, m_ScrollModalAlpha);
            DrawRectBorder(scrollRect, 3.0f);
            DrawRectBorder(new Rect(scrollX + 6f, scrollY + 6f, scrollW - 12f, scrollH - 12f), 1.0f);

            // Header Banner
            Rect headerRect = new Rect(scrollX + 20f, scrollY + 18f, scrollW - 40f, 34f);
            GUI.color = new Color(0.28f, 0.18f, 0.08f, m_ScrollModalAlpha);
            GUI.Label(headerRect, $"⚜  {m_ScrollModalTitle.ToUpper()}  ⚜", m_HeaderStyle);

            // Decorative divider line
            GUI.color = new Color(0.55f, 0.42f, 0.22f, 0.8f * m_ScrollModalAlpha);
            GUI.DrawTexture(new Rect(scrollX + 40f, scrollY + 52f, scrollW - 80f, 2f), m_SolidTexture);

            // Scroll body text (illuminated parchment typography)
            Rect contentRect = new Rect(scrollX + 32f, scrollY + 66f, scrollW - 64f, scrollH - 128f);
            GUI.color = new Color(0.18f, 0.12f, 0.06f, m_ScrollModalAlpha);
            GUI.Label(contentRect, m_ScrollModalContent, m_PromptStyle);

            // Close button
            float closeW = 200f;
            float closeH = 36f;
            Rect closeRect = new Rect(scrollX + (scrollW - closeW) * 0.5f, scrollY + scrollH - 48f, closeW, closeH);
            GUI.color = Color.white;
            if (GUI.Button(closeRect, "Roll Up Scroll [E]", m_ActiveTabButtonStyle))
            {
                CloseScrollModal();
            }
        }
    }
}
