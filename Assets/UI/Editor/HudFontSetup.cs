using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI.Editor
{
    /// <summary>
    /// Builds TextCore FontAsset + PanelTextSettings for MaoKenZhuYuanTi.
    /// Menu: AntiFraudStorm / UI / Setup HUD Font
    /// </summary>
    public static class HudFontSetup
    {
        const string SourceFontPath = "Assets/ImportResource/MaoKenZhuYuanTi-MaokenZhuyuanTi-2.ttf";
        const string FontsFolder = "Assets/UI/Fonts";
        const string FontAssetPath = FontsFolder + "/MaokenZhuyuanTi.asset";
        const string TextSettingsPath = "Assets/UI/PanelTextSettings.asset";
        const string PanelSettingsPath = "Assets/UI/PanelSettings.asset";

        [MenuItem("AntiFraudStorm/UI/Setup HUD Font")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
                AssetDatabase.CreateFolder("Assets", "UI");
            if (!AssetDatabase.IsValidFolder(FontsFolder))
                AssetDatabase.CreateFolder("Assets/UI", "Fonts");

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[UI] Font not found: {SourceFontPath}");
                return;
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = FontAsset.CreateFontAsset(source);
                fontAsset.name = "MaokenZhuyuanTi";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                // Persist atlas / material sub-assets created by FontAsset.
                var materials = fontAsset.material;
                if (materials != null)
                    AssetDatabase.AddObjectToAsset(materials, fontAsset);
                if (fontAsset.atlasTexture != null)
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

                EditorUtility.SetDirty(fontAsset);
            }

            var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(textSettings, TextSettingsPath);
            }

            textSettings.defaultFontAsset = fontAsset;
            EditorUtility.SetDirty(textSettings);

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null)
            {
                panel.textSettings = textSettings;
                EditorUtility.SetDirty(panel);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UI] HUD font ready: {FontAssetPath}");
        }

        [InitializeOnLoadMethod]
        static void AutoSetupIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(SourceFontPath))
                    return;
                if (File.Exists(FontAssetPath) && File.Exists(TextSettingsPath))
                    return;
                Setup();
            };
        }
    }
}
