using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


/// <summary>
/// By default, this script will attempt to send a test prompt to the LM Studio on Start().
/// It will log the processed response in the Unity console.
/// Spin up the LM Studio server before running the game.
/// </summary>
public class LMStudioClient : MonoBehaviour
{

    // Replace with your LM Studio endpoint
    [SerializeField]
    private string url = "http://localhost:1234/v1/chat/completions";

    [SerializeField]
    private string model = "";

    [SerializeField]
    private int maxTokens = 128;

    [SerializeField]
    [TextArea(15, 20)]
    private string systemPrompt = "Always answer in rhymes.";

    [SerializeField]
    [TextArea(15, 20)]
    private string userPrompt = "Introduce yourself.";

    [SerializeField]
    private bool runOnStart = true;

    /// <summary>
    /// Resets every time SendRequest is called.
    /// </summary>
    private StringBuilder output = new StringBuilder("");

    void Start()
    {
        StartCoroutine(SendRequest(() => HandleOutput())); //Comment out this line if you don't want this to happen immediately.
    }

    /// <summary>
    /// Replace with your own output handling.
    /// </summary>
    void HandleOutput()
    {
        Debug.Log(output.ToString());
    }

    /// <summary>
    /// Sends a request to the LM and invokes a callback if successful.
    /// </summary>
    /// <returns></returns>
    IEnumerator SendRequest(Action onComplete)
    {
	if (string.IsNullOrWhiteSpace (url)) {
            Debug.LogError("Please input a URL into the inspector; i.e.: \"http://localhost:1234/v1/chat/completions\"");
            yield break;
        }
	else if (string.IsNullOrWhiteSpace (model)) {
            Debug.LogError("Please input a model into the inspector; i.e.: \"path/to_model.gguf\"");
            yield break;
        }

        output.Clear();

        string jsonData = $@"
        {{
            ""model"": ""{model}"",
            ""messages"": [
                {{ ""role"": ""system"", ""content"": ""{systemPrompt}"" }},
                {{ ""role"": ""user"", ""content"": ""{userPrompt}"" }}
            ],
            ""temperature"": 0.7,
            ""max_tokens"": {maxTokens},
            ""stream"": true
        }}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("Sending request...");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {

            string rawResponse = request.downloadHandler.text;

            // Process each `data:` line
            string[] dataLines = rawResponse.Split(new[] { "data: " }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string dataLine in dataLines)
            {
                try
                {
                    // Trim and ignore the `data: [DONE]` case (end-of-output)
                    string trimmedLine = dataLine.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine == "[DONE]") continue;

                    // Parse the JSON object
                    var parsedJson = JsonUtility.FromJson<StreamedChunk>(trimmedLine);
                    if (parsedJson != null && parsedJson.choices != null)
                    {
                        foreach (var choice in parsedJson.choices)
                        {
                            if (choice.delta != null && !string.IsNullOrEmpty(choice.delta.content))
                            {
                                output.Append(choice.delta.content);
                            }
                        }
                        
                    }
                    
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("Failed to parse line: " + dataLine + " Error: " + ex.Message);
                }
            }
            onComplete.Invoke();
        }
        else
        {
            Debug.LogError($"Error: {request.responseCode} - {request.error}");
        }
    }

}

// Classes to match the streamed JSON structure
[System.Serializable]
public class Delta
{
    public string role;
    public string content;
}

[System.Serializable]
public class Choice
{
    public int index;
    public Delta delta;
    public string finish_reason;
}

[System.Serializable]
public class StreamedChunk
{
    public string id;
    public string @object;
    public long created;
    public string model;
    public List<Choice> choices;
}