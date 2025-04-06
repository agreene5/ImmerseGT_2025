using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

public class ImageUploader : MonoBehaviour
{
    public ARCameraManager cameraManager;
    private AudioSource audioSource;
    public string backendUrl = "https://98e1-38-101-220-234.ngrok-free.app/process-frame";  // 👈 update this


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
        audioSource = GetComponent<AudioSource>();
        InvokeRepeating("TryCapture", 2f, 10f);  // every 10 seconds
    }

    void TryCapture()
    {
        if (cameraManager.TryAcquireLatestCpuImage(out UnityEngine.XR.ARSubsystems.XRCpuImage image))
        {
            StartCoroutine(ProcessImage(image));
            image.Dispose();
        }
        else
        {
            Debug.LogWarning("Failed to get AR camera image.");
        }
    }

    IEnumerator ProcessImage(UnityEngine.XR.ARSubsystems.XRCpuImage image)
    {
        var conversionParams = new UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),
            outputFormat = TextureFormat.RGB24,
            transformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation.None
        };

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        image.Convert(conversionParams, buffer);

        Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGB24, false);
        texture.LoadRawTextureData(buffer);
        texture.Apply();
        buffer.Dispose();

        byte[] imageBytes = texture.EncodeToJPG();
        Destroy(texture);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", imageBytes, "frame.jpg", "image/jpeg");

        using (UnityWebRequest www = UnityWebRequest.Post(backendUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Detection response: " + www.downloadHandler.text);

                string json = www.downloadHandler.text;
                DetectionResponse response = JsonUtility.FromJson<DetectionResponse>(json);

                string summary = "Detected: ";
                foreach (Detection d in response.detections)
                {
                    summary += d.label + " ";
                }

                Debug.Log("Scene Summary: " + summary);
                StartCoroutine(SendSummaryToTTS(summary));
            }
            else
            {
                Debug.LogError("Upload failed: " + www.error);
            }
        }
    }

    IEnumerator SendSummaryToTTS(string summary)
    {
        string ttsUrl = "https://98e1-38-101-220-234.ngrok-free.app/tts"; // 👈 update this to your TTS endpoint
        

        WWWForm form = new WWWForm();
        form.AddField("text", summary);

        using (UnityWebRequest www = UnityWebRequest.Post(ttsUrl, form))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                byte[] audioData = www.downloadHandler.data;
                string audioPath = Path.Combine(Application.persistentDataPath, "response.mp3");
                File.WriteAllBytes(audioPath, audioData);

                using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, AudioType.MPEG))
                {
                    yield return audioReq.SendWebRequest();

                    if (audioReq.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
                        audioSource.PlayOneShot(clip);
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
}
