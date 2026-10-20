
using System;
using System.IO;
using UnityEngine;
using Oculus.Interaction;

public class InteractionLogger : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Grabbable grabbable;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer cubeRenderer;
    [SerializeField] private Color idleColor = Color.blue;
    [SerializeField] private Color grabbedColor = Color.green;

    private Material cubeMaterial;
    private string logPath;

    private bool isGrabbed = false;
    private float grabStartTime;
    private Vector3 grabStartPosition;
    private int grabCount = 0;

    private void Awake()
    {
        logPath = Path.Combine(
            Application.persistentDataPath,
            "interaction_log.txt"
        );

        if (grabbable == null)
        {
            grabbable = GetComponent<Grabbable>();
        }

        if (cubeRenderer == null)
        {
            cubeRenderer = GetComponentInChildren<Renderer>();
        }

        if (cubeRenderer != null)
        {
            cubeMaterial = cubeRenderer.material;
        }
        else
        {
            Debug.LogWarning(
                "Cube Renderer not found. Visual feedback disabled."
            );
        }
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += OnPointerEvent;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= OnPointerEvent;
        }
    }

    private void Start()
    {
        SetCubeColor(idleColor);

        if (grabbable == null)
        {
            Debug.LogError("Grabbable component not found!");
            return;
        }

        Debug.Log("Interaction log location: " + logPath);

        SaveEvent("SESSION_STARTED", 0f, 0f);
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select && !isGrabbed)
        {
            isGrabbed = true;
            grabCount++;

            grabStartTime = Time.realtimeSinceStartup;
            grabStartPosition = grabbable.transform.position;

            SetCubeColor(grabbedColor);

            SaveEvent("GRABBED", 0f, 0f);
        }
        else if (evt.Type == PointerEventType.Unselect && isGrabbed)
        {
            isGrabbed = false;

            float duration =
                Time.realtimeSinceStartup - grabStartTime;

            Vector3 releasePosition =
                grabbable.transform.position;

            float displacement = Vector3.Distance(
                grabStartPosition,
                releasePosition
            );

            SetCubeColor(idleColor);

            SaveEvent("RELEASED", duration, displacement);
        }
    }

    private void SetCubeColor(Color color)
    {
        if (cubeMaterial == null)
        {
            return;
        }

        if (cubeMaterial.HasProperty("_BaseColor"))
        {
            cubeMaterial.SetColor("_BaseColor", color);
        }
        else if (cubeMaterial.HasProperty("_Color"))
        {
            cubeMaterial.SetColor("_Color", color);
        }
        else
        {
            Debug.LogWarning(
                "Material does not support a color property."
            );
        }
    }

    private void SaveEvent(
        string action,
        float duration,
        float displacement
    )
    {
        if (grabbable == null)
        {
            return;
        }

        string timestamp =
            DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ"
            );

        Vector3 position = grabbable.transform.position;

        string entry =
            $"{timestamp} | " +
            $"{grabbable.gameObject.name} | " +
            $"{action} | " +
            $"Position: ({position.x:F3}, " +
            $"{position.y:F3}, {position.z:F3}) | " +
            $"Grab Count: {grabCount} | " +
            $"Duration: {duration:F3}s | " +
            $"Displacement: {displacement:F3}m";

        try
        {
            File.AppendAllText(
                logPath,
                entry + Environment.NewLine
            );

            Debug.Log("INTERACTION LOG: " + entry);
        }
        catch (Exception error)
        {
            Debug.LogError(
                "Logging error: " + error.Message
            );
        }
    }

    private void OnDestroy()
    {
        if (cubeMaterial != null)
        {
            Destroy(cubeMaterial);
        }
    }
}
