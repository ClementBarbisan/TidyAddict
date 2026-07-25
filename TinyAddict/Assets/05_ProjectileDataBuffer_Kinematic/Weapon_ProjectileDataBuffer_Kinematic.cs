using Fusion;
using UnityEngine;

namespace Projectiles.ProjectileDataBuffer_Kinematic
{
    // Projectile data buffer can also be used for kinematic projectiles. Projectiles are simply updated
    // based on the initial data up until they hit something or until their life time expires.
    // Although the solution from this example is fully functional it does not cover some advanced topics like
    // projectiles interpolation between actual path from the camera and visible path from the barrel of the gun;
    // or what happens to the projectiles when this weapon gets destroyed.
    // For more advanced features and examples of multiple other types of projectiles check Projectiles Advanced.
    public class Weapon_ProjectileDataBuffer_Kinematic : WeaponBase
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private float _speed = 50f;
        [SerializeField]
        private LayerMask _hitMask;
        [SerializeField]
        private float _hitImpulse = 50f;
        [SerializeField]
        private float _lifeTime = 4f;
        [SerializeField]
        private float _lifeTimeAfterHit = 2f;
        [SerializeField]
        private DummyProjectile _dummyProjectilePrefab;

        [Networked]
        private int _fireCount { get; set; }
        [Networked, Capacity(64)]
        private NetworkArray<ProjectileData> _projectileData { get; }

        private DummyProjectile[] _projectiles = new DummyProjectile[64];

        private int _visibleFireCount;

        // WeaponBase INTERFACE

        public override void Fire()
        {
            _projectileData.Set(_fireCount % _projectileData.Length, new ProjectileData()
            {
                FireTick = Runner.Tick,
                FirePosition = FireTransform.position,
                FireVelocity = FireTransform.forward * _speed,
                FinishTick = Runner.Tick + Mathf.RoundToInt(_lifeTime / Runner.DeltaTime),
            });

            _fireCount++;
        }

        public override void Spawned()
        {
            _visibleFireCount = _fireCount;
        }

        public override void FixedUpdateNetwork()
        {
            int tick = Runner.Tick;

            // Process projectile update
            for (int i = 0; i < _projectileData.Length; i++)
            {
                var data = _projectileData[i];

                if (data.IsActive == false)
                    continue;
                if (data.FinishTick <= tick)
                    continue;

                // For simplicity projectile update is processed directly in the Weapon.
                // It might be more suitable to move this logic to projectile object itself in order
                // to have different projectile behaviours without the need to alter the weapon.
                // See Projectiles Advanced where such approach is used.
                UpdateProjectile(ref data, tick);

                _projectileData.Set(i, data);
            }
        }

        public override void Render()
        {
            if (_visibleFireCount < _fireCount)
            {
                PlayFireEffect();
            }

            // Instantiate missing projectile objects
            for (int i = _visibleFireCount; i < _fireCount; i++)
            {
                int index = i % _projectileData.Length;
                var data = _projectileData[index];

                var previousProjectile = _projectiles[index];
                if (previousProjectile != null)
                {
                    Destroy(previousProjectile.gameObject);
                }

                var projectile = Instantiate(_dummyProjectilePrefab, data.FirePosition, Quaternion.LookRotation(data.FireVelocity));

                // When using multipeer, move to correct scene and disable renderers for other clients. Can be omitted otherwise.
                if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
                {
                    Runner.MoveToRunnerScene(projectile);
                    Runner.AddVisibilityNodes(projectile.gameObject);
                }

                _projectiles[index] = projectile;
            }

            // Object.RenderTime returns remote time for proxies, and local time for input/state authority.
            float floatTick = Object.RenderTime / Runner.DeltaTime;

            // Update projectile visuals
            for (int i = 0; i < _projectiles.Length; i++)
            {
                var projectile = _projectileData[i];
                var projectileObject = _projectiles[i];

                if (projectile.IsActive == false || projectile.FinishTick < floatTick)
                {
                    if (projectileObject != null)
                    {
                        Destroy(projectileObject.gameObject);
                    }

                    continue;
                }

                if (projectile.HitPosition != Vector3.zero)
                {
                    projectileObject.transform.position = projectile.HitPosition;
                    projectileObject.ShowHitEffect();
                }
                else
                {
                    projectileObject.transform.position = GetMovePosition(ref projectile, floatTick);
                }
            }

            _visibleFireCount = _fireCount;
        }

        // PRIVATE METHODS

        private void UpdateProjectile(ref ProjectileData projectileData, int tick)
        {
            if (projectileData.HitPosition != Vector3.zero)
                return;

            var previousPosition = GetMovePosition(ref projectileData, tick - 1f);
            var nextPosition = GetMovePosition(ref projectileData, tick);

            var direction = nextPosition - previousPosition;

            float distance = direction.magnitude;
            direction /= distance; // Normalize

            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

            if (Runner.LagCompensation.Raycast(previousPosition, direction, distance,
                    Object.InputAuthority, out var hit, _hitMask, hitOptions))
            {
                projectileData.HitPosition = hit.Point;
                projectileData.FinishTick = tick + Mathf.RoundToInt(_lifeTimeAfterHit / Runner.DeltaTime);

                if (hit.Collider != null && hit.Collider.attachedRigidbody != null)
                {
                    hit.Collider.attachedRigidbody.AddForce(direction * _hitImpulse, ForceMode.Impulse);
                }
            }
        }

        private Vector3 GetMovePosition(ref ProjectileData data, float currentTick)
        {
            float time = (currentTick - data.FireTick) * Runner.DeltaTime;

            if (time <= 0f)
                return data.FirePosition;

            return data.FirePosition + data.FireVelocity * time;
        }

        // DATA STRUCTURES

        private struct ProjectileData : INetworkStruct
        {
            public bool IsActive => FireTick > 0;

            public int FireTick;
            public int FinishTick;

            public Vector3 FirePosition;
            public Vector3 FireVelocity;

            public Vector3 HitPosition { get; set; }
        }
    }
}
