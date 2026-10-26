using UnityEngine;
using UnityEngine.UIElements;

namespace AntiFraudStorm.UI
{
    /// <summary>
    /// Binds MainMenu.uxml. Attach to the same GameObject as UIDocument,
    /// or assign the document reference in the Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] UIDocument document;

        Button _startButton;
        Button _settingsButton;
        Button _quitButton;

        void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            var root = document.rootVisualElement;
            _startButton = root.Q<Button>("btn-start");
            _settingsButton = root.Q<Button>("btn-settings");
            _quitButton = root.Q<Button>("btn-quit");

            _startButton?.RegisterCallback<ClickEvent>(OnStartClicked);
            _settingsButton?.RegisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton?.RegisterCallback<ClickEvent>(OnQuitClicked);
        }

        void OnDisable()
        {
            _startButton?.UnregisterCallback<ClickEvent>(OnStartClicked);
            _settingsButton?.UnregisterCallback<ClickEvent>(OnSettingsClicked);
            _quitButton?.UnregisterCallback<ClickEvent>(OnQuitClicked);
        }

        void OnStartClicked(ClickEvent _)
        {
            Debug.Log("[MainMenu] Start");
        }

        void OnSettingsClicked(ClickEvent _)
        {
            Debug.Log("[MainMenu] Settings");
        }

        void OnQuitClicked(ClickEvent _)
        {
            Application.Quit();
        }
    }
}
