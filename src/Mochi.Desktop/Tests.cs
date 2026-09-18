using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mochi;

namespace MochiDuo;
public static class Tests
{
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    public static async Task Run(bool live)
    {
        string path = Path.Combine(AppContext.BaseDirectory, live ? "live-test.txt" : "test-results.txt");
        try {
            if (live) {
                var config = Settings.Load(); config.OutputLanguage = "Japanese"; config.FishModel = "s2.1-pro-free";
                using var ai = new AiClient(); var history = new List<string>();
                for (int turn = 0; turn < 2; turn++) {
                    ai.Clear(); config.CharacterName = Dialogue.Speaker(turn); config.Voice = Dialogue.Voice(turn);
                    var reply = await ai.Chat(config, Dialogue.Prompt("A relaxing weekend", history, turn, 2), null, CancellationToken.None);
                    Check(System.Text.RegularExpressions.Regex.IsMatch(reply.Text, @"[\u3040-\u30ff]"), "Japanese output"); history.Add(config.CharacterName + ": " + reply.Text);
                    var audio = await ai.Speech(config, reply, CancellationToken.None); Check(audio.Length > 1000, "Audio returned for " + config.CharacterName);
                }
                File.WriteAllText(path, "PASS: two live Japanese dialogue turns and Fish audio returned for BOTH exact voice IDs using s2.1-pro-free. Audio playback quality was not evaluated."); return;
            }
            foreach (var folder in new[] { "mochi", "azki" }) foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, folder), "*.png")) {
                var bitmap = new BitmapImage(new Uri(file)); var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
                Check(Enumerable.Range(0, bytes.Length / 4).Any(i => bytes[i * 4 + 3] == 0), "transparent pixels: " + file);
                Check(Enumerable.Range(0, bytes.Length / 4).Any(i => bytes[i * 4 + 3] > 200), "visible artwork: " + file);
            }
            var libraryPath = Path.Combine(Path.GetTempPath(), "mochi-library-test-" + Guid.NewGuid().ToString("N"));
            try {
                var library = new CompanionLibrary(libraryPath);
                var custom = library.Save("Yuki", "Quiet and loves astronomy", Dialogue.AzkiVoice, new() { ["happy"] = Path.Combine(AppContext.BaseDirectory, "azki", "normal.png") });
                Check(custom.ImagePath("sad") == custom.ImagePath("happy"), "missing expressions fall back to main image");
                Check(custom.ImagePath("happy").StartsWith(libraryPath), "artwork imported to durable library storage");
                var reloaded = new CompanionLibrary(libraryPath);
                Check(reloaded.Find(custom.Id).Name == "Yuki" && reloaded.Find(custom.Id).Voice == Dialogue.AzkiVoice, "custom companion survives restart");
                var updated = reloaded.Save("Yuki edited", custom.Personality, "", custom.Images, custom.Id);
                Check(new CompanionLibrary(libraryPath).Items.Count == 3 && updated.Voice == "", "editing replaces profile and allows text-only voice");
                bool rejected = false; try { reloaded.Save("Bad", "", "invalid", custom.Images); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "invalid voice ID rejected");
                var persona = AiClient.Persona(new Settings { CharacterName = "Yuki", Personality = custom.Personality, ReplyLength = "Long", Mode = "One character" });
                Check(persona.Contains("Yuki") && persona.Contains("astronomy") && persona.Contains("4-6"), "custom personality and reply length reach prompt");
            } finally { if (Directory.Exists(libraryPath)) Directory.Delete(libraryPath, true); }
            Check(Settings.Folder.EndsWith("MochiDuo"), "settings isolated");
            Check(Dialogue.Sprite(true, "angry") == "angry" && Dialogue.Sprite(true, "sad") == "normal", "AZKi expressions");
            Check(Dialogue.Sprite(false, "angry") == "puffyface", "Mochi expression fallback");
            Check(Dialogue.TurnCount("Shorter · 4 turns") == 4 && Dialogue.TurnCount("Longer · 12 turns") == 12 && Dialogue.TurnCount("Extended · 20 turns") == 20, "length presets");
            Check(Dialogue.IsUltimate("Ultimate · until Stop") && Dialogue.Prompt("topic", new List<string>(), 3, -1).Contains("ongoing"), "ultimate length prompt");
            var handler = new Fake(); using var client = new AiClient(handler); var settings = new Settings { GoogleKey = "fake", FishKey = "fake", OutputLanguage = "Japanese" }; var lines = new List<string>();
            for (int turn = 0; turn < 4; turn++) {
                client.Clear(); settings.CharacterName = Dialogue.Speaker(turn); settings.Voice = Dialogue.Voice(turn);
                var prompt = Dialogue.Prompt("Cats or dogs?", lines, turn, 4);
                if (turn > 0) Check(prompt.Contains(lines[turn - 1]), "previous turn included");
                var reply = await client.Chat(settings, prompt, null, CancellationToken.None);
                Check(handler.Body.Contains("next spoken turn of " + Dialogue.Speaker(turn)), "correct speaker system prompt");
                Check(handler.Body.Contains("Always write the reply text in Japanese"), "language preserved");
                lines.Add(Dialogue.Speaker(turn) + ": " + reply.Text); await client.Speech(settings, reply, CancellationToken.None);
                using var body = JsonDocument.Parse(handler.Body); Check(body.RootElement.GetProperty("reference_id").GetString() == (turn % 2 == 0 ? "4f61a043daea4c3ab0d9070dfbd69f3c" : "5f8f82504223455f906c53e6d3e6b8cd"), "correct Fish voice"); Check(body.RootElement.GetProperty("text").GetString()!.StartsWith("[curious]"), "expressive speech");
            }
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await client.Chat(settings, "test", null, cancelled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { checks++; }
            settings.Mode = "AZKi only"; settings.CharacterName = "AZKi";
            await client.Chat(settings, "hello", null, CancellationToken.None);
            Check(handler.Body.Contains("chatting directly with the user") && !handler.Body.Contains("do not address the human audience"), "solo prompt addresses user");
            foreach (var code in new[] { 401, 403, 429, 500, 503, 400, 402, 404 }) {
                var retryHandler = new RetryFake(code); using var retryClient = new AiClient(retryHandler);
                var retrySettings = new Settings { GoogleKey = "primary-google", GoogleFallbackKey = "backup-google", FishKey = "primary-fish", FishFallbackKey = "backup-fish", EnableFallback = true };
                bool eligible = code is 401 or 403 or 429 || code >= 500;
                try { await retryClient.Chat(retrySettings, "hello", null, CancellationToken.None); Check(eligible, "unexpected retry success"); } catch (InvalidOperationException) { Check(!eligible, "expected fallback success"); }
                Check(retryHandler.Keys.Count == (eligible ? 2 : 1), "bounded Google retry " + code);
                if (eligible) Check(retryHandler.Keys[1] == "backup-google", "correct Google backup key");
                retryHandler.Keys.Clear();
                try { await retryClient.Speech(retrySettings, new Reply("Hello", "happy", ""), CancellationToken.None); Check(eligible, "unexpected Fish retry"); } catch (InvalidOperationException) { Check(!eligible, "expected Fish fallback success"); }
                Check(retryHandler.Keys.Count == (eligible ? 2 : 1), "bounded Fish retry " + code);
                if (eligible) Check(retryHandler.Keys[1] == "backup-fish", "correct Fish backup key");
            }
            foreach (var enabled in new[] { false, true }) {
                var retryHandler = new RetryFake(429); using var retryClient = new AiClient(retryHandler);
                var retrySettings = new Settings { GoogleKey = "same", GoogleFallbackKey = enabled ? "same" : "different", EnableFallback = enabled };
                try { await retryClient.Chat(retrySettings, "hello", null, CancellationToken.None); } catch (InvalidOperationException) { }
                Check(retryHandler.Keys.Count == 1, "disabled or identical fallback never retries");
            }
            string archivePath = Path.Combine(Path.GetTempPath(), "mochi-history-test-" + Guid.NewGuid() + ".bin");
            try { var archive = new ChatArchive(archivePath); archive.Add("AZKi", "こんにちは"); var loaded = new ChatArchive(archivePath); Check(loaded.Lines.Count == 1 && loaded.Lines[0].Text == "こんにちは", "encrypted history roundtrip"); loaded.Clear(); Check(new ChatArchive(archivePath).Lines.Count == 0, "clear history persisted"); } finally { if (File.Exists(archivePath)) File.Delete(archivePath); }
            File.WriteAllText(path, $"PASS: {checks} checks for six image alpha channels, alternating speakers, topic/history, language, exact voices, expression tags, isolated settings and request cancellation.");
        } catch (Exception e) { File.WriteAllText(path, "FAIL: " + e.Message); Environment.ExitCode = 1; }
    }
    sealed class Fake : HttpMessageHandler
    {
        public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            ct.ThrowIfCancellationRequested(); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri!.Host == "api.fish.audio" ? "audio" : JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = "{\"text\":\"なるほど！\",\"emotion\":\"happy\",\"voiceEmotion\":\"curious\",\"transcript\":\"\"}" } } } } } })) };
        }
    }
    sealed class RetryFake(int status) : HttpMessageHandler
    {
        public List<string> Keys = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            ct.ThrowIfCancellationRequested(); bool google = request.RequestUri!.Host.Contains("google");
            Keys.Add(google ? request.Headers.GetValues("x-goog-api-key").First() : request.Headers.Authorization!.Parameter!);
            string content = google ? JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = "{\"text\":\"Hi\",\"emotion\":\"happy\"}" } } } } } }) : "audio";
            return Task.FromResult(new HttpResponseMessage(Keys.Count == 1 ? (HttpStatusCode)status : HttpStatusCode.OK) { Content = new StringContent(content) });
        }
    }
}
