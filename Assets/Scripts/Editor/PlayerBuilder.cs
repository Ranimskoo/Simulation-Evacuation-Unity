using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using IgnisEscape.Adaptive;
using IgnisEscape.Feedback;
using IgnisEscape.Network;
using IgnisEscape.Player;
using IgnisEscape.Telemetry;

namespace IgnisEscape.Editor
{
    /// <summary>
    /// Merakit karakter pemain sederhana dari primitif (< 1.000 segitiga, tanpa rigging
    /// skeletal) beserta kamera 1,6 m, ShadowCastingMode.ShadowsOnly di kepala,
    /// WristAnchor_Left di lengan kiri, dan komponen fisik/telemetri (Bagian 5A).
    /// </summary>
    public static class PlayerBuilder
    {
        public struct PlayerBuildResult
        {
            public GameObject playerRoot;
            public Transform cameraTransform;
            public Transform wristAnchorLeft;
            public PlayerController playerController;
            public PlayerHealth playerHealth;
            public DataCollector dataCollector;
            public PseudoHapticController pseudoHaptic;
            public AdaptiveInterventionController adaptive;
            public SafetyWaypointTeleport teleport;
            public ScreenStressEffects screenStress;
        }

        [MenuItem("IgnisEscape/Build Player")]
        public static void BuildStandaloneMenu()
        {
            GameObject existing = GameObject.Find("Player");
            if (existing != null) Object.DestroyImmediate(existing);
            BuildPlayer(null, new Vector3(0f, 0.05f, 1.2f), ResolveShader());
        }

        public static PlayerBuildResult BuildPlayer(Transform parent, Vector3 position, Shader shader)
        {
            int playerLayer = EnsureLayerExists("Player");
            EnsureTagExists("Player");

            GameObject root = new GameObject("Player")
            {
                tag = "Player",
                layer = playerLayer
            };
            if (parent != null) root.transform.SetParent(parent);
            root.transform.position = position;

            // CharacterController: tinggi 1,7 m, radius 0,3 m, center di kaki (0, 0.85, 0)
            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;

            // Kamera sudut pandang orang pertama di ketinggian mata 1,6 m.
            // CameraPivot dirotasi oleh PlayerController & dibaca TelemetryExtractor;
            // Main Camera di bawah CameraPivot agar efek getaran ScreenStressEffects
            // tidak mengotori bacaan omega kamera.
            GameObject camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(root.transform, false);
            camPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camPivot.transform.localRotation = Quaternion.identity;

            Camera existingCam = Camera.main;
            GameObject camObj = existingCam != null ? existingCam.gameObject : new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(camPivot.transform, false);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;
            if (camObj.GetComponent<Camera>() == null) camObj.AddComponent<Camera>();
            if (camObj.GetComponent<AudioListener>() == null) camObj.AddComponent<AudioListener>();

            // Material polos tanpa tekstur (kaos oranye, celana gelap, kulit)
            Material matShirt = CreatePlainMat("Mat_PlayerShirt", new Color(0.92f, 0.42f, 0.12f), shader);
            Material matPants = CreatePlainMat("Mat_PlayerPants", new Color(0.16f, 0.18f, 0.22f), shader);
            Material matSkin = CreatePlainMat("Mat_PlayerSkin", new Color(0.88f, 0.72f, 0.60f), shader);

            Mesh lowPolyCapsule = CreateLowPolyCapsuleMesh(8, 1);
            Mesh lowPolySphere = CreateLowPolySphereMesh(8, 5);

            // Visual Root
            GameObject visualRoot = new GameObject("VisualBody");
            visualRoot.transform.SetParent(root.transform, false);
            var visualAnim = visualRoot.AddComponent<PlayerVisualAnimator>();

            // Torso (capsule)
            CreateMeshPart("Torso", lowPolyCapsule, visualRoot.transform, new Vector3(0f, 1.08f, 0f), new Vector3(0.34f, 0.32f, 0.22f), matShirt, playerLayer);

            // Kepala (sphere) -> ShadowsOnly agar tidak menutupi pandangan kamera 1,6 m
            var headRenderer = CreateMeshPart("Head", lowPolySphere, visualRoot.transform, new Vector3(0f, 1.56f, 0f), new Vector3(0.22f, 0.24f, 0.22f), matSkin, playerLayer);
            headRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            // Lengan Kiri + Tangan Kiri + Anchor "WristAnchor_Left" (body-locked)
            GameObject leftArmPivot = new GameObject("LeftArmPivot");
            leftArmPivot.transform.SetParent(visualRoot.transform, false);
            leftArmPivot.transform.localPosition = new Vector3(-0.23f, 1.33f, 0f);
            CreateMeshPart("LeftArm", lowPolyCapsule, leftArmPivot.transform, new Vector3(0f, -0.24f, 0.12f), new Vector3(0.10f, 0.26f, 0.10f), matShirt, playerLayer)
                .transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            CreateMeshPart("LeftHand", lowPolySphere, leftArmPivot.transform, new Vector3(0f, -0.48f, 0.24f), new Vector3(0.08f, 0.08f, 0.08f), matSkin, playerLayer);

            GameObject wristAnchor = new GameObject("WristAnchor_Left");
            wristAnchor.transform.SetParent(leftArmPivot.transform, false);
            wristAnchor.transform.localPosition = new Vector3(0.04f, -0.36f, 0.28f);
            wristAnchor.transform.localRotation = Quaternion.Euler(35f, 20f, 0f);

            // Lengan Kanan + Tangan Kanan
            GameObject rightArmPivot = new GameObject("RightArmPivot");
            rightArmPivot.transform.SetParent(visualRoot.transform, false);
            rightArmPivot.transform.localPosition = new Vector3(0.23f, 1.33f, 0f);
            CreateMeshPart("RightArm", lowPolyCapsule, rightArmPivot.transform, new Vector3(0f, -0.24f, 0f), new Vector3(0.10f, 0.26f, 0.10f), matShirt, playerLayer);
            CreateMeshPart("RightHand", lowPolySphere, rightArmPivot.transform, new Vector3(0f, -0.50f, 0f), new Vector3(0.08f, 0.08f, 0.08f), matSkin, playerLayer);

            // Kaki Kiri & Kanan
            GameObject leftLegPivot = new GameObject("LeftLegPivot");
            leftLegPivot.transform.SetParent(visualRoot.transform, false);
            leftLegPivot.transform.localPosition = new Vector3(-0.10f, 0.78f, 0f);
            CreateMeshPart("LeftLeg", lowPolyCapsule, leftLegPivot.transform, new Vector3(0f, -0.39f, 0f), new Vector3(0.13f, 0.39f, 0.13f), matPants, playerLayer);

            GameObject rightLegPivot = new GameObject("RightLegPivot");
            rightLegPivot.transform.SetParent(visualRoot.transform, false);
            rightLegPivot.transform.localPosition = new Vector3(0.10f, 0.78f, 0f);
            CreateMeshPart("RightLeg", lowPolyCapsule, rightLegPivot.transform, new Vector3(0f, -0.39f, 0f), new Vector3(0.13f, 0.39f, 0.13f), matPants, playerLayer);

            // Komponen Player
            var dampener = root.AddComponent<CameraDampener>();
            var health = root.AddComponent<PlayerHealth>();
            var controller = root.AddComponent<PlayerController>();
            var telemetry = root.AddComponent<TelemetryExtractor>();
            var collector = root.AddComponent<DataCollector>();
            var udp = root.AddComponent<UdpTelemetryClient>();
            var pseudo = root.AddComponent<PseudoHapticController>();
            var adaptive = root.AddComponent<AdaptiveInterventionController>();
            var teleport = root.AddComponent<SafetyWaypointTeleport>();
            var screenStress = root.AddComponent<ScreenStressEffects>();

            AssignField(visualAnim, "playerController", controller);
            AssignField(visualAnim, "leftArmPivot", leftArmPivot.transform);
            AssignField(visualAnim, "rightArmPivot", rightArmPivot.transform);
            AssignField(visualAnim, "leftLegPivot", leftLegPivot.transform);
            AssignField(visualAnim, "rightLegPivot", rightLegPivot.transform);

            AssignField(controller, "cameraPivot", camPivot.transform);
            AssignField(controller, "cameraDampener", dampener);
            AssignField(controller, "telemetryExtractor", telemetry);
            AssignField(controller, "playerHealth", health);

            AssignField(telemetry, "playerController", controller);
            AssignField(telemetry, "cameraTransform", camPivot.transform);

            AssignField(pseudo, "playerHealth", health);
            AssignField(pseudo, "playerController", controller);
            AssignField(pseudo, "cameraDampener", dampener);

            AssignField(teleport, "rayOrigin", camObj.transform);
            AssignField(teleport, "playerController", controller);

            AssignField(screenStress, "adaptiveController", adaptive);
            AssignField(screenStress, "shakeTarget", camObj.transform);

            return new PlayerBuildResult
            {
                playerRoot = root,
                cameraTransform = camObj.transform,
                wristAnchorLeft = wristAnchor.transform,
                playerController = controller,
                playerHealth = health,
                dataCollector = collector,
                pseudoHaptic = pseudo,
                adaptive = adaptive,
                teleport = teleport,
                screenStress = screenStress
            };
        }

        private static MeshRenderer CreateMeshPart(
            string name, Mesh mesh, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, int layer)
        {
            GameObject go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }

        private static Mesh CreateLowPolyCapsuleMesh(int radialSegments, int heightRings)
        {
            // Kapsul sederhana (~96 segitiga) agar total karakter < 1.000 segitiga
            return CreateLowPolySphereMesh(radialSegments, 4 + heightRings, stretchY: 1f);
        }

        private static Mesh CreateLowPolySphereMesh(int segs, int rings, float stretchY = 1f)
        {
            Mesh mesh = new Mesh { name = "LowPolyPrimitive" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int y = 0; y <= rings; y++)
            {
                float v = (float)y / rings;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= segs; x++)
                {
                    float u = (float)x / segs;
                    float theta = u * Mathf.PI * 2f;
                    verts.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta) * 0.5f,
                        Mathf.Cos(phi) * 0.5f * stretchY,
                        Mathf.Sin(phi) * Mathf.Sin(theta) * 0.5f
                    ));
                }
            }

            int stride = segs + 1;
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < segs; x++)
                {
                    int i0 = y * stride + x;
                    int i1 = i0 + stride;
                    tris.Add(i0); tris.Add(i1); tris.Add(i0 + 1);
                    tris.Add(i0 + 1); tris.Add(i1); tris.Add(i1 + 1);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material CreatePlainMat(string name, Color color, Shader shader)
        {
            const string matFolder = "Assets/IgnisEscape/Materials";
            if (!AssetDatabase.IsValidFolder("Assets/IgnisEscape"))
                AssetDatabase.CreateFolder("Assets", "IgnisEscape");
            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder("Assets/IgnisEscape", "Materials");

            string path = $"{matFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name, color = color };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            m.color = color;
            m.enableInstancing = true;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Shader ResolveShader()
        {
            bool usingSrp = GraphicsSettings.currentRenderPipeline != null || QualitySettings.renderPipeline != null;
            if (usingSrp)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }
            return Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        private static int EnsureLayerExists(string layerName)
        {
            int idx = LayerMask.NameToLayer(layerName);
            if (idx != -1) return idx;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            return 0;
        }

        private static void EnsureTagExists(string tag)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
            }
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignField(object target, string fieldName, object value)
        {
            var f = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
            );
            if (f != null) f.SetValue(target, value);
        }
    }
}

