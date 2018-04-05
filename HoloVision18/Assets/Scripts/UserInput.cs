using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class UserInput : MonoBehaviour
{
    private GestureRecognizer _gestureRecognizer;

    public GameObject GazeCursor;

    public Vector3 HeadDirection => Camera.main.transform.forward;
    public Vector3 HeadPosition => Camera.main.transform.position;

    public Vector3 GazeHitPoint { get; private set; }

    void Start()
    {
        _gestureRecognizer = new GestureRecognizer();
        _gestureRecognizer.Tapped += HandTapped;
        _gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
        _gestureRecognizer.StartCapturingGestures();
    }

    private void HandTapped(TappedEventArgs args)
    {
    }

    void Update()
    {
        if (GazeCursor == null) return;

        // Get closest raycast hit 
        var raycastHits = Physics.RaycastAll(HeadPosition, HeadDirection);
        var firstHit = raycastHits.OrderBy(r => r.distance).FirstOrDefault();

        // Set cursor to the position
        GazeCursor.transform.position = firstHit.point;
        GazeCursor.transform.forward = firstHit.normal;
        GazeHitPoint = firstHit.point;
    }

    private void OnDestroy()
    {
        if (_gestureRecognizer != null)
        {
            if (_gestureRecognizer.IsCapturingGestures())
            {
                _gestureRecognizer.StopCapturingGestures();
            }
            _gestureRecognizer.Dispose();
        }
    }
}