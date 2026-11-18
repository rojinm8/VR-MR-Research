
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class RealTimePoseAudioGazeStreamer : MonoBehaviour
{
    [Header("Headset and Eye Tracking")]
    public Transform headsetTransform;
    public EyeGazeReader eyeGazeReader;

    [Header("Device")]
    public string deviceId = "quest_4";

    [Header("Python Server")]
    public string serverUrl =
        "http://172.20.10.6:5001/stream/chunk";

    [Header("Audio")]
    public int sampleRate = 16000;

    [Range(50, 200)]
    public int chunkMilliseconds = 100;

    [Header("Network")]
    public int maxQueuedPackets = 100;

    [Serializable]
    private class GazeData
    {
        public bool valid;
        public double timestamp;
        public Vector3 origin;
        public Vector3 direction;
    }

    [Serializable]
    private class StreamPacket
    {
        public string device_id;
        public string session_id;

        public int sequence;
        public long sample_start;
        public int sample_rate;

        public string capture_utc;
        public double unity_time_seconds;

        public Vector3 position;
        public Quaternion rotation;

        public GazeData gaze;
        public string audio_base64;
    }

    private AudioClip microphoneClip;

    private readonly Queue<string> packetQueue =
        new Queue<string>();

    private string sessionId;

    private int readPosition;
    private int chunkFrames;
    private int sequenceNumber;
    private int previousMicPosition = -1;

    private long sampleIndex;

    private bool microphoneRunning;
    private bool streaming;
    private bool starting;

    private int droppedPackets;
    private int successfulPackets;

    // --------------------------------------------------
    // STARTUP AND RESTART
    // --------------------------------------------------

    private void OnEnable()
    {
        Debug.Log(
            "LIVE: ResearchDataStreamer enabled."
        );

        BeginStreaming();
    }

    private void BeginStreaming()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        // Prevent duplicate initialization.
        if (starting || streaming)
        {
            return;
        }

        starting = true;

        StartCoroutine(InitializeStreaming());
    }

    private IEnumerator InitializeStreaming()
    {
        Debug.Log(
            "LIVE: Initializing streamer for " + deviceId
        );

        // Allow other scene components to initialize.
        yield return null;

        if (headsetTransform == null)
        {
            Debug.LogError(
                "LIVE: Assign CenterEyeAnchor."
            );

            starting = false;
            yield break;
        }

        if (eyeGazeReader == null)
        {
            Debug.LogError(
                "LIVE: Assign EyeGazeTracker."
            );

            starting = false;
            yield break;
        }

        if (sampleRate <= 0)
        {
            Debug.LogError(
                "LIVE: Invalid sample rate."
            );

            starting = false;
            yield break;
        }

        if (string.IsNullOrEmpty(serverUrl))
        {
            Debug.LogError(
                "LIVE: Python server URL is missing."
            );

            starting = false;
            yield break;
        }

#if UNITY_ANDROID

        if (!Permission.HasUserAuthorizedPermission(
                Permission.Microphone))
        {
            Debug.Log(
                "LIVE: Requesting microphone permission."
            );

            Permission.RequestUserPermission(
                Permission.Microphone
            );

            float waitTime = 0f;

            while (!Permission.HasUserAuthorizedPermission(
                       Permission.Microphone)
                   && waitTime < 30f)
            {
                waitTime += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(
                    Permission.Microphone))
            {
                Debug.LogError(
                    "LIVE: Microphone permission denied."
                );

                starting = false;
                yield break;
            }
        }

#endif

        Debug.Log(
            "LIVE: Microphone permission confirmed."
        );

        // Give the headset audio system time to settle.
        yield return new WaitForSecondsRealtime(2f);

        // Create a new recording session.
        sessionId = "session_" +
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss"
            ) + "_" +
            Guid.NewGuid().ToString("N").Substring(0, 8);

        packetQueue.Clear();

        readPosition = 0;
        sampleIndex = 0;
        sequenceNumber = 0;
        previousMicPosition = -1;

        droppedPackets = 0;
        successfulPackets = 0;

        Debug.Log(
            "LIVE: Starting microphone."
        );

        microphoneClip = Microphone.Start(
            null,
            true,
            10,
            sampleRate
        );

        if (microphoneClip == null)
        {
            Debug.LogError(
                "LIVE: Microphone could not start."
            );

            starting = false;
            yield break;
        }

        microphoneRunning = true;

        float timer = 0f;

        while (Microphone.GetPosition(null) <= 0
               && timer < 5f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (Microphone.GetPosition(null) <= 0)
        {
            Debug.LogError(
                "LIVE: No microphone samples."
            );

            StopMicrophone();

            starting = false;
            yield break;
        }

        if (microphoneClip.channels != 1)
        {
            Debug.LogError(
                "LIVE: Mono microphone audio required."
            );

            StopMicrophone();

            starting = false;
            yield break;
        }

        sampleRate = microphoneClip.frequency;

        chunkFrames = Mathf.Max(
            1,
            Mathf.RoundToInt(
                sampleRate * chunkMilliseconds / 1000f
            )
        );

        readPosition = 0;
        sampleIndex = 0;
        sequenceNumber = 0;
        previousMicPosition = -1;

        streaming = true;
        starting = false;

        Debug.Log(
            "LIVE: STREAMING STARTED - " +
            deviceId + " / " + sessionId
        );

        StartCoroutine(SendPackets());
    }

    // --------------------------------------------------
    // AUDIO, HEAD POSITION, AND EYE GAZE
    // --------------------------------------------------

    private void LateUpdate()
    {
        if (!streaming ||
            !microphoneRunning ||
            microphoneClip == null)
        {
            return;
        }

        int micPosition =
            Microphone.GetPosition(null);

        if (micPosition < 0)
        {
            return;
        }

        int bufferLength = microphoneClip.samples;

        if (bufferLength <= 0)
        {
            return;
        }

        if (previousMicPosition >= 0)
        {
            int advanced =
                (micPosition - previousMicPosition +
                 bufferLength) % bufferLength;

            if (advanced > bufferLength / 2)
            {
                Debug.LogWarning(
                    "LIVE: Large microphone buffer advance."
                );
            }
        }

        previousMicPosition = micPosition;

        int available =
            (micPosition - readPosition +
             bufferLength) % bufferLength;

        while (available >= chunkFrames)
        {
            float[] samples =
                new float[chunkFrames];

            if (!microphoneClip.GetData(
                    samples,
                    readPosition))
            {
                Debug.LogError(
                    "LIVE: Microphone GetData failed."
                );

                return;
            }

            // Convert microphone samples to PCM16.
            byte[] audio = new byte[chunkFrames * 2];

            for (int i = 0; i < chunkFrames; i++)
            {
                float value = Mathf.Clamp(
                    samples[i], -1f, 1f
                );

                short pcm =
                    (short)Mathf.RoundToInt(
                        value * 32767f
                    );

                audio[2 * i] =
                    (byte)(pcm & 255);

                audio[2 * i + 1] =
                    (byte)((pcm >> 8) & 255);
            }

            // Capture eye gaze.
            GazeData gaze = new GazeData
            {
                valid = eyeGazeReader.gazeValid,

                timestamp =
                    eyeGazeReader.gazeTimestamp,

                origin =
                    eyeGazeReader.gazePosition,

                direction =
                    eyeGazeReader.gazeDirection
            };

            // Capture synchronized research data.
            StreamPacket packet = new StreamPacket
            {
                device_id = deviceId,
                session_id = sessionId,

                sequence = sequenceNumber,
                sample_start = sampleIndex,
                sample_rate = sampleRate,

                capture_utc =
                    DateTime.UtcNow.ToString("o"),

                unity_time_seconds =
                    Time.realtimeSinceStartupAsDouble,

                position =
                    headsetTransform.position,

                rotation =
                    headsetTransform.rotation,

                gaze = gaze,

                audio_base64 =
                    Convert.ToBase64String(audio)
            };

            string json =
                JsonUtility.ToJson(packet);

            if (packetQueue.Count >=
                Mathf.Max(1, maxQueuedPackets))
            {
                packetQueue.Dequeue();

                droppedPackets++;

                Debug.LogWarning(
                    "LIVE: Packet queue overflow. " +
                    "Dropped = " + droppedPackets
                );
            }

            packetQueue.Enqueue(json);

            readPosition =
                (readPosition + chunkFrames)
                % bufferLength;

            sampleIndex += chunkFrames;

            sequenceNumber++;

            available -= chunkFrames;
        }
    }

    // --------------------------------------------------
    // SEND PACKETS TO PYTHON
    // --------------------------------------------------

    private IEnumerator SendPackets()
    {
        while (streaming)
        {
            if (packetQueue.Count == 0)
            {
                yield return null;
                continue;
            }

            string json =
                packetQueue.Dequeue();

            byte[] bytes =
                Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request =
                new UnityWebRequest(
                    serverUrl,
                    "POST"))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(bytes);

                request.downloadHandler =
                    new DownloadHandlerBuffer();

                request.SetRequestHeader(
                    "Content-Type",
                    "application/json"
                );

                request.timeout = 4;

                yield return request.SendWebRequest();

                if (request.result !=
                    UnityWebRequest.Result.Success ||
                    request.responseCode != 200)
                {
                    Debug.LogWarning(
                        "LIVE: Send failed. HTTP " +
                        request.responseCode + " " +
                        request.error + " " +
                        request.downloadHandler.text
                    );
                }
                else
                {
                    successfulPackets++;

                    if (successfulPackets % 10 == 0)
                    {
                        Debug.Log(
                            "LIVE: Sent " +
                            successfulPackets +
                            " synchronized packets."
                        );
                    }
                }
            }
        }
    }

    // --------------------------------------------------
    // STOP AND CLEANUP
    // --------------------------------------------------

    private void StopMicrophone()
    {
        if (microphoneRunning)
        {
            Microphone.End(null);

            microphoneRunning = false;
        }

        microphoneClip = null;
    }

    private void StopStreaming()
    {
        // Stop initialization and network coroutines.
        StopAllCoroutines();

        streaming = false;
        starting = false;

        StopMicrophone();

        packetQueue.Clear();
    }

    private void OnDisable()
    {
        Debug.Log(
            "LIVE: ResearchDataStreamer disabled."
        );

        StopStreaming();
    }

    private void OnDestroy()
    {
        StopStreaming();
    }

    // --------------------------------------------------
    // QUEST APP PAUSE AND RESUME
    // --------------------------------------------------

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            Debug.Log(
                "LIVE: Application paused."
            );

            StopStreaming();
        }
        else
        {
            Debug.Log(
                "LIVE: Application resumed."
            );

            BeginStreaming();
        }
    }
}
