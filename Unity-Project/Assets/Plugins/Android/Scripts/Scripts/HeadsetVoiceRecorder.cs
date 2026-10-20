
using System;
using System.Collections;
using System.IO;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class HeadsetVoiceRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [Range(5, 60)]
    public int recordingSeconds = 20;

    public int sampleRate = 16000;

    private AudioClip recordedClip;
    private string microphoneDevice;
    private bool isRecording = false;

    private IEnumerator Start()
    {
        Debug.Log("VOICE: Initializing microphone recorder.");

#if UNITY_ANDROID
        // Check Android microphone permission.
        if (!Permission.HasUserAuthorizedPermission(
                Permission.Microphone))
        {
            Debug.Log("VOICE: Requesting microphone permission.");

            Permission.RequestUserPermission(
                Permission.Microphone
            );

            // Wait briefly for the user to respond.
            float permissionTimer = 0f;

            while (!Permission.HasUserAuthorizedPermission(
                       Permission.Microphone)
                   && permissionTimer < 30f)
            {
                permissionTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(
                    Permission.Microphone))
            {
                Debug.LogError(
                    "VOICE: Microphone permission was not granted."
                );

                yield break;
            }
        }
#endif

        Debug.Log("VOICE: Microphone permission granted.");

        // Allow the XR application to initialize.
        yield return new WaitForSecondsRealtime(2f);

        // Check available microphone devices.
        string[] devices = Microphone.devices;

        Debug.Log(
            "VOICE: Number of microphone devices = " +
            devices.Length
        );

        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log(
                "VOICE: Available microphone " +
                i + " = " + devices[i]
            );
        }

        // A null name selects Unity's default microphone.
        microphoneDevice = null;

        Debug.Log("VOICE: Using default microphone.");

        yield return StartCoroutine(RecordVoice());
    }

    private IEnumerator RecordVoice()
    {
        if (isRecording)
        {
            Debug.LogWarning(
                "VOICE: Recording is already running."
            );
            yield break;
        }

        // Extra buffer length prevents wrapping during our test.
        int bufferSeconds = recordingSeconds + 5;

        recordedClip = Microphone.Start(
            microphoneDevice,
            false,
            bufferSeconds,
            sampleRate
        );

        if (recordedClip == null)
        {
            Debug.LogError(
                "VOICE: Microphone.Start returned null."
            );
            yield break;
        }

        isRecording = true;

        Debug.Log("VOICE: Microphone.Start succeeded.");

        // Wait until microphone samples begin arriving.
        float startTimer = 0f;

        while (Microphone.GetPosition(microphoneDevice) <= 0
               && startTimer < 5f)
        {
            startTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        int initialPosition =
            Microphone.GetPosition(microphoneDevice);

        Debug.Log(
            "VOICE: Initial microphone position = " +
            initialPosition
        );

        if (initialPosition <= 0)
        {
            Debug.LogError(
                "VOICE: Microphone did not begin capturing samples."
            );

            StopMicrophone();
            yield break;
        }

        Debug.Log("VOICE: RECORDING STARTED");

        // Record for the configured time.
        yield return new WaitForSecondsRealtime(recordingSeconds);

        // Check microphone position before stopping.
        int recordedFrames =
            Microphone.GetPosition(microphoneDevice);

        Debug.Log(
            "VOICE DIAGNOSTIC: Recorded frames = " +
            recordedFrames
        );

        Debug.Log(
            "VOICE DIAGNOSTIC: Clip frequency = " +
            recordedClip.frequency
        );

        Debug.Log(
            "VOICE DIAGNOSTIC: Clip channels = " +
            recordedClip.channels
        );

        Debug.Log(
            "VOICE DIAGNOSTIC: Clip sample capacity = " +
            recordedClip.samples
        );

        if (recordedFrames <= 0)
        {
            Debug.LogError(
                "VOICE: No audio frames were recorded."
            );

            StopMicrophone();
            yield break;
        }

        int channels = recordedClip.channels;

        // Read microphone samples while capture is active.
        float[] samples = new float[
            recordedClip.samples * channels
        ];

        bool readSuccess = recordedClip.GetData(samples, 0);

        StopMicrophone();

        if (!readSuccess)
        {
            Debug.LogError(
                "VOICE: AudioClip.GetData failed."
            );
            yield break;
        }

        int sampleCount = Mathf.Min(
            recordedFrames * channels,
            samples.Length
        );

        // Analyze the audio to detect silence.
        float peakAmplitude = 0f;
        double sumSquares = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float value = samples[i];
            float absoluteValue = Mathf.Abs(value);

            if (absoluteValue > peakAmplitude)
            {
                peakAmplitude = absoluteValue;
            }

            sumSquares += (double)value * value;
        }

        double rmsAmplitude = Math.Sqrt(
            sumSquares / Math.Max(1, sampleCount)
        );

        Debug.Log(
            "VOICE DIAGNOSTIC: Peak amplitude = " +
            peakAmplitude.ToString("F6")
        );

        Debug.Log(
            "VOICE DIAGNOSTIC: RMS amplitude = " +
            rmsAmplitude.ToString("F6")
        );

        if (peakAmplitude == 0f)
        {
            Debug.LogWarning(
                "VOICE WARNING: Recording contains digital silence."
            );
        }
        else
        {
            Debug.Log(
                "VOICE: Nonzero audio samples detected."
            );
        }

        // Create a unique filename for each recording.
        string filename =
            "voice_recording_" +
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss"
            ) +
            ".wav";

        string filePath = Path.Combine(
            Application.persistentDataPath,
            filename
        );

        try
        {
            SaveWav(
                filePath,
                samples,
                sampleCount,
                channels,
                recordedClip.frequency
            );

            Debug.Log(
                "VOICE: RECORDING SAVED: " + filePath
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "VOICE: Failed to save recording: " +
                exception.Message
            );
        }

        recordedClip = null;
    }

    private void StopMicrophone()
    {
        if (isRecording)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;

            Debug.Log("VOICE: Microphone stopped.");
        }
    }

    private void SaveWav(
        string path,
        float[] samples,
        int sampleCount,
        int channels,
        int frequency)
    {
        // Save as 16-bit PCM WAV.
        using (FileStream stream = new FileStream(
            path, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int dataLength = sampleCount * 2;

            // RIFF header
            writer.Write(
                System.Text.Encoding.ASCII.GetBytes("RIFF")
            );

            writer.Write(36 + dataLength);

            writer.Write(
                System.Text.Encoding.ASCII.GetBytes("WAVE")
            );

            // Audio format
            writer.Write(
                System.Text.Encoding.ASCII.GetBytes("fmt ")
            );

            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            // Audio sample data
            writer.Write(
                System.Text.Encoding.ASCII.GetBytes("data")
            );

            writer.Write(dataLength);

            for (int i = 0; i < sampleCount; i++)
            {
                float value = Mathf.Clamp(
                    samples[i], -1f, 1f
                );

                short pcm = (short)(value * 32767f);

                writer.Write(pcm);
            }
        }
    }

    private void OnDisable()
    {
        StopMicrophone();
    }

    private void OnDestroy()
    {
        StopMicrophone();
    }
}
