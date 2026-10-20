
using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class HeadsetPositionLogger : MonoBehaviour
{
    public Transform headsetTransform;

    [Range(1f, 30f)]
    public float samplesPerSecond = 10f;

    private string filePath;
    private Coroutine recordingCoroutine;

    void Start()
    {
        if (headsetTransform == null)
        {
            headsetTransform = transform;
        }

        filePath = Path.Combine(
            Application.persistentDataPath,
            "headset_position.csv"
        );

        if (!File.Exists(filePath))
        {
            File.WriteAllText(
                filePath,
                "Timestamp,PositionX,PositionY,PositionZ," +
                "RotationX,RotationY,RotationZ\n"
            );
        }

        Debug.Log("Headset position logger started: " + filePath);

        recordingCoroutine = StartCoroutine(RecordPosition());
    }

    IEnumerator RecordPosition()
    {
        while (true)
        {
            Vector3 position = headsetTransform.position;
            Vector3 rotation = headsetTransform.eulerAngles;

            string timestamp = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ"
            );

            string line = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}\n",
                timestamp,
                position.x,
                position.y,
                position.z,
                rotation.x,
                rotation.y,
                rotation.z
            );

            File.AppendAllText(filePath, line);

            yield return new WaitForSeconds(
                1f / samplesPerSecond
            );
        }
    }

    void OnDisable()
    {
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
    }
}
