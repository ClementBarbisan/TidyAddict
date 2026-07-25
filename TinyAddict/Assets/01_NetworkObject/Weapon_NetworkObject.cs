using Fusion;
using UnityEngine;

namespace Projectiles.NetworkObjectExample
{
    public class Weapon_NetworkObject : WeaponBase
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private PhysicsProjectile _projectilePrefab;
        [SerializeField, Tooltip("NetworkObjectBuffer will pre-spawn projectiles in advance to mitigate spawn delay on input authority")]
        private bool _useBuffer;
        [SerializeField]
        private NetworkObjectBuffer _projectileBuffer;

        [Networked]
        private int _fireCount { get; set; }

        private int _visibleFireCount;

        // WeaponBase INTERFACE

        public override void Fire()
        {
            // Spawn the projectile
            if (_useBuffer)
            {
                FireWithBuffer();
            }
            else
            {
                FireSimple();
            }

            // Increase networked property fire count to know on all
            // clients that fire effects should be played
            _fireCount++;
        }

        public override void Spawned()
        {
            // In case of late join (and other scenarios) this object can be spawned
            // with fire count larger than zero. To prevent unwanted fire effects triggered in Render method
            // we consider all fire that happened before the Spawn as already visible.
            _visibleFireCount = _fireCount;
        }

        public override void Render()
        {
            if (_visibleFireCount < _fireCount)
            {
                PlayFireEffect();
            }

            _visibleFireCount = _fireCount;
        }

        // PRIVATE METHODS

        private void FireSimple()
        {
            // Spawn can be called only on state authority
            if (HasStateAuthority == false)
                return;

            var projectile = Runner.Spawn(_projectilePrefab, FireTransform.position, FireTransform.rotation, Object.InputAuthority);
            projectile.Fire(FireTransform.position, FireTransform.rotation);
        }

        private void FireWithBuffer()
        {
            // In Fusion 2 there is no longer a predicted spawn. We can go around this to have a buffer of pre-spawned
            // objects that are already living inside simulation but inactive. Check NetworkObjectBuffer component for more info.
            var projectile = _projectileBuffer.Get<PhysicsProjectile>(FireTransform.position, FireTransform.rotation, Object.InputAuthority);
            if (projectile != null)
            {
                projectile.Fire(FireTransform.position, FireTransform.rotation);
            }
        }
    }
}
