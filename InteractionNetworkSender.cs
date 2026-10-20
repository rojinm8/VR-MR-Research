
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Oculus.Interaction;

public class InteractionNetworkSender : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Grabbable grabbable;

    [Header("Python Server")]
    [SerializeField]
    private string serverUrl =
        "http://172.27.100.9:5000/interaction";

    [Serializable]
    private class InteractionEvent
    {
        public string timestamp;
        public string object_name;
        public string action;
        public Vector3 position;
    }

    private bool isGrabbed = false;

    private void Awake()
    {
        if (grabbable == null)
        {
            grabbable = GetComponent<Grabbable>();
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

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select && !isGrabbed)
        {
            isGrabbed = true;
            SendInteraction("GRABBED");
        }
        else if (evt.Type == PointerEventType.Unselect && isGrabbed)
        {
            isGrabbed = false;
            SendInteraction("RELEASED");
        }
    }

    private void SendInteraction(string action)
    {
        if (grabbable == null)
        {
            return;
        }

        InteractionEvent data = new InteractionEvent
        {
            timestamp = DateTime.UtcNow.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ"
            ),
            object_name = grabbable.gameObject.name,
            action = action,
            position = grabbable.transform.position
        };

        string json = JsonUtility.ToJson(data);

        StartCoroutine(PostInteraction(json));
    }

    private IEnumerator PostInteraction(string json)
    {
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request =
            new UnityWebRequest(serverUrl, "POST"))
        {
            request.uploadHandler =
                new UploadHandlerRaw(body);

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result ==
                UnityWebRequest.Result.Success)
            {
                Debug.Log(
                    "Interaction sent successfully: " + json
                );
            }
            else
            {
                Debug.LogWarning(
                    "Failed to send interaction: " +
                    request.error
                );
            }
        }
    }
}
