using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Mochi;

public sealed class Settings
{
    public string PrimaryCompanion { get; set; } = "mochi";
    public string SecondaryCompanion { get; set; } = "azki";
    public string Personality { get; set; } = "";
    public string ReplyLength { get; set; } = "Normal";
    public string CharacterName { get; set; } = "Mochi";
    public string GoogleKey { get; set; } = "";
    public string FishKey { get; set; } = "";
    public string GoogleFallbackKey { get; set; } = "";
    public string FishFallbackKey { get; set; } = "";
    public bool EnableFallback { get; set; }
    public string Mode { get; set; } = "Two characters";
    public string GoogleModel { get; set; } = "gemini-3.1-flash-lite";
    public string FishModel { get; set; } = "s2.1-pro-free";
    public string Voice { get; set; } = "4f61a043daea4c3ab0d9070dfbd69f3c";
    public bool Speak { get; set; } = true;
    public string OutputLanguage { get; set; } = "Auto (match input)";
    public string VoiceStyle { get; set; } = "Auto (match emotion)";
    public bool OnTop { get; set; }
    public double Height { get; set; } = 510;
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MochiDuo");
    public static string LoadWarning = "";
    public static Settings Load()
    {
        try {
            var file = Path.Combine(Folder, "settings.bin");
            if (!File.Exists(file)) file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MochiCompanion", "settings.bin");
            if (!File.Exists(file)) return new();
            return JsonSerializer.Deserialize<Settings>(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser)) ?? new();
        } catch { LoadWarning = "Saved settings could not be read. Please re-enter your keys."; return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.bin");
        File.WriteAllBytes(path + ".tmp", ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(this), null, DataProtectionScope.CurrentUser));
        File.Move(path + ".tmp", path, true);
    }
}

public record Reply(string Text, string Emotion, string Transcript, string VoiceEmotion = "");
public sealed class AiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly List<object> history = new();
    public AiClient(HttpMessageHandler? handler = null) { http = handler == null ? new() : new(handler); http.Timeout = TimeSpan.FromSeconds(65); }
    public void Clear() => history.Clear();
    public event Action<string>? Notice;
    public static string Persona(Settings s) => s.Personality.Length > 0 ? $"You are {s.CharacterName}, a fictional desktop companion. Personality: {s.Personality}. " + (s.Mode == "Two characters" ? "Write ONLY your next spoken turn responding to the other companion. Do not address the human audience or include speaker labels. " : "Chat directly with the user. Do not invent another speaker. ") + (s.ReplyLength == "Short" ? "Use one brief sentence. " : s.ReplyLength == "Long" ? "Use 4-6 natural sentences. " : "Use 1-3 sentences. ") : s.Mode == "Two characters"
        ? "You are writing ONLY the next spoken turn of " + s.CharacterName + " in a fictional two-character conversation. Mochi is playful, curious and energetic. AZKi is thoughtful, warm and gently witty. Respond directly to the other character, develop the given topic, avoid repeating their point, and do not address the human audience. Never write both speakers or include speaker labels in text. Use 1-2 short sentences. "
        : "You are " + s.CharacterName + ", a friendly desktop companion chatting directly with the user. Mochi is playful and curious; AZKi is thoughtful and gently witty. Answer the user's message naturally in 1-3 sentences. Do not invent another speaker. ";
    async Task<HttpResponseMessage> SendWithFallback(Func<string, HttpRequestMessage> create, string primary, string fallback, bool enabled, string provider, CancellationToken ct)
    {
        using var request = create(primary);
        var response = await http.SendAsync(request, ct);
        int code = (int)response.StatusCode;
        if (enabled && !string.IsNullOrWhiteSpace(fallback) && fallback != primary && (code is 401 or 403 or 429 || code >= 500)) {
            response.Dispose(); ct.ThrowIfCancellationRequested(); Notice?.Invoke(provider + ": retrying once with your fallback key.");
            using var retry = create(fallback); return await http.SendAsync(retry, ct);
        }
        return response;
    }
    public static readonly string[] Languages = { "Auto (match input)", "Japanese", "English", "Chinese", "Korean", "Malay", "Spanish", "French", "German", "Italian", "Portuguese", "Russian", "Arabic" };
    public static readonly string[] VoiceEmotions = { "happy", "sad", "angry", "excited", "calm", "confident", "surprised", "empathetic", "embarrassed", "grateful", "curious", "hopeful", "relaxed", "playfully annoyed", "soft tone", "whispering" };
    public static string LanguageInstruction(string language) => Array.IndexOf(Languages, language) > 0
        ? $"Always write the reply text in {language}, regardless of the input language (typed or spoken) or the language of earlier conversation. Use its native writing system. Do not add an English translation or romanization unless explicitly requested."
        : "Reply in the language of the user-supplied topic. Ignore the language of the surrounding conversation-directing instructions when choosing the reply language.";
    public static string SpeechText(Settings s, Reply reply)
    {
        var cue = Array.IndexOf(VoiceEmotions, s.VoiceStyle) >= 0 ? s.VoiceStyle : reply.VoiceEmotion;
        var tag = Array.IndexOf(VoiceEmotions, cue) >= 0 ? "[" + cue + "]" : Tag(reply.Emotion);
        return tag + " " + reply.Text;
    }
    public static string Tag(string emotion) => emotion switch { "sad" => "[sad]", "puffyface" => "[playfully annoyed]", _ => "[happy]" };
    public static Reply Parse(string json)
    {
        using var d = JsonDocument.Parse(json);
        var r = d.RootElement;
        var text = r.GetProperty("text").GetString()?.Trim() ?? "";
        if (text.Length == 0) throw new InvalidOperationException("Gemini returned an empty reply. Please try again.");
        text = Regex.Replace(text, @"\[[^\]]*\]", "").Trim();
        var emotion = r.TryGetProperty("emotion", out var e) ? e.GetString() : "happy";
        var cue = r.TryGetProperty("voiceEmotion", out var v) ? v.GetString() ?? "" : "";
        if (Array.IndexOf(VoiceEmotions, cue) < 0) cue = "";
        return new(text[..Math.Min(text.Length, 1800)], emotion is "sad" or "puffyface" ? emotion : "happy", r.TryGetProperty("transcript", out var t) ? t.GetString() ?? "" : "", cue);
    }
    public async Task<Reply> Chat(Settings s, string text, byte[]? wav, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.GoogleKey)) throw new InvalidOperationException("Add your Google AI Studio API key in Settings first.");
        if (!Regex.IsMatch(s.GoogleModel, @"^[a-zA-Z0-9.\-]+$")) throw new InvalidOperationException("Invalid Gemini model name.");
        var parts = new List<object> { new { text = wav == null ? text : "Listen to my voice message and respond. Include what I said in transcript." } };
        if (wav != null) parts.Add(new { inlineData = new { mimeType = "audio/wav", data = Convert.ToBase64String(wav) } });
        var contents = new List<object>(history) { new { role = "user", parts } };
        var body = new {
            systemInstruction = new { parts = new[] { new { text = Persona(s) + (s.Mode != "Two characters" && s.OutputLanguage == Languages[0] ? "Reply in the language of the user's latest message." : LanguageInstruction(s.OutputLanguage)) + " Choose happy, sad (empathetic), or puffyface (playful mock annoyance) for the portrait. Choose one voiceEmotion matching the reply's emotional tone from the schema; it controls Fish Audio speech independently of the portrait. Do not include bracket tags in text. For audio input transcribe the user verbatim in their original language in transcript, without translating; for text input transcript is empty. You cannot control the computer. Return JSON only." } } },
            contents,
            generationConfig = new { responseMimeType = "application/json", responseSchema = new { type = "OBJECT", properties = new { text = new { type = "STRING" }, emotion = new { type = "STRING", @enum = new[] { "happy", "sad", "puffyface" } }, voiceEmotion = new { type = "STRING", @enum = VoiceEmotions }, transcript = new { type = "STRING" } }, required = new[] { "text", "emotion", "voiceEmotion", "transcript" } }, maxOutputTokens = 4096 }
        };
        using var response = await SendWithFallback(key => {
            var req = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{s.GoogleModel}:generateContent");
            req.Headers.Add("x-goog-api-key", key);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"); return req;
        }, s.GoogleKey, s.GoogleFallbackKey, s.EnableFallback, "Gemini", ct);
        await Ensure(response, "Gemini", ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0 || !candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var resultParts))
            throw new InvalidOperationException("Gemini returned no reply (possibly filtered). Try rephrasing.");
        var json = new StringBuilder();
        foreach (var part in resultParts.EnumerateArray()) if (part.TryGetProperty("text", out var p) && !(part.TryGetProperty("thought", out var thought) && thought.GetBoolean())) json.Append(p.GetString());
        var reply = Parse(json.ToString());
        history.Add(new { role = "user", parts = new[] { new { text = wav == null ? text : reply.Transcript.Length > 0 ? reply.Transcript : "[Voice message]" } } });
        history.Add(new { role = "model", parts = new[] { new { text = JsonSerializer.Serialize(new { text = reply.Text, emotion = reply.Emotion, transcript = "" }) } } });
        if (history.Count > 20) history.RemoveRange(0, history.Count - 20);
        return reply;
    }
    public async Task<byte[]> Speech(Settings s, Reply reply, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.FishKey)) throw new InvalidOperationException("Reply ready. Add a Fish Audio API key in Settings to hear her voice.");
        if (s.FishModel is not ("s2-pro" or "s2.1-pro-free")) throw new InvalidOperationException("Choose a supported Fish model in Settings.");
        using var response = await SendWithFallback(key => {
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.fish.audio/v1/tts");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Headers.Add("model", s.FishModel);
            req.Content = new StringContent(JsonSerializer.Serialize(new { text = SpeechText(s, reply), reference_id = s.Voice, format = "mp3" }), Encoding.UTF8, "application/json"); return req;
        }, s.FishKey, s.FishFallbackKey, s.EnableFallback, "Fish Audio", ct);
        await Ensure(response, "Fish Audio", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
    private static Task Ensure(HttpResponseMessage r, string provider, CancellationToken ct)
    {
        if (!r.IsSuccessStatusCode) throw new InvalidOperationException($"{provider}: " + ((int)r.StatusCode switch {
            401 or 403 => "API key rejected or access denied. Check your key and model permissions.",
            402 => "No API credits available. Check your account; no paid fallback was attempted.",
            429 => "Free quota or rate limit reached. Wait and try again.",
            404 => provider == "Gemini" ? "The selected Gemini model is unavailable for this key. In Settings, use gemini-3.1-flash-lite and save." : "The selected Fish model or voice is unavailable for this account. Check Settings.",
            _ => $"Request failed (HTTP {(int)r.StatusCode}). Check model availability and try again."
        }));
        return Task.CompletedTask;
    }
    public void Dispose() => http.Dispose();
}

public sealed class Recorder : IDisposable
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern int mciSendString(string command, StringBuilder? output, int length, IntPtr callback);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern bool mciGetErrorString(int error, StringBuilder output, int length);
    private bool active;
    private static void Run(string command) { int code = mciSendString(command, null, 0, IntPtr.Zero); if (code != 0) { var b = new StringBuilder(256); mciGetErrorString(code, b, b.Capacity); throw new InvalidOperationException("Microphone: " + b); } }
    public void Start()
    {
        Dispose();
        Run("open new type waveaudio alias mochiMic"); active = true;
        try { Run("set mochiMic time format milliseconds bitspersample 16 channels 1 samplespersec 16000 bytespersec 32000 alignment 2"); Run("record mochiMic"); } catch { Dispose(); throw; }
    }
    public byte[] Stop()
    {
        var path = Path.Combine(Path.GetTempPath(), "mochi-mic-" + Guid.NewGuid() + ".wav");
        try { Run("stop mochiMic"); Run($"save mochiMic \"{path}\""); return File.ReadAllBytes(path); }
        finally { Dispose(); if (File.Exists(path)) File.Delete(path); }
    }
    public void Dispose() { if (active) mciSendString("close mochiMic", null, 0, IntPtr.Zero); active = false; }
}
