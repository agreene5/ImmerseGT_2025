using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ImageUploader : MonoBehaviour
{
    public Camera arCamera;

    void Start()
    {
        StartCoroutine(CaptureAndSendRoutine());
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

        byte[] jpgBytes = screenshot.EncodeToJPG();
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpgBytes, "frame.jpg", "image/jpeg");

        using (UnityWebRequest www = UnityWebRequest.Post("http://127.0.0.1:8000/upload", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Server response: " + www.downloadHandler.text);
            }
            else
            {
                Debug.Log("Upload failed: " + www.error);
            }
        }
    }
}

