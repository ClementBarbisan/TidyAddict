using UnityEngine;

namespace Projectiles
{
    public class Scene : MonoBehaviour
    {
        public Camera Camera { get; private set; }

        protected void Awake()
        {
            Camera = GetComponentInChildren<Camera>();
        }
    }
}
