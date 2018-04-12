using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class UserInput : MonoBehaviour
{
    private Transform _headTransform;
    private GestureRecognizer _gestureRecognizer;

    public GameObject GazeCursor;

    public Vector3 HeadDirection => _headTransform.forward;
    public Vector3 HeadPosition => _headTransform.position;

    public Vector3 GazeHitPoint { get; private set; }

    public event Action Tapped;

    void Start()
    {
        _headTransform = Camera.main.transform;

        // Attach gesture handlers

        _gestureRecognizer = new GestureRecognizer();
        _gestureRecognizer.Tapped += GestureRecognizerOnTapped;
        _gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
        _gestureRecognizer.StartCapturingGestures();
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
    }

    private void GestureRecognizerOnTapped(TappedEventArgs tappedEventArgs)
    {
        Tapped?.Invoke();
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