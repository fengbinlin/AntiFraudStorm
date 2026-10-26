using Animancer;
using AntiFraudStorm.Player;
using Layer_lab._3D_Casual_Character;
using UnityEditor;
using UnityEngine;

namespace AntiFraudStorm.Player.Editor
{
    public static class PlayerThirdPersonSetup
    {
        const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";

        const string IdlePath = "Assets/Layer lab/3D Casual Character/Animation/Anim@Stand_Idle1.FBX";
        const string WalkPath = "Assets/Layer lab/3D Casual Character/Animation/Anim@Action_Walk.FBX";
        const string RunPath = "Assets/Layer lab/3D Casual Character/Animation/Anim@Action_Run.FBX";
        const string JumpPath = "Assets/Layer lab/3D Casual Character/Animation/Anim@Action_Jump.FBX";

        [MenuItem("AntiFraudStorm/Player/Setup Third Person Controller")]
        public static void Setup()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                ConfigurePlayer(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Player] Setup complete. Camera is inside Player prefab — drag Player into scene and play. Remove any extra Main Camera in the scene.");
        }

        static void ConfigurePlayer(GameObject root)
        {
            var demoControl = root.GetComponent<CharacterControl>();
            if (demoControl != null)
                Object.DestroyImmediate(demoControl, true);

            // Demo dress-up: Awake hides Body_1/Body_2 and never restores without SetItem.
            foreach (var demoBase in root.GetComponentsInChildren<CharacterBase>(true))
                Object.DestroyImmediate(demoBase, true);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Body" || t.name == "Body_2")
                    t.gameObject.SetActive(true);
                else if (t.name == "Body_1")
                    t.gameObject.SetActive(false);
            }

            var controller = root.GetComponent<CharacterController>();
            if (controller == null)
                controller = root.AddComponent<CharacterController>();

            float height = FitCharacterController(root, controller);
            float pivotY = Mathf.Clamp(height * 0.75f, 1.0f, 1.8f);
            float camDist = Mathf.Clamp(height * 2.2f, 3.5f, 6f);

            Ensure<PlayerInputReader>(root);
            Ensure<ThirdPersonMotor>(root);
            var orchestrator = Ensure<ThirdPersonPlayerController>(root);

            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError("[Player] No Animator found under Player prefab.");
                return;
            }

            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;

            var animancer = animator.GetComponent<AnimancerComponent>();
            if (animancer == null)
                animancer = animator.gameObject.AddComponent<AnimancerComponent>();
            animancer.Animator = animator;

            var locomotion = animator.GetComponent<PlayerAnimancerLocomotion>();
            if (locomotion == null)
                locomotion = animator.gameObject.AddComponent<PlayerAnimancerLocomotion>();

            var idle = LoadClip(IdlePath, "Stand_Idle1");
            var walk = LoadClip(WalkPath, "Action_Walk");
            var run = LoadClip(RunPath, "Action_Run");
            var jump = LoadClip(JumpPath, "Action_Jump");
            locomotion.SetClips(idle, walk, run, jump);

            var soLoc = new SerializedObject(locomotion);
            soLoc.FindProperty("animancer").objectReferenceValue = animancer;
            soLoc.FindProperty("idleClip").objectReferenceValue = idle;
            soLoc.FindProperty("walkClip").objectReferenceValue = walk;
            soLoc.FindProperty("runClip").objectReferenceValue = run;
            soLoc.FindProperty("jumpClip").objectReferenceValue = jump;
            soLoc.ApplyModifiedPropertiesWithoutUndo();

            var camTf = EnsureEmbeddedCamera(root, pivotY, camDist);
            var springArm = root.GetComponentInChildren<ThirdPersonSpringArm>(true);

            var soOrch = new SerializedObject(orchestrator);
            soOrch.FindProperty("input").objectReferenceValue = root.GetComponent<PlayerInputReader>();
            soOrch.FindProperty("motor").objectReferenceValue = root.GetComponent<ThirdPersonMotor>();
            soOrch.FindProperty("springArm").objectReferenceValue = springArm;
            soOrch.FindProperty("locomotion").objectReferenceValue = locomotion;
            soOrch.FindProperty("characterController").objectReferenceValue = controller;
            soOrch.FindProperty("animancer").objectReferenceValue = animancer;
            soOrch.FindProperty("cameraTransform").objectReferenceValue = camTf;
            soOrch.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform EnsureEmbeddedCamera(GameObject playerRoot, float pivotY, float camDist)
        {
            var rig = playerRoot.transform.Find("CameraRig");
            if (rig == null)
            {
                var rigGo = new GameObject("CameraRig");
                rig = rigGo.transform;
                rig.SetParent(playerRoot.transform, false);
                rig.localPosition = Vector3.zero;
                rig.localRotation = Quaternion.identity;
                rig.localScale = Vector3.one;
            }

            var arm = rig.GetComponent<ThirdPersonSpringArm>();
            if (arm == null)
                arm = rig.gameObject.AddComponent<ThirdPersonSpringArm>();

            var camTf = rig.Find("PlayerCamera");
            Camera cam;
            if (camTf == null)
            {
                var camGo = new GameObject("PlayerCamera");
                camTf = camGo.transform;
                camTf.SetParent(rig, false);
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            else
            {
                cam = camTf.GetComponent<Camera>();
                if (cam == null)
                    cam = camTf.gameObject.AddComponent<Camera>();
                if (camTf.GetComponent<AudioListener>() == null)
                    camTf.gameObject.AddComponent<AudioListener>();
            }

            camTf.gameObject.tag = "MainCamera";
            camTf.localPosition = new Vector3(0f, pivotY, -camDist);
            camTf.localRotation = Quaternion.Euler(12f, 0f, 0f);

            var so = new SerializedObject(arm);
            so.FindProperty("followTarget").objectReferenceValue = playerRoot.transform;
            so.FindProperty("targetOffset").vector3Value = new Vector3(0f, pivotY, 0f);
            so.FindProperty("defaultDistance").floatValue = camDist;
            so.ApplyModifiedPropertiesWithoutUndo();

            return camTf;
        }

        static float FitCharacterController(GameObject root, CharacterController controller)
        {
            var instance = Object.Instantiate(root);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            Object.DestroyImmediate(instance);

            float height;
            float radius;
            float centerY;

            if (hasBounds && bounds.size.y > 0.01f)
            {
                height = bounds.size.y;
                radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.45f, 0.15f, height * 0.4f);
                centerY = bounds.center.y - root.transform.position.y;
            }
            else
            {
                height = 1.8f;
                radius = 0.3f;
                centerY = 0.9f;
            }

            float scaleY = Mathf.Abs(root.transform.localScale.y);
            if (scaleY < 0.0001f)
                scaleY = 1f;

            controller.height = height / scaleY;
            controller.radius = radius / scaleY;
            controller.center = new Vector3(0f, centerY / scaleY, 0f);
            controller.skinWidth = Mathf.Max(0.01f / scaleY, controller.radius * 0.08f);
            controller.stepOffset = Mathf.Min(controller.height * 0.3f, 0.4f / scaleY);
            controller.minMoveDistance = 0f;
            controller.slopeLimit = 45f;
            return height;
        }

        static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && clip.name == clipName && !clip.name.StartsWith("__preview__"))
                    return clip;
            }

            Debug.LogError($"[Player] Missing clip '{clipName}' in {fbxPath}");
            return null;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
                c = go.AddComponent<T>();
            return c;
        }
    }
}
