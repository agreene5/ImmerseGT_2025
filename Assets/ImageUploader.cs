using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;


public class ImageUploader : MonoBehaviour
{
    public Camera arCamera;

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

    }

    //LIKE THIS?:
    IEnumerator SendSummaryToTTS(string summary)
    {
        string ttsUrl = "https://98e1-38-101-220-234.ngrok-free.app/tts"; // Replace with your actual local IP!

        WWWForm form = new WWWForm();
        form.AddField("text", summary);

        using (UnityWebRequest www = UnityWebRequest.Post(ttsUrl, form))
        {
            www.downloadHandler = new DownloadHandlerBuffer(); // We want to download the .mp3 file
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
        // Capture screenshot from ARCamera
        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        arCamera.targetTexture = rt;
        Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        arCamera.Render();
        RenderTexture.active = rt;
        screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenshot.Apply();
        arCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        // Convert to JPG
        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "frame.jpg", "image/jpeg");

        // Send to backend
        using (UnityWebRequest www = UnityWebRequest.Post("https://98e1-38-101-220-234.ngrok-free.app/upload", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Server response: " + www.downloadHandler.text);

                // Step 2: Parse detection result and summarize
                string json = www.downloadHandler.text;
                DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(json);

                string summary = "Detected: ";
                foreach (Detection d in response.detections)
                {
                    summary += d.label + " ";
                }

                Debug.Log("Scene Summary: " + summary);
                // ✅ NOW call TTS from here
                StartCoroutine(SendSummaryToTTS(summary));
            }
            else
            {
                Debug.Log("Upload failed: " + www.error);
            }
        }
    }
}



