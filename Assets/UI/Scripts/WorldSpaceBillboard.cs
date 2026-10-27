using UnityEngine;

namespace AntiFraudStorm.UI
{
    /// <summary>
    /// Keeps a world-space canvas facing the active gameplay camera.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceBillboard : MonoBehaviour
    {
        Canvas _canvas;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            _canvas.worldCamera = camera;
            transform.rotation = Quaternion.LookRotation(
                transform.position - camera.transform.position,
                Vector3.up);
        }
    }
}
