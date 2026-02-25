using System;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceDebug : MonoBehaviour
{
    private KeywordRecognizer keywordRecognizer;

    void Start()
    {
        string[] keywords = new string[]
        {
            "test",
            "teleport",
            "unity",
            "hello"
        };

        keywordRecognizer = new KeywordRecognizer(keywords);

        keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;

        try
        {
            keywordRecognizer.Start();
            Debug.Log("Voice recognizer started successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start voice recognizer: " + e.Message);
        }
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log(
            $"Voice detected: '{args.text}' | Confidence: {args.confidence} | Time: {args.phraseDuration}"
        );
    }

    void OnDestroy()
    {
        if (keywordRecognizer != null)
        {
            if (keywordRecognizer.IsRunning)
                keywordRecognizer.Stop();

            keywordRecognizer.Dispose();
            Debug.Log("Voice recognizer stopped.");
        }
    }
}