using Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Whisper;

/// <summary>
/// Configure le système de parchemins de sort en un clic :
/// - Prefabs SpellScroll (parchemin + mot flottant) et SpellBall (boule de sort)
/// - ScrollCaster + HandAnchor sur PlayerBase.prefab
/// - IncantationRecorder sur le runner + WhisperManager + parchemins de test dans SampleScene
/// </summary>
public static class SpellSystemSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PlayerPrefabPath = "Assets/Common/Prefabs/PlayerBase.prefab";
    private const string ScrollPrefabPath = "Assets/Prefabs/SpellScroll.prefab";
    private const string BallPrefabPath = "Assets/Prefabs/SpellBall.prefab";
    private const string ParchmentMaterialPath = "Assets/Materials/SpellParchment.mat";
    private const string BallMaterialPath = "Assets/Materials/SpellBallOrb.mat";
    private const string WhisperModelPath = "Whisper/ggml-tiny-q5_1.bin";

    private static readonly Vector3[] ScrollSpawnPositions =
    {
        new Vector3(3f, 0.05f, 4f),
        new Vector3(-4f, 0.05f, 3f),
        new Vector3(6f, 0.05f, -2f),
        new Vector3(-2f, 0.05f, -5f),
    };

    [MenuItem("Tools/TinyAddict/Setup Spell Scrolls")]
    public static void Setup()
    {
        if (System.IO.File.Exists(Application.streamingAssetsPath + "/" + WhisperModelPath) == false)
        {
            Debug.LogWarning($"[SpellSystemSetup] Modèle Whisper introuvable : StreamingAssets/{WhisperModelPath} — la reconnaissance vocale ne marchera pas.");
        }

        var ballPrefab = CreateSpellBallPrefab();
        var scrollPrefab = CreateScrollPrefab();
        SetupPlayerPrefab(ballPrefab);
        SetupScene(scrollPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[SpellSystemSetup] Terminé : prefabs créés, joueur câblé, scène configurée.");
    }

    // PREFABS

    private static NetworkObject CreateSpellBallPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(BallPrefabPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("SpellBall");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<SpellBall>();

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Visual";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(
                BallMaterialPath, new Color(1f, 0.45f, 0.1f), emissive: true);

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.15f);
            light.range = 6f;
            light.intensity = 2.5f;

            PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé : {BallPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<NetworkObject>(BallPrefabPath);
    }

    private static GameObject CreateScrollPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ScrollPrefabPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("SpellScroll");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var scroll = root.AddComponent<SpellScroll>();

            // Rouleau de parchemin : cylindre couché
            var rollMaterial = GetOrCreateMaterial(ParchmentMaterialPath, new Color(0.87f, 0.78f, 0.58f), emissive: false);
            var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roll.name = "Roll";
            roll.transform.SetParent(root.transform, false);
            roll.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            roll.transform.localScale = new Vector3(0.09f, 0.16f, 0.09f);
            Object.DestroyImmediate(roll.GetComponent<Collider>());
            roll.GetComponent<MeshRenderer>().sharedMaterial = rollMaterial;

            // Mot flottant au-dessus du parchemin
            var wordGo = new GameObject("Word");
            wordGo.transform.SetParent(root.transform, false);
            wordGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var text = wordGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = 48;
            text.characterSize = 0.045f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.9f, 0.4f);
            text.text = "sort";
            wordGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            var so = new SerializedObject(scroll);
            so.FindProperty("_wordText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScrollPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé : {ScrollPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(ScrollPrefabPath);
    }

    // JOUEUR

    private static void SetupPlayerPrefab(NetworkObject ballPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Transform cameraPivot = FindChildByName(root.transform, "CameraPivot");
            if (cameraPivot == null)
            {
                Debug.LogError("[SpellSystemSetup] CameraPivot introuvable dans PlayerBase.prefab");
                return;
            }

            var handAnchor = cameraPivot.Find("HandAnchor");
            if (handAnchor == null)
            {
                handAnchor = new GameObject("HandAnchor").transform;
                handAnchor.SetParent(cameraPivot, false);
                // Devant le joueur, légèrement à droite et en dessous du regard
                handAnchor.localPosition = new Vector3(0.3f, -0.25f, 0.6f);
            }

            var caster = root.GetComponent<ScrollCaster>();
            if (caster == null)
                caster = root.AddComponent<ScrollCaster>();

            var so = new SerializedObject(caster);
            so.FindProperty("_handAnchor").objectReferenceValue = handAnchor;
            so.FindProperty("_castOrigin").objectReferenceValue = cameraPivot;
            so.FindProperty("_spellBallPrefab").objectReferenceValue = ballPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"[SpellSystemSetup] ScrollCaster câblé sur {PlayerPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // SCÈNE

    private static void SetupScene(GameObject scrollPrefab)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
                return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        var runner = Object.FindFirstObjectByType<NetworkRunner>(FindObjectsInactive.Include);
        if (runner != null && runner.GetComponent<Recorder>() != null)
        {
            if (runner.GetComponent<IncantationRecorder>() == null)
                runner.gameObject.AddComponent<IncantationRecorder>();
        }
        else
        {
            Debug.LogWarning("[SpellSystemSetup] Runner avec Recorder introuvable — lancez d'abord le setup du vocal spatial.");
        }

        var whisper = Object.FindFirstObjectByType<WhisperManager>(FindObjectsInactive.Include);
        if (whisper == null)
        {
            whisper = new GameObject("WhisperManager").AddComponent<WhisperManager>();
        }

        whisper.language = "fr";
        whisper.initialPrompt = SpellWords.InitialPrompt;
        var whisperSo = new SerializedObject(whisper);
        whisperSo.FindProperty("modelPath").stringValue = WhisperModelPath;
        whisperSo.FindProperty("isModelPathInStreamingAssets").boolValue = true;
        whisperSo.FindProperty("initOnAwake").boolValue = true;
        whisperSo.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindFirstObjectByType<SpellScroll>(FindObjectsInactive.Include) == null)
        {
            foreach (var position in ScrollSpawnPositions)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(scrollPrefab, scene);
                instance.transform.position = position;
            }
            Debug.Log($"[SpellSystemSetup] {ScrollSpawnPositions.Length} parchemins placés dans la scène.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // HELPERS

    private static Material GetOrCreateMaterial(string path, Color color, bool emissive)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        EnsureFolder("Assets/Materials");

        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", color * 3f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path) == false)
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }
        return null;
    }
}
