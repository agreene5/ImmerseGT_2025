using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using PassthroughCameraSamples;
using Debug = UnityEngine.Debug;

public class ImageUploader : MonoBehaviour
{
    [Header("Server Settings")]
    public string serverUrl = "https://3d35-38-101-220-234.ngrok-free.app";
    public float cameraFeedInterval = 1f;

    [Header("Passthrough Settings")]
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private RawImage previewImage; // Optional: for debugging/preview

    [Header("Fallback Settings")]
    public Camera arCamera; // Keep as fallback for non-Quest devices

    [System.Serializable]
    public class Detection
    {
        public string label;
        public float confidence;
        public float[] bbox;
    }

    [System.Serializable]
    public class DetectionResponse
    {
        public Detection[] detections;
    }

    private bool usePassthroughCamera = false;

    void Awake()
    {
        // Check if we have a WebCamTextureManager
        if (webCamTextureManager == null)
        {
            webCamTextureManager = GetComponentInChildren<WebCamTextureManager>();
        }

        // Check if passthrough is supported
        if (webCamTextureManager != null && PassthroughCameraUtils.IsSupported)
        {
            usePassthroughCamera = true;
            Debug.Log("Using Quest 3 passthrough camera");
        }
        else
        {
            Debug.Log("Passthrough camera not available, using fallback camera");
        }
    }

    void Start()
    {
        StartCoroutine(CaptureAndSendRoutine());
        StartCoroutine(SendCameraFeedRoutine());
    }

    IEnumerator SendCameraFeedRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(cameraFeedInterval);
            yield return StartCoroutine(SendCameraFeed());
        }
    }

    IEnumerator CaptureAndSendRoutine()
    {
        // Your existing capture routine can also use SendCameraFeed
        // or have its own logic if different from the feed
        yield return new WaitForSeconds(5f);
        while (true)
        {
            yield return StartCoroutine(SendCameraFeed());
            yield return new WaitForSeconds(5f);
        }
    }

    IEnumerator SendCameraFeed()
    {
        if (usePassthroughCamera)
        {
            yield return StartCoroutine(SendPassthroughCameraFeed());
        }
        else
        {
            yield return StartCoroutine(SendRegularCameraFeed());
        }
    }

    IEnumerator SendPassthroughCameraFeed()
    {
        // Wait until WebCamTexture is initialized and playing
        while (webCamTextureManager.WebCamTexture == null)
        {
            Debug.Log("Waiting for WebCamTexture to initialize...");
            yield return new WaitForSeconds(0.5f);
        }

        // Wait a few frames to ensure the texture has valid data
        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }

        // Optional: show in preview image
        if (previewImage != null)
        {
            previewImage.texture = webCamTextureManager.WebCamTexture;
        }

        // Create a Texture2D from the WebCamTexture
        Texture2D screenshot = new Texture2D(
            webCamTextureManager.WebCamTexture.width,
            webCamTextureManager.WebCamTexture.height,
            TextureFormat.RGB24, false);

        // Get pixels from the WebCamTexture
        screenshot.SetPixels(webCamTextureManager.WebCamTexture.GetPixels());
        screenshot.Apply();

        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "camera_feed.jpg", "image/jpeg");

        // Free up memory
        Destroy(screenshot);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/camera_feed", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Camera feed upload failed: " + www.error);
            }
        }
    }

    IEnumerator SendRegularCameraFeed()
    {
        yield return new WaitForEndOfFrame();

        // Your existing camera capture code
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        arCamera.targetTexture = renderTexture;
        arCamera.Render();

        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();

        arCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "camera_feed.jpg", "image/jpeg");

        Destroy(screenshot);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl + "/camera_feed", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Camera feed upload failed: " + www.error);
            }
        }
    }

    IEnumerator SendSummaryToTTS(string summary)
    {
        string ttsUrl = serverUrl + "/tts";

        WWWForm form = new WWWForm();
        form.AddField("text", summary);

        using (UnityWebRequest www = UnityWebRequest.Post(ttsUrl, form))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                byte[] audioData = www.downloadHandler.data;

                // Convert to AudioClip
                string audioPath = Path.Combine(Application.persistentDataPath, "response.mp3");
                File.WriteAllBytes(audioPath, audioData);

                using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, AudioType.MPEG))
                {
                    yield return audioReq.SendWebRequest();

                    if (audioReq.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
                        AudioSource audioSource = GetComponent<AudioSource>();
                        audioSource.clip = clip;
                        audioSource.Play();
                    }
                    else
                    {
                        Debug.LogError("Error loading audio: " + audioReq.error);
                    }
                }
            }
            else
            {
                Debug.LogError("TTS request failed: " + www.error);
            }
        }
    }
}