using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrabObject : NetworkBehaviour
{
    [SerializeField] private InputActionReference _grab;
    [SerializeField] private InputActionReference _throw;
    private Transform _cam;
    private NetworkObject _obj;
    private PlayerRef _ref;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        _cam = Camera.main.transform;
        _grab.action.Enable();
        _grab.action.performed += TryToGrab;
        _ref = GetComponent<NetworkObject>().InputAuthority;
    }
    
    void OnDisable()
    {
        _grab.action.Disable();
        _grab.action.performed -= TryToGrab;
    }

    private void TryToGrab(InputAction.CallbackContext obj)
    {
        if (_obj == null && Physics.Raycast(_cam.position, _cam.forward, out RaycastHit hit, 2.5f))
        {
            if (hit.collider.CompareTag("Grabbable"))
            {
                _obj = hit.collider.GetComponent<NetworkObject>();
                _obj.GetComponent<GrabbableObject>().Grab(_ref, _cam);
            }
        }
        else if (_obj)
        {
            _obj.GetComponent<GrabbableObject>().Release(_cam.forward * 2);
            _obj = null;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
