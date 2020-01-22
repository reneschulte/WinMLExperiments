using System.Linq;
using UnityEngine;

public class UserInput : MonoBehaviour
{
    private Transform _headTransform;

    public GameObject GazeCursor;

    public Vector3 HeadDirection => _headTransform.forward;
    public Vector3 HeadPosition => _headTransform.position;

    public Vector3 GazeHitPoint { get; private set; }
    public float GazeHitDistance { get; private set; }

    void Start()
    {
        _headTransform = Camera.main.transform;
    }

    void Update()
    {
        if (GazeCursor == null) return;

        // Get closest raycast hit 
        var raycastHits = Physics.RaycastAll(_headTransform.position, _headTransform.forward);
        var firstHit = raycastHits.OrderBy(r => r.distance).FirstOrDefault();

        // Set cursor to the position and store result
        GazeCursor.transform.position = firstHit.point;
        GazeCursor.transform.forward = firstHit.normal;
        GazeHitPoint = firstHit.point;
        GazeHitDistance = firstHit.distance;
    }
}