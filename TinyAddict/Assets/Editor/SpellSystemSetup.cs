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
    private const string WallPrefabPath = "Assets/Prefabs/SpellWall.prefab";
    private const string WallMaterialPath = "Assets/Materials/SpellWallStone.mat";
    private const string BlackHolePrefabPath = "Assets/Prefabs/BlackHole.prefab";
    private const string BlackHoleMaterialPath = "Assets/Materials/SpellBlackHole.mat";

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
        var wallPrefab = CreateSpellWallPrefab();
        var blackHolePrefab = CreateBlackHolePrefab();
        var gameStatePrefab = CreateGameStatePrefab();
        var scrollPrefab = CreateScrollPrefab();
        SetupPlayerPrefab(ballPrefab, iceZonePrefab, wallPrefab, blackHolePrefab);
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
        {
            // Migration : zone de gel agrandie (rayon 4 → 7)
            var iceZone = existing.GetComponent<IceZone>();
            if (iceZone != null)
            {
                var iceSo = new SerializedObject(iceZone);
                var radiusProperty = iceSo.FindProperty("_radius");
                if (radiusProperty != null && radiusProperty.floatValue < 7f)
                {
                    radiusProperty.floatValue = 7f;
                    iceSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(existing);
                    Debug.Log("[SpellSystemSetup] IceZone : rayon agrandi à 7 m.");
                }
            }
            return existing;
        }

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

    private const string WallModelPath = "Assets/Environnement/Mur_Axel.fbx";
    private const float WallHeight = 3f;

    private static NetworkObject CreateBlackHolePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(BlackHolePrefabPath);
        if (existing != null)
            return existing;

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("BlackHole");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<BlackHoleZone>();

            // Sphère sombre émissive qui tourne sur elle-même
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Visual";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * 1.8f;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(
                BlackHoleMaterialPath, new Color(0.42f, 0.35f, 0.88f), emissive: true);

            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = new Color(0.42f, 0.35f, 0.88f);
            light.range = 10f;
            light.intensity = 3f;

            PrefabUtility.SaveAsPrefabAsset(root, BlackHolePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé : {BlackHolePrefabPath}");
        return AssetDatabase.LoadAssetAtPath<NetworkObject>(BlackHolePrefabPath);
    }

    private static NetworkObject CreateSpellWallPrefab()
    {
        // Déjà construit avec le modèle Mur_Axel : rien à faire.
        // (Un ancien prefab au cube est reconstruit — même chemin, même GUID.)
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(WallPrefabPath);
        if (existing != null && existing.transform.Find("Mur_Axel") != null)
            return existing;

        EnsureFolder("Assets/Prefabs");

        var root = new GameObject("SpellWall");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<SpellWall>();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WallModelPath);
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Mur_Axel";
                visual.transform.SetParent(root.transform, false);

                // Normalisation : hauteur du mur = WallHeight, base posée au sol,
                // centré sur le root, face large perpendiculaire à l'axe Z (regard)
                var bounds = ComputeRendererBounds(visual);
                if (bounds.size.y > 0.001f)
                {
                    float scale = WallHeight / bounds.size.y;
                    visual.transform.localScale = Vector3.one * scale;

                    bounds = ComputeRendererBounds(visual);
                    visual.transform.localPosition = new Vector3(
                        -bounds.center.x,
                        -bounds.min.y,
                        -bounds.center.z);
                }

                // Le FBX n'a pas de collider : une box ajustée aux dimensions bloque
                bounds = ComputeRendererBounds(visual);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                collider.size = bounds.size;
            }
            else
            {
                Debug.LogWarning($"[SpellSystemSetup] Modèle introuvable : {WallModelPath} — cube de secours utilisé.");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Visual";
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = new Vector3(0f, WallHeight * 0.5f, 0f);
                wall.transform.localScale = new Vector3(6f, WallHeight, 0.6f);
                wall.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateStoneMaterial();
            }

            PrefabUtility.SaveAsPrefabAsset(root, WallPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        Debug.Log($"[SpellSystemSetup] Prefab créé/reconstruit avec Mur_Axel : {WallPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<NetworkObject>(WallPrefabPath);
    }

    private static Bounds ComputeRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // En espace local du root (le root est à l'origine pendant la construction)
        bounds.center -= root.transform.parent != null ? root.transform.parent.position : Vector3.zero;
        return bounds;
    }

    // Matériau pierre construit sur les textures TileStones déjà présentes dans le projet
    private static Material GetOrCreateStoneMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        if (material != null)
            return material;

        EnsureFolder("Assets/Materials");

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environnement/Enviro.fbm/TCom_TileStones_2K_albedo.tif");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environnement/Enviro.fbm/TCom_TileStones_2K_normal.tif");

        if (albedo != null)
        {
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", new Vector2(2f, 1f));
        }
        else
        {
            material.color = new Color(0.55f, 0.55f, 0.58f);
        }

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        AssetDatabase.CreateAsset(material, WallMaterialPath);
        return material;
    }

    private static NetworkObject CreateGameStatePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(GameStatePrefabPath);
        if (existing != null)
        {
            // Prefab déjà créé par une version précédente du setup : on s'assure
            // qu'il porte bien le TeamManager
            var contents = PrefabUtility.LoadPrefabContents(GameStatePrefabPath);
            try
            {
                bool dirty = false;

                if (contents.GetComponent<TeamManager>() == null)
                {
                    contents.AddComponent<TeamManager>();
                    dirty = true;
                    Debug.Log("[SpellSystemSetup] TeamManager ajouté au prefab GameState.");
                }

                // Migration : 3 étapes de zones (départ + 2 changements)
                var gameStateSo = new SerializedObject(contents.GetComponent<GameState>());
                var stepsProperty = gameStateSo.FindProperty("_zoneSteps");
                if (stepsProperty != null && stepsProperty.intValue == 5)
                {
                    stepsProperty.intValue = 3;
                    gameStateSo.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    Debug.Log("[SpellSystemSetup] GameState : zones passées à 3 étapes.");
                }

                if (dirty)
                    PrefabUtility.SaveAsPrefabAsset(contents, GameStatePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
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

    private static void SetupPlayerPrefab(NetworkObject ballPrefab, NetworkObject iceZonePrefab, NetworkObject wallPrefab, NetworkObject blackHolePrefab)
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
            so.FindProperty("_wallPrefab").objectReferenceValue = wallPrefab;
            so.FindProperty("_blackHolePrefab").objectReferenceValue = blackHolePrefab;
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

        // L'UI de debug Fusion fait doublon avec notre lobby : on la retire, et le
        // démarrage réseau passe en Manual — c'est le LobbyMenu qui le déclenche
        // avec la salle choisie par le joueur
        var debugGui = Object.FindFirstObjectByType<FusionBootstrapDebugGUI>(FindObjectsInactive.Include);
        if (debugGui != null)
        {
            Object.DestroyImmediate(debugGui);
            Debug.Log("[SpellSystemSetup] FusionBootstrapDebugGUI supprimé (remplacé par le LobbyMenu).");
        }

        var bootstrap = Object.FindFirstObjectByType<FusionBootstrap>(FindObjectsInactive.Include);
        if (bootstrap != null && bootstrap.StartMode != FusionBootstrap.StartModes.Manual)
        {
            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("StartMode").enumValueIndex = (int)FusionBootstrap.StartModes.Manual;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SpellSystemSetup] FusionBootstrap passé en StartMode Manual (démarrage via le lobby).");
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
        CreateTeamSpawnPoints();

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

    private const int TargetScrollSpawnPoints = 12;

    // Points de spawn des parchemins : déplaçables librement dans la scène,
    // le SpellScrollSpawner fait vivre un parchemin par point. Le setup
    // complète jusqu'à TargetScrollSpawnPoints sans toucher aux points existants.
    private static void CreateScrollSpawnPoints()
    {
        var existingPoints = Object.FindObjectsByType<ScrollSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existingPoints.Length >= TargetScrollSpawnPoints)
            return;

        var parentObject = GameObject.Find("ScrollSpawnPoints");
        var parent = parentObject != null ? parentObject.transform : new GameObject("ScrollSpawnPoints").transform;

        var candidates = new[]
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
            new Vector3(10f, 0.05f, 0f),
            new Vector3(-10f, 0.05f, 2f),
            new Vector3(2f, 0.05f, 10f),
            new Vector3(-3f, 0.05f, -9f),
            new Vector3(9f, 0.05f, -5f),
            new Vector3(-9f, 0.05f, 6f),
        };

        int total = existingPoints.Length;
        int created = 0;

        foreach (var candidate in candidates)
        {
            if (total >= TargetScrollSpawnPoints)
                break;

            // Évite de doubler un point déjà placé au même endroit
            bool tooClose = false;
            foreach (var existingPoint in existingPoints)
            {
                if (existingPoint != null && Vector3.Distance(existingPoint.transform.position, candidate) < 2f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            total++;
            created++;
            var point = new GameObject($"ScrollSpawnPoint ({total})");
            point.transform.SetParent(parent, false);
            point.transform.position = candidate;
            point.AddComponent<ScrollSpawnPoint>();
        }

        if (created > 0)
            Debug.Log($"[SpellSystemSetup] {created} points de spawn de parchemins ajoutés ({total} au total) — déplacez-les librement.");
    }

    // Points de spawn d'équipe : au lancement, chaque gobelin est téléporté sur
    // un point de son camp, les deux équipes face à face
    private static void CreateTeamSpawnPoints()
    {
        if (Object.FindFirstObjectByType<TeamSpawnPoint>(FindObjectsInactive.Include) != null)
            return;

        var parent = new GameObject("TeamSpawnPoints").transform;

        CreateTeamSpawnPoint(parent, Team.Red, 1, new Vector3(-6f, 0f, -1.5f));
        CreateTeamSpawnPoint(parent, Team.Red, 2, new Vector3(-6f, 0f, 1.5f));
        CreateTeamSpawnPoint(parent, Team.Blue, 1, new Vector3(6f, 0f, -1.5f));
        CreateTeamSpawnPoint(parent, Team.Blue, 2, new Vector3(6f, 0f, 1.5f));

        Debug.Log("[SpellSystemSetup] 4 points de spawn d'équipe créés (face à face) — déplacez-les librement.");
    }

    private static void CreateTeamSpawnPoint(Transform parent, Team team, int index, Vector3 position)
    {
        var point = new GameObject($"Spawn{(team == Team.Red ? "Rouge" : "Bleu")} ({index})");
        point.transform.SetParent(parent, false);
        point.transform.position = position;
        point.AddComponent<TeamSpawnPoint>().Team = team;
    }

    // Étapes du parcours des zones : 5 points rouges + 5 points bleus, la zone
    // de chaque équipe saute sur le point suivant chaque minute
    private static void CreateZoneStepPoints()
    {
        if (Object.FindFirstObjectByType<ZoneStepPoint>(FindObjectsInactive.Include) != null)
            return;

        var parent = new GameObject("ZoneSteps").transform;

        // 3 étapes par équipe : position de départ + 2 changements
        var redPositions = new[]
        {
            new Vector3(-12f, 0f, 0f),
            new Vector3(-8f, 0f, 8f),
            new Vector3(-4f, 0f, -8f),
        };
        var bluePositions = new[]
        {
            new Vector3(12f, 0f, 0f),
            new Vector3(8f, 0f, -8f),
            new Vector3(4f, 0f, 8f),
        };

        for (int i = 0; i < redPositions.Length; i++)
        {
            CreateStepPoint(parent, Team.Red, i, redPositions[i]);
            CreateStepPoint(parent, Team.Blue, i, bluePositions[i]);
        }

        Debug.Log("[SpellSystemSetup] 6 points d'étapes de zones créés (3 rouges, 3 bleus) — déplacez-les librement.");
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
        var material = GetOrCreateZoneXRayMaterial(materialPath, color);

        var existingZone = GameObject.Find(name);
        if (existingZone != null)
        {
            // Zone déjà créée : on s'assure qu'elle porte le marqueur et que le
            // matériau translucide (recette corrigée) lui est bien câblé — le
            // marqueur régénère/redimensionne lui-même la box mesh
            var existingMarker = existingZone.GetComponent<CollectionZoneMarker>();
            if (existingMarker == null)
            {
                existingMarker = existingZone.AddComponent<CollectionZoneMarker>();
                existingMarker.Team = team;
            }

            var markerSo = new SerializedObject(existingMarker);
            markerSo.FindProperty("_visualMaterial").objectReferenceValue = material;
            markerSo.ApplyModifiedPropertiesWithoutUndo();

            var visual = existingZone.transform.Find("Visual");
            if (visual != null)
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return;
        }

        var root = new GameObject(name);
        root.transform.position = position;
        var marker = root.AddComponent<CollectionZoneMarker>();
        marker.Team = team;

        var newMarkerSo = new SerializedObject(marker);
        newMarkerSo.FindProperty("_visualMaterial").objectReferenceValue = material;
        newMarkerSo.ApplyModifiedPropertiesWithoutUndo();

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 4.6f, 0f);

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

    // Matériau des zones de collecte : shader X-Ray (visible à travers les murs
    // en silhouette atténuée, faces intérieures rendues). Recette réappliquée
    // aux matériaux existants à chaque setup.
    private static Material GetOrCreateZoneXRayMaterial(string path, Color color)
    {
        var shader = Shader.Find("TidyAddict/ZoneXRay");
        if (shader == null)
        {
            Debug.LogWarning("[SpellSystemSetup] Shader TidyAddict/ZoneXRay introuvable — matériau transparent standard utilisé.");
            return GetOrCreateTransparentMaterial(path, color);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;

        if (isNew)
        {
            EnsureFolder("Assets/Materials");
            material = new Material(shader);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetColor("_Color", new Color(color.r, color.g, color.b, 0.3f));
        material.SetFloat("_OccludedAlpha", 0.12f);

        if (isNew)
            AssetDatabase.CreateAsset(material, path);
        else
            EditorUtility.SetDirty(material);

        return material;
    }

    private static Material GetOrCreateTransparentMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = material == null;

        if (isNew)
        {
            EnsureFolder("Assets/Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        // Recette URP complète pour la transparence alpha — réappliquée même sur
        // un matériau existant (une recette incomplète rend le mesh invisible)
        material.color = color;
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetShaderPassEnabled("DepthOnly", false);
        material.SetShaderPassEnabled("SHADOWCASTER", false);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (isNew)
            AssetDatabase.CreateAsset(material, path);
        else
            EditorUtility.SetDirty(material);

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
