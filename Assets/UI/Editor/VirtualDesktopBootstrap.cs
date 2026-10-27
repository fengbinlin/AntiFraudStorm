using System.IO;
using AntiFraudStorm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI.Editor
{
    /// <summary>
    /// Wires FakeWindowsDesktop virtual desktop UI only (no scene routing yet).
    /// Menu: AntiFraudStorm / UI / Bootstrap Virtual Desktop Scene
    /// </summary>
    public static class VirtualDesktopBootstrap
    {
        const string PanelSettingsPath = "Assets/UI/PanelSettings.asset";
        const string DesktopUxmlPath = "Assets/UI/Documents/VirtualDesktop.uxml";
        const string FakeWindowsDesktopPath = "Assets/Scenes/FakeWindowsDesktop.unity";

        [MenuItem("AntiFraudStorm/UI/Bootstrap Virtual Desktop Scene")]
        public static void Bootstrap()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DesktopUxmlPath);
            if (panel == null || uxml == null)
            {
                Debug.LogError("[UI] Missing PanelSettings or VirtualDesktop.uxml");
                return;
            }

            EnsureFakeWindowsDesktop(panel, uxml);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI] Virtual desktop UI wired into FakeWindowsDesktop.");
        }

        static void EnsureFakeWindowsDesktop(PanelSettings panel, VisualTreeAsset uxml)
        {
            if (!File.Exists(FakeWindowsDesktopPath))
            {
                Debug.LogError($"[UI] Scene missing: {FakeWindowsDesktopPath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(FakeWindowsDesktopPath, OpenSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.48f, 0.78f, 0.97f, 1f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            var uiRoot = GameObject.Find("VirtualDesktopUI");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("VirtualDesktopUI");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create VirtualDesktopUI");
            }

            var doc = uiRoot.GetComponent<UIDocument>();
            if (doc == null)
                doc = Undo.AddComponent<UIDocument>(uiRoot);

            doc.panelSettings = panel;
            doc.visualTreeAsset = uxml;
            doc.sortingOrder = 100;
            EditorUtility.SetDirty(doc);
            EditorUtility.SetDirty(uiRoot);

            if (uiRoot.GetComponent<VirtualDesktopController>() == null)
                Undo.AddComponent<VirtualDesktopController>(uiRoot);

            if (GameObject.Find("EventSystem") == null)
            {
                var eventSystem = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
                Undo.AddComponent<UnityEngine.EventSystems.EventSystem>(eventSystem);
                Undo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>(eventSystem);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
