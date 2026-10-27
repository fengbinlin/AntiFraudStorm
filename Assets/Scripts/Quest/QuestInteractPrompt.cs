using UnityEngine;

namespace AntiFraudStorm.Quest
{
    /// <summary>
    /// World-space 3D prompt beside a quest point (TextMesh, not uGUI).
    /// </summary>
    public class QuestInteractPrompt : MonoBehaviour
    {
        [SerializeField] string promptText = "按E注册";
        [SerializeField] Font font;
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] float worldHeight = 0.055f;
        [SerializeField] Color color = Color.black;

        Transform _billboard;
        TextMesh _label;
        bool _visible;

        void Awake()
        {
            EnsureLabel();
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (_billboard == null || !_visible)
                return;

            _billboard.position = transform.position + worldOffset;
            Camera cam = Camera.main;
            if (cam == null)
                cam = FindObjectOfType<Camera>();
            if (cam != null)
                _billboard.rotation = Quaternion.LookRotation(_billboard.position - cam.transform.position, Vector3.up);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            EnsureLabel();
            if (_billboard != null)
                _billboard.gameObject.SetActive(visible);
        }

        void EnsureLabel()
        {
            if (_billboard != null)
                return;

            var go = new GameObject("QuestInteractPrompt3D");
            _billboard = go.transform;
            _billboard.SetParent(null, true);
            _billboard.position = transform.position + worldOffset;

            _label = go.AddComponent<TextMesh>();
            _label.text = promptText;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = worldHeight;
            _label.fontSize = 48;
            _label.fontStyle = FontStyle.Bold;
            _label.color = color;
            if (font != null)
            {
                _label.font = font;
                var rend = go.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.sharedMaterial = font.material;
                    rend.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                    rend.sortingOrder = 1;
                }

                CreateOutlineCopies(go.transform, font);
            }
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
                outline.text = "按E注册";
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

        void OnDisable()
        {
            if (_billboard != null)
                _billboard.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_billboard != null)
                Destroy(_billboard.gameObject);
        }
    }
}
