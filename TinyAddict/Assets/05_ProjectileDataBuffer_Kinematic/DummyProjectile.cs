using UnityEngine;

namespace Projectiles.ProjectileDataBuffer_Kinematic
{
    public class DummyProjectile : MonoBehaviour
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private GameObject _hitEffect;
        [SerializeField]
        private GameObject _visualsRoot;

        private bool _hitEffectVisible;

        // PUBLIC METHODS

        public void ShowHitEffect()
        {
            if (_hitEffectVisible)
                return;

            if (_hitEffect != null)
            {
                _hitEffect.SetActive(true);
            }

            if (_visualsRoot != null)
            {
                _visualsRoot.SetActive(false);
            }

            _hitEffectVisible = true;
        }

        // MONOBEHAVIOUR

        protected void Awake()
        {
            if (_hitEffect != null)
            {
                _hitEffect.SetActive(false);
            }
        }
    }
}
