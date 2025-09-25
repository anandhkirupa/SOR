using UnityEngine;
using TMPro;
using Oculus.Voice.Dictation;
using System.Diagnostics;
using System.IO;

public class DictationUI : MonoBehaviour
{
    [Header("References")]
    public AppDictationExperience dictationExperience;
    public TMP_Text transcriptText;
    public RunJets jetsSpeaker;

    // timers
    private Stopwatch sttTimer = new Stopwatch();
    private Stopwatch ttsTimer = new Stopwatch();
    private Stopwatch roundTripTimer = new Stopwatch();

    private string lastTranscript = "";
    private string logFilePath;

    [System.Serializable]
    public class SpeechToTextJson
    {
        public string[] modality;       // optional, allow multiple modalities
        public string text;             // required
        public int[] photo_id;          // optional, allow multiple IDs
        public int[] block_ids;         // optional, allow multiple IDs
        public string lesson_id;        // required
        public string teacher_id;       // required
    }

    [System.Serializable]
    public class RootJson
    {
        public string lesson_id;
        public string teacher_id;
        public ResponseJson response;
    }

    [System.Serializable]
    public class ResponseJson
    {
        public string text;
        public float confidence;
    }

    void Start()
    {
        dictationExperience.DictationEvents.OnPartialTranscription.AddListener(OnPartial);
        dictationExperience.DictationEvents.OnFullTranscription.AddListener(OnFull);
        dictationExperience.DictationEvents.OnError.AddListener(OnError);

        // prepare log file
        logFilePath = Path.Combine(Application.persistentDataPath, "LatencyLog.txt");
        File.AppendAllText(logFilePath, "\n=== Session Started " + System.DateTime.Now + " ===\n");
    }

    public void StartDictation()
    {
        transcriptText.text = "Listening...";
        sttTimer.Restart();
        roundTripTimer.Restart();
        dictationExperience.Activate();
    }

    private void OnPartial(string text) => transcriptText.text = text;

    private void OnFull(string text)
    {
        sttTimer.Stop();
        transcriptText.text = $"{text}\n(STT Latency: {sttTimer.ElapsedMilliseconds} ms)";
        lastTranscript = text;  // save the result
        SaveTranscriptAsJson(); // parse to  JSON

        // log STT latency
        File.AppendAllText(logFilePath, System.DateTime.Now + $": STT={sttTimer.ElapsedMilliseconds}ms\n");
    }

    private void OnError(string error, string message)
    {
        transcriptText.text = $"Error: {error}\n{message}";
    }

    // Hook this to another button: "Speak Back"
    public void PlayBackTranscript()
    {
        string transcript = GetTranscriptTextFromJson();
        if (!string.IsNullOrEmpty(transcript))
        {
            ttsTimer.Restart();
            roundTripTimer.Restart();
            jetsSpeaker.SpeakPhonemes(transcript);
        }
        else
        {
            transcriptText.text = "No transcript yet!";
        }
    }

    public void SaveTranscriptAsJson()
    {
        var data = new SpeechToTextJson
        {
            text = lastTranscript,
            modality = new string[] { "Speech" },
            photo_id = new int[] { },
            block_ids = new int[] { },
            lesson_id = "lesson-123",
            teacher_id = "teacher-456"
        };
        string folderPath = Path.Combine(Application.persistentDataPath, "JSON");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);  
        }
        string savePath = Path.Combine(folderPath, "1_STT.json"); 
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);
        UnityEngine.Debug.Log("Transcript saved as JSON: " + savePath);
    }

    public string GetTranscriptTextFromJson()
    {
        string folderPath = Path.Combine(Application.persistentDataPath, "JSON");
        string jsonPath = Path.Combine(folderPath, "1_TTS.json");
        if (File.Exists(jsonPath))
        {
            string jsonStr = File.ReadAllText(jsonPath);
            var data = JsonUtility.FromJson<RootJson>(jsonStr);
            return data.response?.text;
        }
        return null;
    }
    public void OnJetsTTSStarted(string spokenText)
    {
        ttsTimer.Stop();
        roundTripTimer.Stop();

        string logLine = $"TTS={ttsTimer.ElapsedMilliseconds}ms, Manual RoundTrip={roundTripTimer.ElapsedMilliseconds}ms";

        transcriptText.text += $"\n{logLine}";
        File.AppendAllText(logFilePath, System.DateTime.Now + ": " + logLine + "\n");
    }
}