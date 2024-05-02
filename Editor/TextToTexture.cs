using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEditor;
using Object = UnityEngine.Object;

namespace TextToTexture
{
    public class TextToTexture : EditorWindow
    {
        #region Constants

        private const string POIYOMI_SHADER_NAME = ".poiyomi/• Poiyomi Toon •";
        private const string FALLBACK_SHADER_NAME = "Standard";

        private const string DUMMY_PARENT_NAME = "(Temp) TTT_Objects";

        private const string EDITORPREFS_SAVEPATH = "Text2TextureSavePath";
        private const string EDITORPREFS_DEFAULTSAVEPATH = "Assets/DreadScripts/TextToTexture/Generated Assets";

        private const string RESOURCE_FONTPATH = "Assets/DreadScripts/TextToTexture/Resources/TTT_DancingScript TMP.asset";
        private const string RESOURCE_FONTGUID = "e7d6a5821a387564caacc9929e10eb22";

        private const string RESOURCE_TEMPLATEPATH = "Assets/DreadScripts/TextToTexture/Resources/TTT_CanvasTemplate.prefab";
        private const string RESOURCE_TEMPLATEGUID = "2cd48bc473a380748ad104409753555e";

        private readonly string[] DimensionPresets =
        {
            "128x128",
            "256x256",
            "512x512",
            "1024x1024",
            "2048x2048",
            "4096x4096",
        };

        #endregion

        #region Automated Variables

        public static string savePath;

        private static GUIContent resetIcon;
        private static bool showExtra;
        private static bool isInCustomizing;

        private static Camera tempCamera;
        private static TextMeshProUGUI tempText;
        private static GameObject tempParent;
        private static Material tempMaterial;
        private static RenderTexture tempRenderTexture;

        #endregion

        #region Input Variables
        public static string textInput = "";
        public static TextureGenerationType generationType = TextureGenerationType.GenerateMaterial;

        public static TMP_FontAsset customFont;
        public static float outlineThickness = 0.075f;
        public static float extraPadding = 5;
        public static Vector2 offset;
        public static int resolutionWidth = 1024;
        public static int resolutionHeight = 1024;

        public static float verticalMargin = 100;
        public static float horizontalMargin = 100;

        public static bool invert;
        public static bool wrapText = true;
        #endregion

        #region Declarations
        public enum TextureGenerationType
        {
            GenerateNormalMap,
            GenerateMask,
            GenerateCustomTexture,
            GenerateMaterial
        }
        #endregion

        [MenuItem("DreadTools/Utilities/Text2Texture")]
        public static void showWindow()
        {
            var title = GetWindow<TextToTexture>(false, "Text2Texture", true).titleContent;
            title.image = EditorGUIUtility.IconContent("GUIText Icon").image;
        }

        private void OnGUI()
        {
            EditorGUI.BeginDisabledGroup(isInCustomizing);

            GUIStyle iconStyle = new GUIStyle() {margin = new RectOffset(0, 0, 1, 1)};

            using (new GUILayout.VerticalScope("helpbox"))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Text Input", "boldlabel");
                    GUILayout.FlexibleSpace();
                }
                textInput = EditorGUILayout.TextArea(textInput);
                generationType = (TextureGenerationType)EditorGUILayout.EnumPopup(generationType);
            }

            using (new GUILayout.VerticalScope("box"))
            {
                showExtra = EditorGUILayout.Foldout(showExtra, "Extra Settings");

                if (showExtra)
                {

                    using (new GUILayout.HorizontalScope("helpbox"))
                        customFont = (TMP_FontAsset) EditorGUILayout.ObjectField("Custom Font", customFont, typeof(TMP_FontAsset), false);

                    using (new GUILayout.HorizontalScope("helpbox"))
                        using (new EditorGUI.DisabledScope(generationType != TextureGenerationType.GenerateNormalMap && generationType != TextureGenerationType.GenerateMaterial))
                            outlineThickness = Mathf.Clamp01(EditorGUILayout.FloatField("Normal Thickness", outlineThickness));

                    using (new GUILayout.HorizontalScope("helpbox"))
                        extraPadding = EditorGUILayout.FloatField("Padding", extraPadding);

                    using (new GUILayout.HorizontalScope("helpbox"))
                    {
                        GUILayout.Label("Offset", GUILayout.Width(136));
                        offset = EditorGUILayout.Vector2Field(GUIContent.none, offset);
                    }

                    using (new GUILayout.HorizontalScope("helpbox"))
                    {
                        GUILayout.Label("Margin (%)", GUILayout.Width(136));

                        EditorGUIUtility.labelWidth = 10;
                        horizontalMargin = EditorGUILayout.FloatField(new GUIContent("X", "Width"), horizontalMargin);
                        verticalMargin = EditorGUILayout.FloatField(new GUIContent("Y", "Height"), verticalMargin);
                        EditorGUIUtility.labelWidth = 0;
                    }

                    using (new GUILayout.HorizontalScope("helpbox"))
                    {
                        GUILayout.Label("Resolution", GUILayout.Width(136));

                        EditorGUIUtility.labelWidth = 0;

                        EditorGUIUtility.labelWidth = 10;
                        resolutionWidth = EditorGUILayout.IntField(new GUIContent("X", "Width"), resolutionWidth);
                        resolutionHeight = EditorGUILayout.IntField(new GUIContent("Y", "Height"), resolutionHeight);
                        EditorGUIUtility.labelWidth = 0;


                        int dummy = -1;
                        EditorGUI.BeginChangeCheck();
                        dummy = EditorGUILayout.Popup(dummy, DimensionPresets, GUILayout.Width(18));
                        if (EditorGUI.EndChangeCheck())
                        {
                            string[] dimensions = ((string) DimensionPresets.GetValue(dummy)).Split('x');
                            resolutionWidth = int.Parse(dimensions[0]);
                            resolutionHeight = int.Parse(dimensions[1]);
                        }

                        if (GUILayout.Button(resetIcon, iconStyle, GUILayout.Height(18), GUILayout.Width(18)))
                            resolutionWidth = resolutionHeight = 1024;
                    }

                    using (new GUILayout.HorizontalScope("helpbox"))
                    {
                        wrapText = EditorGUILayout.Toggle("Wrap Text", wrapText);

                        using (new EditorGUI.DisabledScope((int)generationType > 1))
                            invert = EditorGUILayout.Toggle(new GUIContent("Invert Map"), invert);
                    }
                }
            }

            EditorGUI.EndDisabledGroup();

            if (!isInCustomizing)
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(textInput)))
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generate"))
                    {
                        switch (generationType)
                        {
                            case TextureGenerationType.GenerateNormalMap:
                            case TextureGenerationType.GenerateMask:
                                GenerateInit();
                                break;
                            case TextureGenerationType.GenerateCustomTexture:
                                isInCustomizing = true;
                                GenerateInit();
                                break;
                            case TextureGenerationType.GenerateMaterial:

                                generationType = TextureGenerationType.GenerateNormalMap;
                                Texture2D normalTexture = GenerateInit();

                                generationType = TextureGenerationType.GenerateMask;
                                bool oldInvert = invert;
                                invert = true;
                                Texture2D maskTexture = GenerateInit();
                                invert = oldInvert;

                                generationType = TextureGenerationType.GenerateMaterial;

                                Shader currentShader = Shader.Find(POIYOMI_SHADER_NAME) ?? Shader.Find(FALLBACK_SHADER_NAME);
                                Material newMaterial = new Material(currentShader);
                                #region Standard/Generic Property Set
                                newMaterial.SetTexture("_MainTex", maskTexture);
                                newMaterial.SetTexture("_MetallicGlossMap", maskTexture);
                                newMaterial.SetTexture("_BumpMap", normalTexture);
                                newMaterial.SetFloat("_Glossiness", 0.85f);
                                newMaterial.SetFloat("_GlossMapScale", 0.85f);
                                #endregion

                                #region Poiyomi Propert Set
                                newMaterial.EnableKeyword("VIGNETTE_CLASSIC");
                                newMaterial.SetTexture("_BRDFSpecularMap", maskTexture);
                                newMaterial.SetTexture("_BRDFMetallicMap", maskTexture);
                                newMaterial.SetFloat("_LightingMode", 1);
                                newMaterial.SetFloat("_LightingStandardSmoothness", 0.85f);
                                newMaterial.SetFloat("_EnableBRDF", 1);
                                newMaterial.SetFloat("_BRDFMetallic", 1);
                                newMaterial.SetFloat("_BRDFGlossiness", 0.85f);
                                #endregion
                                string newMatName = Regex.Replace(textInput, "[^\\w\\._ ]", "");
                                if (newMatName.Length > 30) newMatName = "Generated TextMaterial";
                                string newMatPath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{newMatName}.mat");
                                AssetDatabase.CreateAsset(newMaterial, newMatPath);
                                EditorGUIUtility.PingObject(newMaterial);
                                break;
                        }
                        
                    }
                }
            }
            else
            {
                if (!tempCamera) isInCustomizing = false;
                if (GUILayout.Button("Save Texture"))
                    GenerateTexture();
            }


            DrawSeparator();
            savePath = AssetFolderPath(savePath, "New Textures Path", EDITORPREFS_SAVEPATH, false);
            Credit();
        }

        private Texture2D GenerateInit()
        {
            GameObject canvasTemplate = ReadyResource<GameObject>(RESOURCE_TEMPLATEPATH, RESOURCE_TEMPLATEGUID);
            customFont = customFont ?? ReadyResource<TMP_FontAsset>(RESOURCE_FONTPATH, RESOURCE_FONTGUID);

            tempRenderTexture = new RenderTexture(resolutionWidth, resolutionHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            float resolutionRatio = (float) resolutionWidth / resolutionHeight;

            tempParent = GameObject.Find(DUMMY_PARENT_NAME);
            if (tempParent) DestroyImmediate(tempParent);
            tempParent = new GameObject(DUMMY_PARENT_NAME)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                transform =
                {
                    position = new Vector3(420, 666, 69)
                }
            };

            GameObject canvas = Instantiate(canvasTemplate, tempParent.transform);
            if (generationType == TextureGenerationType.GenerateCustomTexture) EditorGUIUtility.PingObject(canvas.GetInstanceID());

            tempCamera = canvas.GetComponentInChildren<Camera>();
            tempText = canvas.GetComponentInChildren<TextMeshProUGUI>();
            RectTransform textRect = tempText.GetComponent<RectTransform>();

            tempCamera.transform.Translate(-offset);
            tempCamera.orthographicSize = 80 + extraPadding * 2;

            tempText.enableWordWrapping = wrapText;
            tempText.font = customFont;
            tempText.text = textInput;

            switch (generationType)
            {
                case TextureGenerationType.GenerateNormalMap:
                    CreateTemporaryMaterial(tempText.fontSharedMaterial, true, true);
                    break;
                case TextureGenerationType.GenerateMask:
                    CreateTemporaryMaterial(tempText.fontSharedMaterial, true, false);
                    break;
                case TextureGenerationType.GenerateCustomTexture:
                    CreateTemporaryMaterial(tempText.fontSharedMaterial, false, false);
                    break;
            }


            textRect.sizeDelta = new Vector2(160 * resolutionRatio * (horizontalMargin/100), 160 * (verticalMargin / 100));


            return isInCustomizing ? null : GenerateTexture();
        }

        private Texture2D GenerateTexture()
        {
            isInCustomizing = false;
            tempCamera.targetTexture = tempRenderTexture;
            RenderTexture.active = tempRenderTexture;

            ReadyPath(savePath);
            byte[] textureBytes = null;

            switch (generationType)
            {
                case TextureGenerationType.GenerateNormalMap:
                {

                    tempMaterial.SetFloat("_LightAngle", 0);
                    Texture2D bottomTexture = CaptureRender();

                    tempMaterial.SetFloat("_LightAngle", 6.28f / 4);
                    Texture2D rightTexture = CaptureRender();

                    tempMaterial.SetFloat("_LightAngle", 6.28f / 4 * 2);
                    Texture2D topTexture = CaptureRender();

                    tempMaterial.SetFloat("_LightAngle", 6.28f / 4 * 3);
                    Texture2D leftTexture = CaptureRender();


                    Color[] bottomPixels = bottomTexture.GetPixels();
                    Color[] rightPixels = rightTexture.GetPixels();
                    Color[] topPixels = topTexture.GetPixels();
                    Color[] leftPixels = leftTexture.GetPixels();

                    Color[] newColors = new Color[resolutionHeight * resolutionWidth];

                    Parallel.For(0, resolutionHeight, i =>
                    {
                        Parallel.For(0, resolutionWidth, j =>
                        {
                            int currentIndex = i * resolutionWidth + j;
                            float horizontalInfluence = 0.5f + leftPixels[currentIndex].r / 2 - rightPixels[currentIndex].r / 2;
                            float verticalInfluence = 0.5f + bottomPixels[currentIndex].r / 2 - topPixels[currentIndex].r / 2;
                            float blueColor = 1 - ((Mathf.Abs(horizontalInfluence - 0.5f) + Mathf.Abs(verticalInfluence - 0.5f)) / 2);
                            newColors[currentIndex] = new Color(horizontalInfluence, verticalInfluence, blueColor);
                        });
                    });

                    DestroyImmediate(rightTexture);
                    DestroyImmediate(topTexture);
                    DestroyImmediate(leftTexture);

                    bottomTexture.SetPixels(newColors);
                    bottomTexture.Apply();
                    textureBytes = bottomTexture.EncodeToPNG();
                    DestroyImmediate(bottomTexture);
                    break;
                }
                case TextureGenerationType.GenerateMask:
                {
                    CreateTemporaryMaterial(tempText.fontSharedMaterial, true, false);

                    tempCamera.backgroundColor = invert ? Color.white : Color.black;
                    Texture2D maskTexture = CaptureRender();
                    textureBytes = maskTexture.EncodeToPNG();
                    DestroyImmediate(maskTexture);

                    break;
                }
                case TextureGenerationType.GenerateCustomTexture:
                {
                    Texture2D newTexture = CaptureRender();
                    textureBytes = newTexture.EncodeToPNG();
                    DestroyImmediate(newTexture);
                    break;
                }
            }

            RenderTexture.active = null;
            tempRenderTexture.Release();

            Texture2D generatedTexture = SaveTexture(textureBytes);
            EditorGUIUtility.PingObject(generatedTexture);

            if (tempParent)
                DestroyImmediate(tempParent);

            return generatedTexture;
        }

        private static Texture2D SaveTexture(byte[] textureBytes)
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(tempMaterial));
            string suffix;
            switch (generationType)
            {
                case TextureGenerationType.GenerateNormalMap:
                    suffix = " Normal";
                    break;
                case TextureGenerationType.GenerateMask:
                    suffix = " Mask";
                    break;
                default:
                    suffix = string.Empty;
                    break;
            }
            string texturePath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{Regex.Replace(textInput, "[^\\w\\._ ]", "")}{suffix}.png");
            try
            {
                File.WriteAllBytes(texturePath, textureBytes);
            }
            catch
            {
                texturePath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/Generated TextTexture.png");
                File.WriteAllBytes(texturePath, textureBytes);
            }

            AssetDatabase.ImportAsset(texturePath);

            TextureImporter textureSettings = (TextureImporter)AssetImporter.GetAtPath(texturePath);

            if (generationType == TextureGenerationType.GenerateNormalMap) textureSettings.textureType = TextureImporterType.NormalMap;
            if (generationType == TextureGenerationType.GenerateMask) textureSettings.sRGBTexture = false;

            textureSettings.maxTextureSize = 1024;
            textureSettings.crunchedCompression = true;
            textureSettings.compressionQuality = 100;

            textureSettings.streamingMipmaps = true;
            textureSettings.SaveAndReimport();

            return (Texture2D)AssetDatabase.LoadMainAssetAtPath(texturePath);
        }

        private static Texture2D CaptureRender()
        {
            tempCamera.Render();
            Texture2D capturedTexture = new Texture2D(resolutionWidth, resolutionHeight, TextureFormat.RGBA32, false);
            capturedTexture.ReadPixels(new Rect(0, 0, resolutionWidth, resolutionHeight), 0, 0);
            return capturedTexture;
        }

        private static void SetRequiredMaterialSettings(Material m, bool isNormal)
        {
            foreach (var key in m.shaderKeywords)
                m.DisableKeyword(key);

            if (isNormal)
            {
                m.EnableKeyword("BEVEL_ON");
                m.SetFloat("_OutlineWidth", outlineThickness);
            }

            m.SetFloat("_ShaderFlags", 0);
            m.SetFloat("_Bevel", isNormal ? invert ? -1 : 1 : 0);
            m.SetFloat("_BevelOffset", 0);
            m.SetFloat("_BevelClamp", 0);
            m.SetFloat("_BevelRoundness", 0);
            m.SetFloat("_BevelWidth", 0.5f);

            m.SetFloat("_Ambient", 1);
            m.SetFloat("_SpecularPower", 1);
            m.SetFloat("_Reflectivity", 5);
            m.SetFloat("_Diffuse", 0);

            m.SetFloat("_BumpFace", 0);
            m.SetFloat("_BumpOutline", 0);

            m.SetColor("_FaceColor", isNormal ? Color.clear : invert ? Color.black : Color.white);
            m.SetColor("_OutlineColor", isNormal ? Color.black : Color.clear);
            m.SetColor("_ReflectFaceColor", Color.clear);
            m.SetColor("_ReflectOutlineColor", Color.clear);
            m.SetColor("_SpecularColor", new Color(1.498039f, 1.498039f, 1.498039f));
        }

        private static void CreateTemporaryMaterial(Material original, bool ForceSettings, bool isNormal)
        {
            ReadyPath($"{savePath}/Temp");
            string path = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/Temp/(Temp) {original.name}.mat");
            tempMaterial = CopyAssetAndReturn(original, path);
            if (ForceSettings) SetRequiredMaterialSettings(tempMaterial, isNormal);
            tempText.fontSharedMaterial = tempMaterial;
        }

        private void OnEnable()
        {
            savePath = EditorPrefs.GetString(EDITORPREFS_SAVEPATH, EDITORPREFS_DEFAULTSAVEPATH);
            resetIcon = new GUIContent(EditorGUIUtility.IconContent("d_Refresh")) {tooltip = "Reset Dimensions"};
        }

        #region DSHelper Methods

        private static T ReadyResource<T>(string resourcePath, string resourceGUID = "", string msg = "") where T : Object
        {
            T asset = Resources.Load<T>(resourcePath);
            if (asset) return asset;

            if (!string.IsNullOrWhiteSpace(resourceGUID))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(resourceGUID));
                if (asset) return asset;
            }

            if (string.IsNullOrWhiteSpace(msg)) msg = $"Required Resources Asset at Resource Path {resourcePath} not found! Execution may not proceed.";
            EditorUtility.DisplayDialog("Error", msg, "Heck");
            throw new NullReferenceException(msg);
        }

        private static void ReadyPath(string path)
        {
            if (Directory.Exists(path)) return;

            Directory.CreateDirectory(path);
            AssetDatabase.ImportAsset(path);
        }

        private static T CopyAssetAndReturn<T>(T obj, string newPath) where T : Object
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (!mainAsset) return null;
            if (obj != mainAsset)
            {
                T newAsset = Object.Instantiate(obj);
                AssetDatabase.CreateAsset(newAsset, newPath);
                return newAsset;
            }

            AssetDatabase.CopyAsset(assetPath, newPath);
            return AssetDatabase.LoadAssetAtPath<T>(newPath);
        }

        private static string AssetFolderPath(string variable, string title, string playerpref, bool isPlayerPrefs = true)
        {
            using (new GUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(title, variable);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : string.Empty, string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return null;

                    if (!dummyPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return null;
                    }

                    variable = FileUtil.GetProjectRelativePath(dummyPath);
                    if (isPlayerPrefs)
                        PlayerPrefs.SetString(playerpref, variable);
                    else
                        EditorPrefs.SetString(playerpref, variable);
                }
            }

            return variable;
        }

        private static void DrawSeparator(int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
            EditorGUI.DrawRect(r, lineColor);
        }

        private static void Credit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Dreadrith#3238", "boldlabel"))
                    Application.OpenURL("https://linktr.ee/Dreadrith");
            }
        }

        #endregion
    }
}
