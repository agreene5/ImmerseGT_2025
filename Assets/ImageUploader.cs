using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class ImageUploader : MonoBehaviour
{
    public Camera arCamera;
    public string serverUrl = "https://9469-38-101-220-234.ngrok-free.app";
    public float cameraFeedInterval = 1f; // More frequent updates for the web view

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

    void Start()
    {
        StartCoroutine(CaptureAndSendRoutine());
        StartCoroutine(SendCameraFeedRoutine()); // Additional routine for camera feed
    }

    // New routine to send camera images for web viewing
    IEnumerator SendCameraFeedRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(cameraFeedInterval);
            yield return StartCoroutine(SendCameraFeed());
        }
    }

    // New method to send camera feed to web viewer endpoint
    IEnumerator SendCameraFeed()
    {
        yield return new WaitForEndOfFrame();

        // Use main camera to capture what user sees
        Camera camera = Camera.main;
        int width = Screen.width;
        int height = Screen.height;

        RenderTexture rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;

        var currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        camera.Render();

        Texture2D screenshot = new Texture2D(width, height);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        camera.targetTexture = null;
        RenderTexture.active = currentRT;

        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "camera_feed.jpg", "image/jpeg");

        Destroy(rt);
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
        string ttsUrl = "https://9469-38-101-220-234.ngrok-free.app/tts";

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
                        Debug.Log("Failed to load audio clip: " + audioReq.error);
                    }
                }
            }
            else
            {
                Debug.Log("TTS POST failed: " + www.error);
            }
        }
    }

    IEnumerator CaptureAndSendRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f);
            yield return StartCoroutine(CaptureAndSendImage());
        }
    }

    IEnumerator CaptureAndSendImage()
    {
        // Wait until rendering is complete before taking the photo
        yield return new WaitForEndOfFrame();

        // Use main camera to capture what user sees
        Camera camera = Camera.main;
        int width = Screen.width;
        int height = Screen.height;

        // Create a new render texture the size of the screen
        RenderTexture rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;

        // The Render Texture in RenderTexture.active is the one
        // that will be read by ReadPixels
        var currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        // Render the camera's view
        camera.Render();

        // Make a new texture and read the active Render Texture into it
        Texture2D screenshot = new Texture2D(width, height);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();

        // Change back the camera target texture
        camera.targetTexture = null;

        // Replace the original active Render Texture
        RenderTexture.active = currentRT;

        // Convert to JPG for API upload
        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "frame.jpg", "image/jpeg");

        // Free up memory
        Destroy(rt);
        Destroy(screenshot);

        // Send to backend
        using (UnityWebRequest www = UnityWebRequest.Post("https://9469-38-101-220-234.ngrok-free.app/upload", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Server response: " + www.downloadHandler.text);

                // Parse detection result and summarize
                string json = www.downloadHandler.text;
                DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(json);

                string summary = "Detected: ";
                foreach (Detection d in response.detections)
                {
                    summary += d.label + " ";
                }

                Debug.Log("Scene Summary: " + summary);
                // Call TTS with summary
                StartCoroutine(SendSummaryToTTS(summary));
            }
            else
            {
                Debug.Log("Upload failed: " + www.error);
            }
        }
    }
}