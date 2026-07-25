using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configure le vocal spatial (Photon Voice 2 + Fusion) en un clic :
/// - Recorder + FusionVoiceClient sur le GameObject du NetworkRunner (SampleScene)
/// - Speaker + VoiceNetworkObject + AudioSource 3D sur le prefab PlayerBase
///   (hérité par la variante Player_NetworkObject spawnée par le GameManager)
/// </summary>
public static class SpatialVoiceSetup
{
    private const string PlayerPrefabPath = "Assets/Common/Prefabs/PlayerBase.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string VoiceChildName = "VoiceSpeaker";

    [MenuItem("Tools/TinyAddict/Setup Spatial Voice")]
    public static void Setup()
    {
        SetupPlayerPrefab();
        SetupRunnerScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[SpatialVoiceSetup] Terminé. Il reste à renseigner l'AppId Voice dans " +
                  "Assets/Photon/Fusion/Resources/PhotonAppSettings.asset (champ App Id Voice).");
    }

    private static void SetupPlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var voiceTransform = root.transform.Find(VoiceChildName);
            GameObject voice;
            if (voiceTransform == null)
            {
                voice = new GameObject(VoiceChildName);
                voice.transform.SetParent(root.transform, false);
                // Hauteur approximative de la tête pour que la voix vienne du bon endroit
                voice.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            }
            else
            {
                voice = voiceTransform.gameObject;
            }

            var audioSource = voice.GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = voice.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
            // Le doppler sur de la voix produit des artefacts de pitch désagréables
            audioSource.dopplerLevel = 0f;

            if (voice.GetComponent<Speaker>() == null)
                voice.AddComponent<Speaker>();

            if (voice.GetComponent<VoiceNetworkObject>() == null)
                voice.AddComponent<VoiceNetworkObject>();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log($"[SpatialVoiceSetup] Prefab joueur configuré : {PlayerPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupRunnerScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
                return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        var runner = Object.FindFirstObjectByType<NetworkRunner>(FindObjectsInactive.Include);
        if (runner == null)
        {
            Debug.LogError($"[SpatialVoiceSetup] Aucun NetworkRunner trouvé dans {ScenePath}");
            return;
        }

        var go = runner.gameObject;

        var recorder = go.GetComponent<Recorder>();
        if (recorder == null)
            recorder = go.AddComponent<Recorder>();

        var recorderSo = new SerializedObject(recorder);
        recorderSo.FindProperty("recordingEnabled").boolValue = true;
        recorderSo.FindProperty("transmitEnabled").boolValue = true;
        // Ne transmet que lorsque le joueur parle (économise la bande passante)
        recorderSo.FindProperty("voiceDetection").boolValue = true;
        recorderSo.ApplyModifiedPropertiesWithoutUndo();

        var voiceClient = go.GetComponent<FusionVoiceClient>();
        if (voiceClient == null)
            voiceClient = go.AddComponent<FusionVoiceClient>();

        var voiceClientSo = new SerializedObject(voiceClient);
        voiceClientSo.FindProperty("primaryRecorder").objectReferenceValue = recorder;
        // false : c'est le VoiceNetworkObject de chaque joueur qui enregistre le Recorder
        // avec son Object.Id en UserData, ce qui lie chaque voix au bon Speaker distant
        voiceClientSo.FindProperty("usePrimaryRecorder").boolValue = false;
        voiceClientSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SpatialVoiceSetup] FusionVoiceClient + Recorder ajoutés sur '{go.name}' dans {ScenePath}");
    }
}
