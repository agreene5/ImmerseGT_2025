using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Text.RegularExpressions;

public class RedditWordFetcher : MonoBehaviour
{
    public TextMeshProUGUI textDisplay;
    private string apiUrl = "https://www.reddit.com/r/javascript.json";

    void Start()
    {
        if (textDisplay == null)
        {
            Debug.LogError("Text Display reference is not set in the Inspector!");
            return;
        }

        StartCoroutine(FetchRedditWords());
    }

    IEnumerator FetchRedditWords()
    {
        textDisplay.text = "Connecting to Reddit API...";

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            // Add a user agent to avoid Reddit API blocking
            request.SetRequestHeader("User-Agent", "Unity Reddit Word Fetcher 1.0");

            // Send request
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                textDisplay.text = "Can not connect to API";
                Debug.LogError("Error fetching Reddit data: " + request.error);
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;

                // Extract words from the JSON response
                List<string> words = ExtractWords(jsonResponse);

                if (words.Count >= 20)
                {
                    // Join the first 20 words with spaces
                    string firstTwentyWords = string.Join(" ", words.GetRange(0, 20));
                    textDisplay.text = firstTwentyWords;
                }
                else if (words.Count > 0)
                {
                    // If less than 20 words were found, display all of them
                    string allWords = string.Join(" ", words);
                    textDisplay.text = allWords;
                }
                else
                {
                    textDisplay.text = "No words found in the JSON response";
                }
            }
        }
    }

    private List<string> ExtractWords(string json)
    {
        // Remove special JSON characters and split by whitespace
        string cleanedText = Regex.Replace(json, "[\\{\\}\\[\\]\\,\\:\\\"\\\\]", " ");
        string[] allWords = cleanedText.Split(new char[] { ' ', '\t', '\n', '\r' },
                                             StringSplitOptions.RemoveEmptyEntries);

        List<string> result = new List<string>();

        foreach (string word in allWords)
        {
            // Skip common JSON terms and numbers
            if (!string.IsNullOrWhiteSpace(word) &&
                !word.Equals("null") &&
                !word.Equals("true") &&
                !word.Equals("false") &&
                !Regex.IsMatch(word, "^[0-9]+$"))
            {
                result.Add(word);

                // Stop once we have 20 words
                if (result.Count >= 20)
                    break;
            }
        }

        return result;
    }
}