using Fusion;
using UnityEngine;

namespace Projectiles.ProjectileDataBuffer_Hitscan
{
    // Using projectile data buffer is the most versatile solution that can scale very well with the project.
    // In this example we use hitscan projectiles and the added complexity over Example 03 is minimal.
    // Hitscan projectiles are very easy to implement and are the most efficient. You can trick the player
    // that the projectile is flying through the air by using dummy flying projectile.
    // However if kinematic projectiles are needed, the solution needs to be more complex, proceed to Example 05.
    public class Weapon_ProjectileDataBuffer_Hitscan : WeaponBase
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
        [Networked, Capacity(32)]
        private NetworkArray<ProjectileData> _projectileData { get; }

        private int _visibleFireCount;

        // WeaponBase INTERFACE

        public override void Fire()
        {
            var hitPosition = Vector3.zero;

            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

            // Whole projectile path and effects are immediately processed (= hitscan projectile)
            if (Runner.LagCompensation.Raycast(FireTransform.position, FireTransform.forward, 100f,
                    Object.InputAuthority, out var hit, _hitMask, hitOptions))
            {
                if (hit.Collider != null && hit.Collider.attachedRigidbody != null)
                {
                    hit.Collider.attachedRigidbody.AddForce(FireTransform.forward * _hitImpulse, ForceMode.Impulse);
                }

                hitPosition = hit.Point;
            }

            // As opposed to Example 03, with projectile data buffer it would be possible to fire
            // multiple projectiles at once (e.g. shotgun)
            _projectileData.Set(_fireCount % _projectileData.Length, new ProjectileData()
            {
                HitPosition = hitPosition,
            });

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
            }

            if (_dummyProjectilePrefab != null)
            {
                // As opposed to Example 03, all missing projectiles are instantiated here
                for (int i = _visibleFireCount; i < _fireCount; i++)
                {
                    var data = _projectileData[i % _projectileData.Length];

                    var dummyProjectile = Instantiate(_dummyProjectilePrefab, FireTransform.position, FireTransform.rotation);
                    dummyProjectile.SetHitPosition(data.HitPosition);

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

        // DATA STRUCTURES

        private struct ProjectileData : INetworkStruct
        {
            public Vector3 HitPosition;

            // ProjectileData struct can be expanded with additional data
            // like ImpactNormal, ImpactType to better reconstruct projectile effects on all clients
            // See ProjectileManager in the Projectiles Advanced.
            // It is however best practice to keep the ProjectileData struct as small as possible.
        }
    }
}
