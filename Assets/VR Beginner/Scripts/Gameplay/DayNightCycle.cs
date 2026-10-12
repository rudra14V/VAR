using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRBeginner.Gameplay
{
    /// <summary>
    /// Dynamic Day-Night Cycle system.
    /// Smoothly cycles lighting continuously: Morning -> Day -> Evening (sunset) -> Night (moonlight) -> Morning.
    /// Dynamically shifts Directional Light angle, color, intensity, ambient sky lighting,
    /// architectural castle windows, and exterior sky backdrops.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        public enum CyclePhase
        {
            Dawn,
            Day,
            Dusk,
            Night
        }

        [Header("Cycle Settings")]
        [Tooltip("Total real-world seconds for one full 24-hour day/night cycle.")]
        public float CycleDuration = 75.0f; // Smooth, engaging cycle pace

        [Range(0f, 24f)]
        public float CurrentHour = 8.0f; // Start at 8 AM (Morning)

        [Header("Light References")]
        public Light SunLight;

        [Header("Phase Colors")]
        public Color DawnSunColor = new Color(1.0f, 0.78f, 0.55f);
        public Color DaySunColor = new Color(1.0f, 0.96f, 0.90f);
        public Color DuskSunColor = new Color(1.0f, 0.46f, 0.18f);
        public Color NightSunColor = new Color(0.25f, 0.38f, 0.72f);

        [Header("Phase Intensities")]
        public float DawnIntensity = 0.90f;
        public float DayIntensity = 1.35f;
        public float DuskIntensity = 0.95f;
        public float NightIntensity = 0.35f;

        [Header("Exterior Window Backdrops")]
        public List<Renderer> WindowBackdrops = new List<Renderer>();

        private Material m_WindowBackdropMat;
        private List<Light> m_WindowBeams = new List<Light>();

        public CyclePhase CurrentPhase { get; private set; } = CyclePhase.Dawn;

        public string PhaseDisplayName
        {
            get
            {
                switch (CurrentPhase)
                {
                    case CyclePhase.Dawn: return "Morning (Dawn)";
                    case CyclePhase.Day: return "Midday";
                    case CyclePhase.Dusk: return "Evening (Sunset)";
                    case CyclePhase.Night: return "Night (Moonlight)";
                    default: return "Day";
                }
            }
        }

        public string PhaseIcon
        {
            get
            {
                switch (CurrentPhase)
                {
                    case CyclePhase.Dawn: return "🌅";
                    case CyclePhase.Day: return "☀️";
                    case CyclePhase.Dusk: return "🌇";
                    case CyclePhase.Night: return "🌙";
                    default: return "☀️";
                }
            }
        }

        public string TimeString
        {
            get
            {
                int hours = Mathf.FloorToInt(CurrentHour);
                int minutes = Mathf.FloorToInt((CurrentHour - hours) * 60f);
                string ampm = hours >= 12 ? "PM" : "AM";
                int displayHour = hours % 12;
                if (displayHour == 0) displayHour = 12;
                return $"{displayHour:00}:{minutes:00} {ampm}";
            }
        }

        private void Awake()
        {
            Instance = this;
            FindOrCreateSun();
            SetupWindowBackdrops();
        }

        private void Start()
        {
            UpdateLighting(0f);
        }

        private void Update()
        {
            float deltaHours = (Time.deltaTime / Mathf.Max(CycleDuration, 10f)) * 24.0f;
            CurrentHour = (CurrentHour + deltaHours) % 24.0f;

            UpdateLighting(Time.deltaTime);
        }

        private void FindOrCreateSun()
        {
            if (SunLight == null)
            {
                Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in allLights)
                {
                    if (l != null && l.type == LightType.Directional && l.enabled)
                    {
                        SunLight = l;
                        break;
                    }
                }
            }

            if (SunLight == null)
            {
                GameObject sunGo = new GameObject("DynamicDirectionalSun");
                SunLight = sunGo.AddComponent<Light>();
                SunLight.type = LightType.Directional;
                SunLight.shadows = LightShadows.Soft;
            }
        }

        private void SetupWindowBackdrops()
        {
            string sceneName = SceneManager.GetActiveScene().name.ToLower();

            // Level 2 and Level 3 have windows removed per user request:
            // "in level-02 remove windows", "in level-03 remove the windows and make it normal"
            if (sceneName.Contains("medieval") || sceneName.Contains("chamber") || sceneName.Contains("2") ||
                sceneName.Contains("witch") || sceneName.Contains("cottage") || sceneName.Contains("3"))
            {
                return;
            }

            // Create shared backdrop material for windows
            m_WindowBackdropMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            m_WindowBackdropMat.color = DawnSunColor;

            // Search for window objects in the scene to attach backdrops
            GameObject[] allGo = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go.name.Contains("Window", StringComparison.OrdinalIgnoreCase) && !go.name.Contains("Backdrop"))
                {
                    if (go.transform.Find("WindowSkyBackdrop") == null)
                    {
                        GameObject skyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        skyQuad.name = "WindowSkyBackdrop";
                        skyQuad.transform.SetParent(go.transform, false);
                        skyQuad.transform.localPosition = new Vector3(0f, 1.0f, 0.35f);
                        skyQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        skyQuad.transform.localScale = new Vector3(2.4f, 2.4f, 1f);

                        Collider col = skyQuad.GetComponent<Collider>();
                        if (col != null) Destroy(col);

                        Renderer r = skyQuad.GetComponent<Renderer>();
                        if (r != null)
                        {
                            r.material = m_WindowBackdropMat;
                            WindowBackdrops.Add(r);
                        }
                    }
                }
            }

            // If no existing window objects found, construct architectural castle windows with sky panes!
            if (WindowBackdrops.Count == 0)
            {
                EnsureRoomWindows();
            }
        }

        private void EnsureRoomWindows()
        {
            string sceneName = SceneManager.GetActiveScene().name.ToLower();

            if (sceneName.Contains("dining") || sceneName.Contains("1"))
            {
                // Level 1: Two grand gothic castle windows on the West Wall (x = -5.9m)
                CreateWindowAt(new Vector3(-5.88f, 2.4f, -2.5f), Quaternion.Euler(0f, 90f, 0f), 2.2f, 2.8f);
                CreateWindowAt(new Vector3(-5.88f, 2.4f, 2.5f), Quaternion.Euler(0f, 90f, 0f), 2.2f, 2.8f);
            }
            // Level 2 and Level 3 windows completely removed per user request
        }

        private void CreateWindowAt(Vector3 worldPos, Quaternion rotation, float width, float height)
        {
            GameObject winRoot = new GameObject($"CastleWindow_{worldPos.z:F1}");
            winRoot.transform.position = worldPos;
            winRoot.transform.rotation = rotation;

            var frameMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            frameMat.color = new Color(0.22f, 0.18f, 0.14f);

            // Left Post
            CreateBar(winRoot.transform, "LeftPost", new Vector3(-width * 0.5f, 0f, 0f), new Vector3(0.12f, height, 0.15f), frameMat);
            // Right Post
            CreateBar(winRoot.transform, "RightPost", new Vector3(width * 0.5f, 0f, 0f), new Vector3(0.12f, height, 0.15f), frameMat);
            // Sill
            CreateBar(winRoot.transform, "Sill", new Vector3(0f, -height * 0.5f, 0f), new Vector3(width + 0.15f, 0.15f, 0.22f), frameMat);
            // Lintel Arch
            CreateBar(winRoot.transform, "Lintel", new Vector3(0f, height * 0.5f, 0f), new Vector3(width + 0.15f, 0.18f, 0.18f), frameMat);

            // Window Mullions (cross bars)
            CreateBar(winRoot.transform, "MullionV", new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, height * 0.95f, 0.06f), frameMat);
            CreateBar(winRoot.transform, "MullionH", new Vector3(0f, 0f, 0.02f), new Vector3(width * 0.95f, 0.06f, 0.06f), frameMat);

            // Sky Pane Quad
            GameObject skyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyQuad.name = "WindowSkyPane";
            skyQuad.transform.SetParent(winRoot.transform, false);
            skyQuad.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            skyQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            skyQuad.transform.localScale = new Vector3(width * 0.96f, height * 0.96f, 1f);

            Collider col = skyQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer r = skyQuad.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = m_WindowBackdropMat;
                WindowBackdrops.Add(r);
            }

            // Window Sunlight / Moonlight Beam Light
            GameObject beamGo = new GameObject("WindowLightBeam");
            beamGo.transform.SetParent(winRoot.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, 0.2f, 0.4f);
            beamGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Light beamLight = beamGo.AddComponent<Light>();
            beamLight.type = LightType.Spot;
            beamLight.color = DawnSunColor;
            beamLight.intensity = 2.0f;
            beamLight.range = 8.5f;
            beamLight.spotAngle = 65f;
            m_WindowBeams.Add(beamLight);
        }

        private void CreateBar(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().material = mat;
            Collider c = cube.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        private void UpdateLighting(float dt)
        {
            if (SunLight == null) return;

            Color targetColor;
            float targetIntensity;
            float sunPitch;

            // 24 Hour Day-Night Schedule:
            // 05:00 - 08:30: Dawn / Morning
            // 08:30 - 17:00: Midday / Daylight
            // 17:00 - 20:30: Dusk / Sunset
            // 20:30 - 05:00: Night / Moonlit
            if (CurrentHour >= 5.0f && CurrentHour < 8.5f)
            {
                CurrentPhase = CyclePhase.Dawn;
                float t = (CurrentHour - 5.0f) / 3.5f;
                targetColor = Color.Lerp(NightSunColor, DawnSunColor, t);
                targetIntensity = Mathf.Lerp(NightIntensity, DawnIntensity, t);
                sunPitch = Mathf.Lerp(10f, 35f, t);
            }
            else if (CurrentHour >= 8.5f && CurrentHour < 17.0f)
            {
                CurrentPhase = CyclePhase.Day;
                float t = (CurrentHour - 8.5f) / 8.5f;
                targetColor = Color.Lerp(DawnSunColor, DaySunColor, Mathf.PingPong(t * 2f, 1f));
                targetIntensity = Mathf.Lerp(DawnIntensity, DayIntensity, Mathf.PingPong(t * 2f, 1f));
                sunPitch = Mathf.Lerp(35f, 65f, Mathf.Sin(t * Mathf.PI));
            }
            else if (CurrentHour >= 17.0f && CurrentHour < 20.5f)
            {
                CurrentPhase = CyclePhase.Dusk;
                float t = (CurrentHour - 17.0f) / 3.5f;
                targetColor = Color.Lerp(DaySunColor, DuskSunColor, t);
                targetIntensity = Mathf.Lerp(DayIntensity, DuskIntensity, t);
                sunPitch = Mathf.Lerp(35f, 8f, t);
            }
            else
            {
                CurrentPhase = CyclePhase.Night;
                float nightProgress;
                if (CurrentHour >= 20.5f)
                    nightProgress = (CurrentHour - 20.5f) / 8.5f;
                else
                    nightProgress = (CurrentHour + 3.5f) / 8.5f;

                targetColor = Color.Lerp(DuskSunColor, NightSunColor, Mathf.Clamp01(nightProgress * 2f));
                targetIntensity = Mathf.Lerp(DuskIntensity, NightIntensity, Mathf.Clamp01(nightProgress * 2f));
                sunPitch = Mathf.Lerp(15f, 40f, Mathf.Sin(nightProgress * Mathf.PI));
            }

            // Apply Sun Light Properties
            SunLight.color = targetColor;
            SunLight.intensity = targetIntensity;

            // Rotate Sun angle: yaw rotates around the horizon, pitch raises/lowers
            float sunYaw = (CurrentHour / 24.0f) * 360.0f - 90.0f;
            SunLight.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);

            // Ambient sky tint
            Color ambientColor = targetColor * 0.45f;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.ambientSkyColor = targetColor;

            // Compute sky vista color for windows & outdoor vistas
            Color skyVistaColor;
            switch (CurrentPhase)
            {
                case CyclePhase.Dawn:
                    skyVistaColor = new Color(1.0f, 0.72f, 0.48f);
                    break;
                case CyclePhase.Day:
                    skyVistaColor = new Color(0.55f, 0.78f, 1.0f);
                    break;
                case CyclePhase.Dusk:
                    skyVistaColor = new Color(0.95f, 0.40f, 0.15f);
                    break;
                case CyclePhase.Night:
                    skyVistaColor = new Color(0.08f, 0.12f, 0.28f);
                    break;
                default:
                    skyVistaColor = Color.white;
                    break;
            }

            if (m_WindowBackdropMat != null)
            {
                m_WindowBackdropMat.color = skyVistaColor;
                if (m_WindowBackdropMat.HasProperty("_BaseColor"))
                    m_WindowBackdropMat.SetColor("_BaseColor", skyVistaColor);
            }

            // Update all registered window backdrops and outdoor panoramas
            for (int i = 0; i < WindowBackdrops.Count; i++)
            {
                var r = WindowBackdrops[i];
                if (r != null && r.material != null)
                {
                    r.material.color = skyVistaColor;
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", skyVistaColor);
                }
            }

            // Update window light beam colors
            for (int i = 0; i < m_WindowBeams.Count; i++)
            {
                var b = m_WindowBeams[i];
                if (b != null)
                {
                    b.color = targetColor;
                    b.intensity = targetIntensity * 1.8f;
                }
            }
        }
    }
}
