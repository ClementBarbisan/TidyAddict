using Fusion;

namespace Projectiles
{
    public class Cube : NetworkBehaviour
    {
        public override void Spawned()
        {
            // All cubes in the level are simulated on all clients.
            // Physics prediction is turned on (check prefab RunnerBase, RunnerSimulatePhysics3D component)
            // so without objects being properly simulated it would result in jittery motion on clients.
            Runner.SetIsSimulated(Object, true);
        }
    }
}
