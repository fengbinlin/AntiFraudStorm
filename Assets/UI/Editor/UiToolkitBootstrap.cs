using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI.Editor
{
    /// <summary>
    /// Creates PanelSettings and wires SampleScene UIDocument once assets exist.
    /// Menu: AntiFraudStorm / UI / Bootstrap UI Toolkit
    /// </summary>
    public static class UiToolkitBootstrap
    {
        const string UiRoot = "Assets/UI";
        const string PanelSettingsPath = UiRoot + "/PanelSettings.asset";
        const string ThemePath = UiRoot + "/Themes/AppTheme.tss";
        const string MainMenuUxmlPath = UiRoot + "/Documents/MainMenu.uxml";
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("AntiFraudStorm/UI/Bootstrap UI Toolkit")]
        public static void Bootstrap()
        {
            EnsureFolders();
            var panelSettings = EnsurePanelSettings();
            EnsureSampleSceneUi(panelSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[UI] Bootstrap complete: PanelSettings + SampleScene UIDocument.");
        }

        [InitializeOnLoadMethod]
        static void AutoBootstrapIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.delayCall += () =>
            {
                if (File.Exists(PanelSettingsPath))
                    return;
                if (!File.Exists(ThemePath) || !File.Exists(MainMenuUxmlPath))
                    return;

                Bootstrap();
            };
        }

        static void EnsureFolders()
        {
            CreateFolder("Assets", "UI");
            CreateFolder(UiRoot, "Themes");
            CreateFolder(UiRoot, "Styles");
            CreateFolder(UiRoot, "Documents");
            CreateFolder(UiRoot, "Components");
            CreateFolder(UiRoot, "Scripts");
            CreateFolder(UiRoot, "Editor");
        }

        static void CreateFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                AssignTheme(existing);
                return existing;
            }

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            AssignTheme(panel);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        static void AssignTheme(PanelSettings panel)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogWarning($"[UI] Theme not found at {ThemePath}");
                return;
            }

            panel.themeStyleSheet = theme;
            EditorUtility.SetDirty(panel);
        }

        static void EnsureSampleSceneUi(PanelSettings panelSettings)
        {
            if (!File.Exists(SampleScenePath))
            {
                Debug.LogWarning($"[UI] Sample scene missing: {SampleScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            var uiRoot = GameObject.Find("UIRoot");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("UIRoot");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create UIRoot");
            }

            var doc = uiRoot.GetComponent<UIDocument>();
            if (doc == null)
                doc = Undo.AddComponent<UIDocument>(uiRoot);

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxmlPath);
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;

            var controller = uiRoot.GetComponent<MainMenuController>();
            if (controller == null)
                Undo.AddComponent<MainMenuController>(uiRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
