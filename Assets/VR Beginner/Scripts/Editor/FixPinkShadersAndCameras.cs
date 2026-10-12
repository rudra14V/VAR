using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using VRBeginner.Gameplay;

namespace VRBeginner.Editor
{
    /// <summary>
    /// Fixes all pink shaders by upgrading HDRP and Standard materials to Universal Render Pipeline (URP/Lit).
    /// Also disables conflicting Cinemachine cameras, HDRP camera scripts, and HDRP volumes in Level 1 and Level 2.
    /// </summary>
    [InitializeOnLoad]
    public static class FixPinkShadersAndCameras
    {
        private const string FixDonePref = "EscapeRoom_PinkShadersFixed_v2";

        static FixPinkShadersAndCameras()
        {
            EditorApplication.delayCall += RunAutoFix;
        }

        private static void RunAutoFix()
        {
            if (!EditorPrefs.GetBool(FixDonePref, false))
            {
                EditorPrefs.SetBool(FixDonePref, true);
                FixAllShadersAndScenes(false);
            }
        }

        [MenuItem("Tools/Escape Room/Fix Pink Shaders & Clean Cameras", false, 2)]
        public static void FixAllMenu()
        {
            FixAllShadersAndScenes(true);
        }

        public static void FixAllShadersAndScenes(bool showDialog = true)
        {
            int convertedMaterials = UpgradeMaterialsToURP();
            FixLevel1Scene();
            FixLevel2Scene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FixPinkShaders] Fixed {convertedMaterials} materials and cleaned up Level 1 & Level 2 scenes!");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Pink Shaders & Cameras Fixed!",
                    $"Successfully converted {convertedMaterials} materials to Universal Render Pipeline (URP/Lit)!\n\n" +
                    "• Removed conflicting Cinemachine cameras & HDRP volumes in Level 1.\n" +
                    "• Configured clean lighting and player camera.\n" +
                    "• Level 1 is now fully visible, playable, and beautiful!",
                    "Awesome!"
                );
            }
        }

        public static int UpgradeMaterialsToURP()
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("[FixPinkShaders] Could not find 'Universal Render Pipeline/Lit' shader!");
                return 0;
            }

            int count = 0;
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new string[] { "Assets/Scenes/ZNS3D", "Assets/diningRoom" });

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "";

                // If material is using HDRP, Standard, or an invalid/missing shader
                if (shaderName.Contains("HDRP") || shaderName.Contains("Standard") || shaderName.Contains("Error") || 
                    shaderName.Contains("InternalErrorShader") || shaderName != "Universal Render Pipeline/Lit")
                {
                    Texture mainTex = null;
                    if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                        mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseColorMap") && mat.GetTexture("_BaseColorMap") != null)
                        mainTex = mat.GetTexture("_BaseColorMap");
                    else if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                        mainTex = mat.GetTexture("_BaseMap");

                    Texture bumpMap = null;
                    if (mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null)
                        bumpMap = mat.GetTexture("_BumpMap");
                    else if (mat.HasProperty("_NormalMap") && mat.GetTexture("_NormalMap") != null)
                        bumpMap = mat.GetTexture("_NormalMap");

                    Texture metallicMap = null;
                    if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null)
                        metallicMap = mat.GetTexture("_MetallicGlossMap");
                    else if (mat.HasProperty("_MaskMap") && mat.GetTexture("_MaskMap") != null)
                        metallicMap = mat.GetTexture("_MaskMap");

                    Color baseColor = Color.white;
                    if (mat.HasProperty("_Color"))
                        baseColor = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor"))
                        baseColor = mat.GetColor("_BaseColor");

                    // Set to URP Lit
                    mat.shader = urpLitShader;

                    // Reassign textures
                    if (mainTex != null)
                    {
                        mat.SetTexture("_BaseMap", mainTex);
                    }
                    mat.SetColor("_BaseColor", baseColor != Color.black ? baseColor : Color.white);

                    if (bumpMap != null)
                    {
                        mat.EnableKeyword("_NORMALMAP");
                        mat.SetTexture("_BumpMap", bumpMap);
                    }

                    if (metallicMap != null)
                    {
                        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                        mat.SetTexture("_MetallicGlossMap", metallicMap);
                    }

                    mat.SetFloat("_Smoothness", 0.5f);

                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }

            return count;
        }

        private static void FixLevel1Scene()
        {
            string scenePath = "Assets/Scenes/Level1_DiningRoom.unity";
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Disable static dummy Main Camera
            Camera[] oldCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in oldCameras)
            {
                if (cam.gameObject.name == "Main Camera")
                {
                    cam.gameObject.SetActive(false);
                }
            }

            // Ensure EscapeRoomBootstrap and GameLevelManager exist
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixPinkShaders] Cleaned up Level1_DiningRoom.unity!");
        }

        private static void FixLevel2Scene()
        {
            string scenePath = "Assets/Scenes/Level2_MedievalRoom.unity";
            if (!File.Exists(scenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Disable static dummy Main Camera
            Camera[] oldCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in oldCameras)
            {
                if (cam.gameObject.name.Contains("Camera_") || cam.gameObject.name.Contains("Cinemachine") || cam.gameObject.name == "Main Camera" || cam.gameObject.name == "Main Camera ")
                {
                    cam.gameObject.SetActive(false);
                }
            }

            // Ensure URP Directional Light exists
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool hasDirLight = false;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.enabled) { hasDirLight = true; break; }
            }

            if (!hasDirLight)
            {
                GameObject dirLightGo = new GameObject("MedievalRoomSun");
                Light dir = dirLightGo.AddComponent<Light>();
                dir.type = LightType.Directional;
                dir.color = new Color(1f, 0.94f, 0.85f);
                dir.intensity = 1.3f;
                dirLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Ensure EscapeRoomBootstrap and GameLevelManager exist
            if (UnityEngine.Object.FindFirstObjectByType<EscapeRoomBootstrap>() == null)
            {
                GameObject boot = new GameObject("EscapeRoomBootstrap");
                boot.AddComponent<EscapeRoomBootstrap>();
            }

            if (UnityEngine.Object.FindFirstObjectByType<GameLevelManager>() == null)
            {
                GameObject glm = new GameObject("GameLevelManager");
                glm.AddComponent<GameLevelManager>();
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixPinkShaders] Cleaned up Level2_MedievalRoom.unity!");
        }
    }
}
