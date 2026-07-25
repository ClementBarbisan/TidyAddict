# Vocal de proximité — Photon Voice 2 + Fusion 2

## Package

Installé via UPM dans `Packages/manifest.json` :
`com.photonengine.voice-fusion` → branche `fusion/v2/voice-for-fusion` du repo Photon-UPM (Voice 2.63).
Unity le télécharge automatiquement à l'ouverture du projet.

## 1. AppIds (obligatoire, une fois par personne / par app)

Dashboard Photon (https://dashboard.photonengine.com) :

1. Créer une app **Fusion** → copier l'AppId dans `AppIdFusion`.
2. Créer une app **Voice** → copier l'AppId dans `AppIdVoice`.

Dans Unity : `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`
(ou menu *Fusion → Realtime Settings*). Les deux champs sont vides actuellement.

## 2. Setup scène (GameObject du NetworkRunner)

Sur le GameObject qui porte le `NetworkRunner` :

1. Ajouter **`FusionVoiceClient`** (composant du package Voice).
   Il rejoint automatiquement une room Voice miroir de la room Fusion.
2. Ajouter **`Recorder`** :
   - `Transmit Enabled` : off (géré par `VoiceTransmitControl`)
   - `Microphone Type` : Unity
   - Assigner ce Recorder comme **Primary Recorder** du `FusionVoiceClient`.
3. Ajouter **`VoiceTransmitControl`** (script projet) :
   - Mode `PushToTalk` (V pour parler, M pour mute) ou `OpenMicVoiceDetection`.

## 3. Setup player prefab (le prefab networké spawné par Fusion)

Sur le prefab joueur (idéalement un enfant "VoiceHead" à hauteur de tête) :

1. **`VoiceNetworkObject`** (composant du package, à côté du `NetworkObject`).
2. **`Speaker`** + **`AudioSource`** sur l'enfant tête.
3. **`ProximityVoiceAudio`** (script projet) sur le même enfant :
   - `Full Volume Distance` : 2 m
   - `Max Audible Distance` : 18 m (au-delà : silence total, falloff linéaire)

Le son suit la position répliquée du joueur → l'atténuation 3D fait la proximité.

## Prérequis encore manquant

Le `FPSController` actuel est local (non networké). Pour entendre les autres,
il faut un player prefab Fusion (`NetworkObject` + KCC) spawné par le runner —
sans position répliquée, pas de proximité possible.

## Test rapide

Deux instances (build + éditeur, ou ParrelSync), même room Fusion :
parler en tenant V, s'éloigner de plus de 18 m → le son disparaît.
