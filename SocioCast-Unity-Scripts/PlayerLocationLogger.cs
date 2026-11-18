using System;
using System.IO;
using UnityEngine;
using Fusion;

public class PlayerLocationLogger : MonoBehaviour
{
    public Transform headsetTransform;  // Assign the headset (camera) transform here
    public float logInterval = 1f;  // Interval in seconds between logs

    private string logFilePath;  // Path to the log file
    private float nextLogTime = 0f;  // Time for the next log
    private NetworkRunner networkRunner;  // Fusion's NetworkRunner
    private PlayerRef playerRef;  // Reference to the local player
    private bool isLoggingStarted = false;  // Ensure logging only starts after connection

    // Start is called before the first frame update
    void Start()
    {
        // Assume the NetworkRunner is assigned or obtained dynamically in your scene
        networkRunner = FindObjectOfType<NetworkRunner>();
        if (networkRunner == null)
        {
            Debug.LogError("NetworkRunner not found in the scene.");
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Check if player is connected to the server
        if (!isLoggingStarted && networkRunner.IsRunning && networkRunner.SessionInfo.IsValid)
        {
            // Player is connected, start logging
            StartLogging();
            isLoggingStarted = true;
        }

        // If logging has started, log the headset position at the defined interval
        if (isLoggingStarted && Time.time >= nextLogTime)
        {
            LogHeadsetPosition();
            nextLogTime = Time.time + logInterval;
        }
    }

    // Starts the logging process
    private void StartLogging()
    {
        // Get the local player reference
        playerRef = networkRunner.LocalPlayer;

        // Retrieve the player name or ID (replace this with your method to get the name/ID)
        string userNameOrId = GetPlayerNameOrId(playerRef);

        // Create a unique log file path with the player name/ID and timestamp
        logFilePath = Path.Combine(Application.persistentDataPath, $"PlayerLog_{userNameOrId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        // Write the log file creation timestamp
        File.AppendAllText(logFilePath, $"Log file created at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n");

        // Log the initial position
        LogHeadsetPosition();
    }

    // Logs the current headset position and writes it to the file
    private void LogHeadsetPosition()
    {
        if (headsetTransform != null)
        {
            Vector3 position = headsetTransform.position;
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}: Headset Position - X: {position.x}, Y: {position.y}, Z: {position.z}\n";

            // Append the log entry to the log file
            File.AppendAllText(logFilePath, logEntry);
        }
        else
        {
            Debug.LogWarning("Headset Transform is not assigned!");
        }
    }

    // Helper method to retrieve player name or ID
    private string GetPlayerNameOrId(PlayerRef playerRef)
    {
        // Replace with your method to get the player name or ID
        // For now, we use the PlayerRef's PlayerId as a unique identifier
        return $"Player_{playerRef.PlayerId}";
    }
}
