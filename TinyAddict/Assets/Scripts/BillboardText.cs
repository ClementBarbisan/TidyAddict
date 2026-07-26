using UnityEngine;

public class BillboardText : MonoBehaviour
{
    private Transform _cam;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _cam = Camera.main.transform;
    }

    // Update is called once per frame
    void Update()
    {
        transform.forward = (transform.position - _cam.position).normalized;
    }
}
