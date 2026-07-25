using Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configure le système de parchemins de sort en un clic :
/// - Plugins Vosk ciblés par plateforme (Windows x64, macOS universel)
/// - Prefabs SpellScroll (parchemin + mot flottant) et SpellBall (boule de sort)
/// - ScrollCaster + HandAnchor sur PlayerBase.prefab
/// - IncantationRecorder sur le runner + parchemins de test dans SampleScene
/// </summary>
public static class SpellSystemSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PlayerPrefabPath = "Assets/Common/Prefabs/PlayerBase.prefab";
    private const string ScrollPrefabPath = "Assets/Prefabs/SpellScroll.prefab";
    private const string BallPrefabPath = "Assets/Prefabs/SpellBall.prefab";
    private const string ParchmentMaterialPath = "Assets/Materials/SpellParchment.mat";
    private const string BallMaterialPath = "Assets/Materials/SpellBallOrb.mat";
    private const string IceZonePrefabPath = "Assets/Prefabs/IceZone.prefab";
    private const string IceMaterialPath = "Assets/Materials/SpellIceZone.mat";
    private const string GameStatePrefabPath = "Assets/Prefabs/GameState.prefab";

    private const string VoskModelFolder = "VoskModel/vosk-model-small-fr-0.22";

    [MenuItem("Tools/TinyAddict/Setup Spell Scrolls")]
    public static void Setup()
    {
        if (System.IO.Directory.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, VoskModelFolder)) == false)
        {
            Debug.LogWarning($"[SpellSystemSetup] Modèle Vosk introuvable : StreamingAssets/{VoskModelFolder} — la reconnaissance vocale ne marchera pas.");
        }

        ConfigureVoskPlugins();

        var ballPrefab = CreateSpellBallPrefab();
        var iceZonePrefab = CreateIceZonePrefab();
        var gameStatePrefab = CreateGameStatePrefab();
        var scrollPrefab = CreateScrollPrefab();
        SetupPlayerPrefab(ballPrefab, iceZonePrefab);
        SetupScene(scrollPrefab, gameStatePrefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[SpellSystemSetup] Terminé : prefabs créés, joueur câblé, scène configurée.");
    }

    // PLUGINS VOSK

    private static void ConfigureVoskPlugins()
    {
        // Natives Windows x64 (libvosk + runtime MinGW)
        foreach (string dll in new[] { "libvosk.dll", "libstdc++-6.dll", "libwinpthread-1.dll", "libgcc_s_seh-1.dll" })
        {
            ConfigureNativePlugin($"Assets/Plugins/Vosk/win-x64/{dll}", BuildTarget.StandaloneWindows64, "Windows");
        }

        // Native macOS (dylib universel Intel + Apple Silicon)
        ConfigureNativePlugin("Assets/Plugins/Vosk/osx/libvosk.dylib", BuildTarget.StandaloneOSX, "OSX");
    }

    private static void ConfigureNativePlugin(string assetPath, BuildTarget target, string editorOs)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[SpellSystemSetup] Plugin introuvable : {assetPath}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithPlatform(target, true);
        // Aussi utilisé dans l'éditeur, mais uniquement sur l'OS correspondant
        importer.SetCompatibleWithEditor(true);
        importer.SetEditorData("OS", editorOs);
        importer.SaveAndReimport();
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

    private static NetworkObject CreateIceZonePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(IceZonePrefabPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("IceZone");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<IceZone>();

            // Disque bleu translucide au sol (rayon 4 m, comme IceZone._radius)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Visual";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(8f, 0.02f, 8f);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateTransparentMaterial(
                IceMaterialPath, new Color(0.35f, 0.75f, 1f, 0.45f));

            PrefabUtility.SaveAsPrefabAsset(root, IceZonePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé : {IceZonePrefabPath}");
        return AssetDatabase.LoadAssetAtPath<NetworkObject>(IceZonePrefabPath);
    }

    private static NetworkObject CreateGameStatePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(GameStatePrefabPath);
        if (existing != null)
        {
            // Prefab déjà créé par une version précédente du setup : on s'assure
            // qu'il porte bien le TeamManager
            if (existing.GetComponent<TeamManager>() == null)
            {
                var contents = PrefabUtility.LoadPrefabContents(GameStatePrefabPath);
                try
                {
                    if (contents.GetComponent<TeamManager>() == null)
                        contents.AddComponent<TeamManager>();
                    PrefabUtility.SaveAsPrefabAsset(contents, GameStatePrefabPath);
                    Debug.Log("[SpellSystemSetup] TeamManager ajouté au prefab GameState.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            return AssetDatabase.LoadAssetAtPath<NetworkObject>(GameStatePrefabPath);
        }

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("GameState");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<GameState>();
            // Le TeamManager vit sur le même objet réseau spawné à l'exécution :
            // en objet de scène, son dictionnaire ne se synchronisait pas
            root.AddComponent<TeamManager>();
            PrefabUtility.SaveAsPrefabAsset(root, GameStatePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé : {GameStatePrefabPath}");
        return AssetDatabase.LoadAssetAtPath<NetworkObject>(GameStatePrefabPath);
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

    private static void SetupPlayerPrefab(NetworkObject ballPrefab, NetworkObject iceZonePrefab)
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

            if (root.GetComponent<PlayerSpellEffects>() == null)
                root.AddComponent<PlayerSpellEffects>();

            if (root.GetComponent<PlayerProfile>() == null)
                root.AddComponent<PlayerProfile>();

            var so = new SerializedObject(caster);
            so.FindProperty("_handAnchor").objectReferenceValue = handAnchor;
            so.FindProperty("_castOrigin").objectReferenceValue = cameraPivot;
            so.FindProperty("_spellBallPrefab").objectReferenceValue = ballPrefab;
            so.FindProperty("_iceZonePrefab").objectReferenceValue = iceZonePrefab;
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

    private static void SetupScene(GameObject scrollPrefab, NetworkObject gameStatePrefab)
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

            // Purge des éventuels scripts manquants laissés par l'ancien système Whisper
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(runner.gameObject);

            // Les parchemins sont spawnés à l'exécution via Runner.Spawn : plus fiable
            // que des objets réseau posés en scène (pas de baking de scène en jeu)
            var spawner = runner.GetComponent<SpellScrollSpawner>();
            if (spawner == null)
                spawner = runner.gameObject.AddComponent<SpellScrollSpawner>();

            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_scrollPrefab").objectReferenceValue = scrollPrefab.GetComponent<NetworkObject>();
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            // État de partie (lobby) : spawné par le serveur au démarrage de session
            var gameStateSpawner = runner.GetComponent<GameStateSpawner>();
            if (gameStateSpawner == null)
                gameStateSpawner = runner.gameObject.AddComponent<GameStateSpawner>();

            var gameStateSo = new SerializedObject(gameStateSpawner);
            gameStateSo.FindProperty("_gameStatePrefab").objectReferenceValue = gameStatePrefab;
            gameStateSo.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[SpellSystemSetup] Runner avec Recorder introuvable — lancez d'abord le setup du vocal spatial.");
        }

        // Le TeamManager de scène ne se synchronise pas (objet réseau de scène
        // non attaché) : il vit désormais sur le prefab GameState spawné à
        // l'exécution. On retire la copie de scène, sinon son Awake capture
        // l'Instance et fait s'autodétruire la version spawnée (et GameState avec).
        var sceneTeamManager = Object.FindFirstObjectByType<TeamManager>(FindObjectsInactive.Include);
        if (sceneTeamManager != null)
        {
            Object.DestroyImmediate(sceneTeamManager.gameObject);
            Debug.Log("[SpellSystemSetup] TeamManager de scène supprimé (déplacé sur le prefab GameState).");
        }

        // L'ancien WhisperManager de scène n'a plus lieu d'être
        var oldWhisperGo = GameObject.Find("WhisperManager");
        if (oldWhisperGo != null)
        {
            Object.DestroyImmediate(oldWhisperGo);
            Debug.Log("[SpellSystemSetup] Ancien WhisperManager supprimé de la scène.");
        }

        CreateCollectionZoneVisuals();
        CreateZoneStepPoints();
        CreateScrollSpawnPoints();

        // Supprime les parchemins posés en scène (source des ramassages impossibles) :
        // ils sont désormais spawnés à l'exécution par le SpellScrollSpawner
        var sceneScrolls = Object.FindObjectsByType<SpellScroll>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sceneScroll in sceneScrolls)
        {
            Object.DestroyImmediate(sceneScroll.gameObject);
        }
        if (sceneScrolls.Length > 0)
            Debug.Log($"[SpellSystemSetup] {sceneScrolls.Length} parchemins de scène supprimés (remplacés par le SpellScrollSpawner).");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Points de spawn des parchemins : déplaçables librement dans la scène,
    // le SpellScrollSpawner fait vivre un parchemin par point
    private static void CreateScrollSpawnPoints()
    {
        if (Object.FindFirstObjectByType<ScrollSpawnPoint>(FindObjectsInactive.Include) != null)
            return;

        var parent = new GameObject("ScrollSpawnPoints").transform;
        var positions = new[]
        {
            new Vector3(3f, 0.05f, 4f),
            new Vector3(-4f, 0.05f, 3f),
            new Vector3(6f, 0.05f, -2f),
            new Vector3(-2f, 0.05f, -5f),
            new Vector3(0f, 0.05f, 7f),
            new Vector3(8f, 0.05f, 5f),
            new Vector3(-8f, 0.05f, -3f),
            new Vector3(5f, 0.05f, -7f),
            new Vector3(-6f, 0.05f, 7f),
            new Vector3(0f, 0.05f, -2f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var point = new GameObject($"ScrollSpawnPoint ({i + 1})");
            point.transform.SetParent(parent, false);
            point.transform.position = positions[i];
            point.AddComponent<ScrollSpawnPoint>();
        }

        Debug.Log($"[SpellSystemSetup] {positions.Length} points de spawn de parchemins créés — déplacez-les librement dans la scène.");
    }

    // Étapes du parcours des zones : 5 points rouges + 5 points bleus, la zone
    // de chaque équipe saute sur le point suivant chaque minute
    private static void CreateZoneStepPoints()
    {
        if (Object.FindFirstObjectByType<ZoneStepPoint>(FindObjectsInactive.Include) != null)
            return;

        var parent = new GameObject("ZoneSteps").transform;

        var redPositions = new[]
        {
            new Vector3(-12f, 0f, 0f),
            new Vector3(-8f, 0f, 8f),
            new Vector3(-4f, 0f, -8f),
            new Vector3(-10f, 0f, -6f),
            new Vector3(-6f, 0f, 4f),
        };
        var bluePositions = new[]
        {
            new Vector3(12f, 0f, 0f),
            new Vector3(8f, 0f, -8f),
            new Vector3(4f, 0f, 8f),
            new Vector3(10f, 0f, 6f),
            new Vector3(6f, 0f, -4f),
        };

        for (int i = 0; i < redPositions.Length; i++)
        {
            CreateStepPoint(parent, Team.Red, i, redPositions[i]);
            CreateStepPoint(parent, Team.Blue, i, bluePositions[i]);
        }

        Debug.Log("[SpellSystemSetup] 10 points d'étapes de zones créés (5 rouges, 5 bleus) — déplacez-les librement.");
    }

    private static void CreateStepPoint(Transform parent, Team team, int step, Vector3 position)
    {
        var point = new GameObject($"ZoneStep{(team == Team.Red ? "Rouge" : "Bleu")} ({step + 1})");
        point.transform.SetParent(parent, false);
        point.transform.position = position;

        var stepPoint = point.AddComponent<ZoneStepPoint>();
        stepPoint.Team = team;
        stepPoint.Step = step;
    }

    // Visuels des zones de collecte (purement locaux : le comptage est fait par
    // GameState avec les mêmes centres/tailles que ses valeurs par défaut)
    private static void CreateCollectionZoneVisuals()
    {
        CreateZoneVisual("ZoneCollecteRouge", new Vector3(-12f, 0f, 0f), new Color(1f, 0.3f, 0.25f, 0.35f),
            "Assets/Materials/ZoneCollecteRouge.mat", Team.Red);
        CreateZoneVisual("ZoneCollecteBleue", new Vector3(12f, 0f, 0f), new Color(0.3f, 0.55f, 1f, 0.35f),
            "Assets/Materials/ZoneCollecteBleue.mat", Team.Blue);
    }

    private static void CreateZoneVisual(string name, Vector3 position, Color color, string materialPath, Team team)
    {
        var existingZone = GameObject.Find(name);
        if (existingZone != null)
        {
            // Zone déjà créée par une version précédente : on s'assure qu'elle
            // porte le marqueur qui pilote la zone de comptage
            var existingMarker = existingZone.GetComponent<CollectionZoneMarker>();
            if (existingMarker == null)
            {
                existingMarker = existingZone.AddComponent<CollectionZoneMarker>();
                existingMarker.Team = team;
                Debug.Log($"[SpellSystemSetup] CollectionZoneMarker ajouté sur {name}.");
            }
            return;
        }

        var root = new GameObject(name);
        root.transform.position = position;
        var marker = root.AddComponent<CollectionZoneMarker>();
        marker.Team = team;

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        disc.name = "Visual";
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        // Même emprise au sol que la zone de comptage de GameState (8 x 8 m)
        disc.transform.localScale = new Vector3(8f, 0.04f, 8f);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateTransparentMaterial(materialPath, color);

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 3f, 0f);

        var label = labelObject.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.font = font;
        label.fontSize = 64;
        label.characterSize = 0.12f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(color.r, color.g, color.b, 1f);
        label.text = name == "ZoneCollecteRouge" ? "ZONE ROUGE" : "ZONE BLEUE";
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        Debug.Log($"[SpellSystemSetup] Zone de collecte créée : {name} à {position}");
    }

    // HELPERS

    private static Material GetOrCreateTransparentMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        EnsureFolder("Assets/Materials");

        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        // Recette URP pour la transparence alpha
        material.SetFloat("_Surface", 1f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

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
