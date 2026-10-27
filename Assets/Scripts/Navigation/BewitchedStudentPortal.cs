using AntiFraudStorm.Player;
using AntiFraudStorm.SceneFlow;
using UnityEngine;

namespace AntiFraudStorm.Navigation
{
    /// <summary>
    /// Exposes an E-key interaction only after a digital human has been
    /// bewitched by a nearby scam flyer.
    /// </summary>
    public class BewitchedStudentPortal : MonoBehaviour
    {
        const float InteractionRadius = 2.8f;
        const float PromptHeight = 2.15f;

        static Font _fallbackFont;

        DigitalHumanWalker _walker;
        Font _promptFont;
        TextMesh _prompt;
        bool _nearPlayer;
        bool _transitioning;

        public void Configure(DigitalHumanWalker walker, Font promptFont)
        {
            _walker = walker;
            _promptFont = promptFont;
        }

        void Update()
        {
            if (_transitioning)
                return;

            if (_walker == null ||
                !_walker.IsBewitched ||
                CampusComputerSession.IsFrozen)
            {
                SetPromptVisible(false);
                return;
            }

            PlayerInputReader input = PlayerInputReader.Active;
            if (input == null)
            {
                SetPromptVisible(false);
                return;
            }

            Vector3 offset = input.transform.position - transform.position;
            offset.y = 0f;
            bool near = offset.sqrMagnitude <= InteractionRadius * InteractionRadius;
            SetPromptVisible(near);
            if (!near)
                return;

            if (!input.InteractPressed && !Input.GetKeyDown(KeyCode.E))
                return;

            _transitioning = true;
            SetPromptVisible(false);
            if (CampusComputerSession.Instance == null)
            {
                Debug.LogError(
                    "[BewitchedStudent] CampusComputerSession is missing from School.",
                    this);
                _transitioning = false;
                return;
            }

            CampusComputerSession.Instance.EnterDangerScene(_walker);
        }

        void LateUpdate()
        {
            if (_prompt == null || !_nearPlayer)
                return;

            Transform promptTransform = _prompt.transform;
            promptTransform.position = transform.position + Vector3.up * PromptHeight;
            Camera camera = Camera.main;
            if (camera == null)
                return;
            promptTransform.rotation = Quaternion.LookRotation(
                promptTransform.position - camera.transform.position,
                Vector3.up);
        }

        void SetPromptVisible(bool visible)
        {
            _nearPlayer = visible;
            EnsurePrompt();
            _prompt.gameObject.SetActive(visible);
        }

        void EnsurePrompt()
        {
            if (_prompt != null)
                return;

            var promptGo = new GameObject("BewitchedStudentInteractionPrompt");
            promptGo.transform.SetParent(transform, false);
            _prompt = promptGo.AddComponent<TextMesh>();
            _prompt.text = "按E劝阻";
            _prompt.anchor = TextAnchor.MiddleCenter;
            _prompt.alignment = TextAlignment.Center;
            _prompt.characterSize = 0.055f;
            _prompt.fontSize = 48;
            _prompt.fontStyle = FontStyle.Bold;
            _prompt.color = Color.black;

            Font font = _promptFont != null ? _promptFont : GetFallbackFont();
            if (font != null)
            {
                _prompt.font = font;
                MeshRenderer renderer = promptGo.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = font.material;
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sortingOrder = 1;
                }

                CreateOutlineCopies(promptGo.transform, font);
            }

            _prompt.gameObject.SetActive(false);
        }

        static void CreateOutlineCopies(Transform parent, Font font)
        {
            Vector3[] offsets =
            {
                new Vector3(-0.009f, 0f, 0f),
                new Vector3(0.009f, 0f, 0f),
                new Vector3(0f, -0.009f, 0f),
                new Vector3(0f, 0.009f, 0f),
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                var outlineGo = new GameObject($"PromptOutline{i + 1}");
                outlineGo.transform.SetParent(parent, false);
                outlineGo.transform.localPosition = offsets[i];

                var outline = outlineGo.AddComponent<TextMesh>();
                outline.text = "按E劝阻";
                outline.anchor = TextAnchor.MiddleCenter;
                outline.alignment = TextAlignment.Center;
                outline.characterSize = 0.055f;
                outline.fontSize = 48;
                outline.fontStyle = FontStyle.Bold;
                outline.color = Color.white;
                outline.font = font;

                MeshRenderer renderer =
                    outlineGo.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = font.material;
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sortingOrder = 0;
                }
            }
        }

        static Font GetFallbackFont()
        {
            if (_fallbackFont != null)
                return _fallbackFont;

            string[] preferred =
            {
                "Microsoft YaHei",
                "Microsoft YaHei UI",
                "SimHei",
                "Arial Unicode MS",
            };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < preferred.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (installed[j] == preferred[i])
                    {
                        _fallbackFont = Font.CreateDynamicFontFromOSFont(
                            preferred[i],
                            48);
                        return _fallbackFont;
                    }
                }
            }

            _fallbackFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _fallbackFont;
        }

        void OnDestroy()
        {
            if (_prompt != null)
                Destroy(_prompt.gameObject);
        }
    }
}
