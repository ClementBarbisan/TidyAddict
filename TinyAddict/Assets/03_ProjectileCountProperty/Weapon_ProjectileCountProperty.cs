using Fusion;
using UnityEngine;

namespace Projectiles.ProjectileCountProperty
{
    // This solution uses hitscan projectiles and dummy flying projectile visuals.
    // Synchronizing just the projectile count is the most efficient option and can be very much recommended.
    // In this script we save some data about the last projectile (hit position). This can be used for slower fire
    // rates and single projectile per FUN otherwise there could be some projectile visuals missing on proxies
    // (=> on proxies we have information only about the last projectile).
    // Use projectile data buffer (Example 04) if there is need for high fire rate or multiple projectiles
    // per shot (shotgun).
    public class Weapon_ProjectileCountProperty : WeaponBase
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private LayerMask _hitMask;
        [SerializeField]
        private float _hitImpulse = 50f;
        [SerializeField]
        private DummyFlyingProjectile _dummyProjectilePrefab;

        [Networked]
        private int _fireCount { get; set; }
        [Networked]
        private Vector3 _hitPosition { get; set; }

        private int _visibleFireCount;

        // WeaponBase INTERFACE

        public override void Fire()
        {
            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

            // Whole projectile path and effects are immediately processed (= hitscan projectile)
            if (Runner.LagCompensation.Raycast(FireTransform.position, FireTransform.forward, 100f,
                    Object.InputAuthority, out var hit, _hitMask, hitOptions))
            {
                if (hit.Collider != null && hit.Collider.attachedRigidbody != null)
                {
                    hit.Collider.attachedRigidbody.AddForce(FireTransform.forward * _hitImpulse, ForceMode.Impulse);
                }

                // Save hit point to correctly show bullet path on all clients
                // This however works only for single projectile per FUN and with higher fire cadence
                // some projectiles might not be fired on proxies because we save only the position
                // of the LAST projectile hit. See Example 04 where higher cadence or multiple projectiles at once
                // (e.g. shotgun fire) is not an issue.
                _hitPosition = hit.Point;
            }
            else
            {
                _hitPosition = Vector3.zero;
            }

            // In this example projectile count property (fire count) is used not only for weapon fire effects
            // but to spawn the projectile visuals themselves.
            _fireCount++;
        }

        public override void Spawned()
        {
            _visibleFireCount = _fireCount;
        }

        public override void Render()
        {
            if (_visibleFireCount < _fireCount)
            {
                PlayFireEffect();

                // Try to spawn dummy flying projectile.
                // Even though projectile hit was immediately processed in FUN we can spawn
                // dummy projectile that still travels through air with some speed until the hit position is reached.
                // That way the immediate hitscan effect is covered by the flying visuals.
                if (_dummyProjectilePrefab != null)
                {
                    var dummyProjectile = Instantiate(_dummyProjectilePrefab, FireTransform.position, FireTransform.rotation);
                    dummyProjectile.SetHitPosition(_hitPosition);

                    // When using multipeer, move to correct scene and disable renderers for other clients. Can be omitted otherwise.
                    if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
                    {
                        Runner.MoveToRunnerScene(dummyProjectile);
                        Runner.AddVisibilityNodes(dummyProjectile.gameObject);
                    }
                }
            }

            _visibleFireCount = _fireCount;
        }
    }
}
