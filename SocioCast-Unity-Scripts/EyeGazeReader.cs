
using UnityEngine;

public class EyeGazeReader : MonoBehaviour
{
    [Header("Eye Gaze Source")]
    public OVREyeGaze eyeGaze;

    [Header("Eye Gaze Data")]
    public bool gazeValid = false;
    public Vector3 gazePosition = Vector3.zero;
    public Vector3 gazeDirection = Vector3.zero;
    public double gazeTimestamp = 0.0;

    [Header("Diagnostics")]
    public bool showDiagnostics = true;
    public float diagnosticInterval = 2.0f;
    public float minimumConfidence = 0.5f;

    private float nextLogTime = 0f;
    private bool wasValid = false;

    private void Update()
    {
        gazeValid = false;
        gazePosition = Vector3.zero;
        gazeDirection = Vector3.zero;
        gazeTimestamp = 0.0;

        if (eyeGaze != null &&
            eyeGaze.isActiveAndEnabled &&
            eyeGaze.EyeTrackingEnabled &&
            eyeGaze.Confidence >= minimumConfidence)
        {
            gazePosition = eyeGaze.transform.position;
            gazeDirection = eyeGaze.transform.forward.normalized;

            // Unity capture time, not the native eye sensor timestamp.
            gazeTimestamp = Time.realtimeSinceStartupAsDouble;

            gazeValid = true;
        }

        if (showDiagnostics &&
            Time.unscaledTime >= nextLogTime)
        {
            nextLogTime = Time.unscaledTime +
                Mathf.Max(0.5f, diagnosticInterval);

            Debug.Log(
                "GAZE DIAGNOSTIC: " +
                "SourceAssigned=" + (eyeGaze != null) +
                " GazeValid=" + gazeValid +
                " Confidence=" +
                (eyeGaze != null ? eyeGaze.Confidence : 0f)
            );

            if (gazeValid)
            {
                Debug.Log(
                    "GAZE DATA: " +
                    "Origin=" + gazePosition +
                    " Direction=" + gazeDirection +
                    " Timestamp=" + gazeTimestamp
                );
            }
        }

        if (gazeValid != wasValid)
        {
            if (gazeValid)
                Debug.Log("GAZE: Eye tracking became VALID.");
            else
                Debug.LogWarning("GAZE: Eye tracking became INVALID.");

            wasValid = gazeValid;
        }
    }
}
