using UnityEngine;
using System.IO;
using System;
using Fusion;

public class VoiceRecorder : MonoBehaviour
{
    private AudioClip audioClip;
    private int sampleRate = 44100;
    private string deviceName;
    private int channels = 1;
    private int lastSamplePosition = 0;
    private bool isRecording = false;
    private FileStream fileStream;
    private NetworkRunner networkRunner;  // Fusion's NetworkRunner
    private PlayerRef playerRef;  // Reference to the local player
    private bool isStopping;

    void Start()
    {
        // Assume the NetworkRunner is assigned or obtained dynamically in your scene
        networkRunner = FindObjectOfType<NetworkRunner>();
        if (networkRunner == null)
        {
            Debug.LogError("NetworkRunner not found in the scene.");
            return;
        }

        // Check if any microphones are available
        if (Microphone.devices.Length > 0)
        {
            deviceName = Microphone.devices[0];
            Debug.Log("Microphone found: " + deviceName);
        }
        else
        {
            Debug.LogWarning("No microphone device found. WAV recording is disabled.", this);
            return;
        }

        // Start the recording process
        StartRecording();
    }

    void StartRecording()
    {
        // Start recording from the microphone
        audioClip = Microphone.Start(deviceName, true, 10, sampleRate);

        if (audioClip == null)
        {
            Debug.LogError("Failed to start recording from the microphone.");
        }
        else
        {
            channels = audioClip.channels;
            lastSamplePosition = 0;
            isRecording = true;
            Debug.Log("Recording started successfully.");
            OpenWavFile();
        }
    }

    void StopRecording()
    {
        if (!isRecording || isStopping)
        {
            return;
        }

        isStopping = true;
        try
        {
            isRecording = false;
            if (!string.IsNullOrEmpty(deviceName))
            {
                Microphone.End(deviceName);
            }

            CloseWavFile();
        }
        finally
        {
            isStopping = false;
        }
    }

    void Update()
    {
        if (isRecording)
        {
            WriteAudioToFile(); // Write audio data to file continuously
        }
    }

    private void OpenWavFile()
    {
        // Get the local player reference
        playerRef = networkRunner.LocalPlayer;
        string userNameOrId = GetPlayerNameOrId(playerRef);

        // Create a WAV file
        string filePath = Path.Combine(Application.persistentDataPath, $"PlayerLog_{userNameOrId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.wav");
        fileStream = new FileStream(filePath, FileMode.Create);

        // Write a placeholder WAV header
        WriteWavHeader(fileStream, 0, channels, sampleRate);
        Debug.Log("Recording file created at: " + filePath);
    }

    private string GetPlayerNameOrId(PlayerRef playerRef)
    {
        // Placeholder for player ID or name
        return $"Player_{playerRef.PlayerId}";
    }

    private void CloseWavFile()
    {
        // Calculate the final data size and update the header
        if (fileStream == null)
        {
            return;
        }

        int dataSize = Math.Max(0, (int)fileStream.Length - 44); // Subtract header size (44 bytes)
        fileStream.Seek(4, SeekOrigin.Begin);
        fileStream.Write(BitConverter.GetBytes(fileStream.Length - 8), 0, 4); // Update RIFF chunk size
        fileStream.Seek(40, SeekOrigin.Begin);
        fileStream.Write(BitConverter.GetBytes(dataSize), 0, 4); // Update data size
        fileStream.Close();
        fileStream = null;

        Debug.Log("Recording stopped and file saved.");
    }

    private void WriteAudioToFile()
    {
        if (audioClip == null || fileStream == null || string.IsNullOrEmpty(deviceName)) return;

        int currentPosition = Microphone.GetPosition(deviceName);
        if (currentPosition <= 0 || currentPosition >= audioClip.samples || currentPosition == lastSamplePosition)
        {
            return;
        }

        int sampleLength;
        if (currentPosition > lastSamplePosition)
        {
            sampleLength = currentPosition - lastSamplePosition; // Simple case: buffer hasn't wrapped
        }
        else
        {
            sampleLength = (audioClip.samples - lastSamplePosition) + currentPosition; // Handle buffer wrap-around
        }

        float[] samples = new float[sampleLength * channels];
        if (currentPosition > lastSamplePosition)
        {
            audioClip.GetData(samples, lastSamplePosition); // Read data between last position and current position
        }
        else
        {
            // Handle wrap-around case
            float[] part1 = new float[(audioClip.samples - lastSamplePosition) * channels];
            audioClip.GetData(part1, lastSamplePosition);
            float[] part2 = new float[currentPosition * channels];
            audioClip.GetData(part2, 0);
            Array.Copy(part1, samples, part1.Length);
            Array.Copy(part2, 0, samples, part1.Length, part2.Length);
        }

        lastSamplePosition = currentPosition;

        // Convert samples to 16-bit PCM and write to file
        byte[] wavData = ConvertToPCM(samples);
        fileStream.Write(wavData, 0, wavData.Length);
    }

    private byte[] ConvertToPCM(float[] samples)
    {
        MemoryStream stream = new MemoryStream();
        for (int i = 0; i < samples.Length; i++)
        {
            short intSample = (short)(samples[i] * short.MaxValue); // Convert float [-1.0, 1.0] to 16-bit PCM
            byte[] byteArr = BitConverter.GetBytes(intSample);
            stream.Write(byteArr, 0, byteArr.Length);
        }
        return stream.ToArray();
    }

    private void WriteWavHeader(FileStream stream, int sampleCount, int channels, int sampleRate)
    {
        int byteRate = sampleRate * channels * 2;
        int blockAlign = channels * 2;

        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + sampleCount * 2), 0, 4); // Placeholder for size
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4); // Size of fmt chunk
        stream.Write(BitConverter.GetBytes((short)1), 0, 2); // PCM format
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2); // Number of channels
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4); // Sample rate
        stream.Write(BitConverter.GetBytes(byteRate), 0, 4); // Byte rate
        stream.Write(BitConverter.GetBytes((short)blockAlign), 0, 2); // Block align
        stream.Write(BitConverter.GetBytes((short)16), 0, 2); // Bits per sample
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(sampleCount * 2), 0, 4); // Placeholder for data size
    }

    private void OnApplicationQuit()
    {
        StopRecording(); // Stop recording and close the file on application quit
    }

    private void OnDisable()
    {
        StopRecording();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopRecording(); // Stop recording if the application is paused
        }
    }
}
