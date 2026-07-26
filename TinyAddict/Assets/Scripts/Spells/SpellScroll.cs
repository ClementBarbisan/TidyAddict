using Fusion;
using UnityEngine;

/// <summary>
/// Parchemin de sort posé au sol. Un mot aléatoire lui est assigné au spawn.
/// Quand un joueur (mains vides) marche dessus, le serveur le lui met en main ;
/// il suit alors l'ancre de main du porteur. Reposé, il retombe devant le joueur.
/// </summary>
///
namespace Projectiles
{
    public class SpellScroll : NetworkBehaviour
    {
        [SerializeField] private float _pickupRadius = 0.9f;
        [SerializeField] private TextMesh _wordText;
        [SerializeField] private Collider _pickupCollider;

        // Au sol : rouleau plié. En main : parchemin ouvert avec l'image du sort
        [SerializeField] private GameObject _rollVisual;
        [SerializeField] private GameObject _openVisual;
        [SerializeField] private Renderer _openRenderer;
        [Tooltip("Textures du parchemin ouvert, indexées par sort (0 = polaris … 11 = taurus)")]
        [SerializeField] private Texture2D[] _spellTextures;

        private MaterialPropertyBlock _textureBlock;
        private int _appliedTextureIndex = -1;

        [Networked] public int WordIndex { get; set; }
        [Networked] public ScrollCaster Holder { get; set; }
        [Networked] private TickTimer PickupCooldown { get; set; }

        public bool IsHeld => Holder != null;
        public string Word => SpellWords.Words[Mathf.Abs(WordIndex) % SpellWords.Words.Length];

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                WordIndex = Random.Range(0, SpellWords.Words.Length);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority == false)
                return;

            if (IsHeld)
            {
                // Position autoritaire sur la main du porteur (synchronisée par NetworkTransform)
                FollowHolder();
                return;
            }

            if (PickupCooldown.ExpiredOrNotRunning(Runner) == false)
                return;

            var hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, _pickupRadius, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var caster = hit.GetComponentInParent<ScrollCaster>();
                if (caster != null && caster.HeldScroll == null)
                {
                    Holder = caster;
                    caster.HeldScroll = this;
                    break;
                }
            }
        }

        // Appelé par ScrollCaster (state authority) quand le joueur repose le parchemin
        public void Drop(Vector3 position, Quaternion rotation)
        {
            Holder = null;
            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 1.5f);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void LateUpdate()
        {
            // Les propriétés [Networked] ne sont accessibles qu'une fois l'objet spawné
            if (Object == null || Object.IsValid == false)
                return;

            // Suivi visuel sans retard uniquement chez le porteur : chez les autres,
            // le pitch de son CameraPivot n'est pas simulé, on laisse NetworkTransform
            // interpoler la position calculée par le serveur.
            if (IsHeld && Holder.HasInputAuthority)
                FollowHolder();

            if (_pickupCollider != null)
                _pickupCollider.enabled = IsHeld == false;

            if (_wordText != null && _wordText.text != Word)
            {
                _wordText.text = Word;
                // Le mot prend la couleur du sort qu'il déclenche
                _wordText.color = SpellWords.ColorOf(WordIndex);
            }

            UpdateVisuals();

            // Le mot flottant fait toujours face à la caméra
            if (_wordText != null && Camera.main != null)
                _wordText.transform.rotation =
                    Quaternion.LookRotation(_wordText.transform.position - Camera.main.transform.position);
        }

        private void FollowHolder()
        {
            if (IsHeld == false || Holder.HandAnchor == null)
                return;

            transform.SetPositionAndRotation(Holder.HandAnchor.position, Holder.HandAnchor.rotation);
        }

        // Bascule rouleau plié (au sol) / parchemin ouvert avec l'image du sort (en main)
        private void UpdateVisuals()
        {
            bool held = IsHeld;

            if (_rollVisual != null && _rollVisual.activeSelf != !held)
                _rollVisual.SetActive(!held);

            if (_openVisual != null && _openVisual.activeSelf != held)
                _openVisual.SetActive(held);

            // Texture du sort appliquée une seule fois par index
            if (_openRenderer != null && _spellTextures != null && _spellTextures.Length > 0 &&
                _appliedTextureIndex != WordIndex)
            {
                _appliedTextureIndex = WordIndex;
                var texture = _spellTextures[Mathf.Abs(WordIndex) % _spellTextures.Length];
                if (texture != null)
                {
                    _textureBlock ??= new MaterialPropertyBlock();
                    _textureBlock.SetTexture("_BaseMap", texture);
                    _openRenderer.SetPropertyBlock(_textureBlock);
                }
            }
        }
    }
}