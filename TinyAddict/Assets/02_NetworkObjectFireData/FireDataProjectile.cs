using Fusion;
using UnityEngine;

namespace Projectiles.NetworkObjectFireData
{
    // FireDataProjectile is still a NetworkObject spawned in the scene, but it does not use NetworkRigidbody
    // or NetworkTransform to constantly synchronize position and rotation to all peers. Instead only fire data
    // (fire position, fire rotation) is saved on the start and it's position for specific time is calculated based
    // on this data separately on all peers.
    // This approach is more suitable than to use NetworkTransform but there is still overhead when spawning new
    // NetworkObject for each projectile. For hitscan projectiles, solutions from Example 03 and 04 are much more
    // efficient and easier. For kinematic projectiles use this solution only when the projectile needs to live for
    // a long time or simplicity is a key. Otherwise kinematic projectile data buffer (Example 05) is a better option.
    public class FireDataProjectile : NetworkBehaviour
    {
        // PRIVATE MEMBERS

        [SerializeField]
        private float _speed = 50f;
        [SerializeField]
        private float _lifeTime = 4f;
        [SerializeField]
        private LayerMask _hitMask;
        [SerializeField]
        private float _hitImpulse = 50f;
        [SerializeField]
        private GameObject _hitEffect;
        [SerializeField]
        private float _lifeTimeAfterHit = 2f;
        [SerializeField]
        private GameObject _visualsRoot;
        [SerializeField]
        private TrailRenderer _trail;

        [Networked]
        private int _fireTick { get; set; }
        [Networked]
        private Vector3 _firePosition { get; set; }
        [Networked]
        private Vector3 _fireVelocity { get; set; }
        [Networked]
        private NetworkBool _isDestroyed { get; set; }
        [Networked]
        private TickTimer _lifeCooldown { get; set; }
        [Networked]
        private Vector3 _hitPosition { get; set; }

        private bool _isInitializedRender;
        private bool _isDestroyedRender;

        // PUBLIC METHODS

        public void Fire(Vector3 position, Vector3 direction)
        {
            // Save fire data
            _fireTick = Runner.Tick;
            _firePosition = position;
            _fireVelocity = direction * _speed;

            if (_lifeTime > 0f)
            {
                _lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTime);
            }
        }

        // NetworkBehaviour INTERFACE

        public override void FixedUpdateNetwork()
        {
            if (_lifeCooldown.IsRunning && _lifeCooldown.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            if (_isDestroyed)
                return;

            // Previous and next position is calculated based on the initial parameters.
            // There is no point in actually moving the object in FUN.
            var previousPosition = GetMovePosition(Runner.Tick - 1);
            var nextPosition = GetMovePosition(Runner.Tick);

            var direction = nextPosition - previousPosition;

            float distance = direction.magnitude;
            direction /= distance; // Normalize

            var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;
            if (Runner.LagCompensation.Raycast(previousPosition, direction, distance,
                    Object.InputAuthority, out var hit, _hitMask, hitOptions))
            {
                _isDestroyed = true;
                _lifeCooldown = TickTimer.CreateFromSeconds(Runner, _lifeTimeAfterHit);

                // Save hit position so hit effects are at correct position on proxies
                _hitPosition = hit.Point;

                if (hit.Collider != null && hit.Collider.attachedRigidbody != null)
                {
                    hit.Collider.attachedRigidbody.AddForce(direction * _hitImpulse, ForceMode.Impulse);
                }
            }
        }

        public override void Render()
        {
            InitializeRender();

            if (_isDestroyed && _isDestroyedRender == false)
            {
                _isDestroyedRender = true;
                ShowDestroyEffect();
            }

            if (_isDestroyed)
                return;

            // Object.RenderTime returns remote time for proxies, and local time for input/state authority.
            float floatTick = Object.RenderTime / Runner.DeltaTime;

            // It is enough to move the object only in Render.
            // In FUN previous and next position can be calculated from initial parameters.
            transform.position = GetMovePosition(floatTick);
        }

        // MONOBEHAVIOUR

        protected void Awake()
        {
            if (_hitEffect != null)
            {
                _hitEffect.SetActive(false);
            }

            if (_trail != null)
            {
                _trail.gameObject.SetActive(false);
            }
        }

        protected void OnEnable()
        {
            _isInitializedRender = false;
            _isDestroyedRender = false;
        }

        // PRIVATE METHODS

        private void InitializeRender()
        {
            if (_isInitializedRender)
                return;

            // Usually we would do following functionality in Spawned
            // but since this projectile can be used in NetworkObjectBuffer
            // Spawned is called too soon (when adding object to buffer)
            // so we do it in first Render instead

            // Set initial position and rotation on proxies
            if (IsProxy)
            {
                transform.position = _firePosition;
                transform.rotation = Quaternion.LookRotation(_fireVelocity);
            }

            if (_trail != null)
            {
                _trail.gameObject.SetActive(true);
                _trail.Clear();
            }

            _isInitializedRender = true;
        }

        private Vector3 GetMovePosition(float currentTick)
        {
            float time = (currentTick - _fireTick) * Runner.DeltaTime;

            if (time <= 0f)
                return _firePosition;

            return _firePosition + _fireVelocity * time;
        }

        private void ShowDestroyEffect()
        {
            transform.position = _hitPosition;

            if (_hitEffect != null)
            {
                _hitEffect.SetActive(true);
            }

            // Hide projectile visual
            _visualsRoot.SetActive(false);
        }
    }
}
