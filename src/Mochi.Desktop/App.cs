using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mochi;
namespace MochiDuo;
public static class Native
{
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserObjectInformation(IntPtr h, int index, StringBuilder b, int size, out int needed);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    public static string Scope() { var b = new StringBuilder(512); if (!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, b, 1024, out _)) throw new Exception("Cannot identify desktop."); return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + b))); }
}
public static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if (Array.Exists(args, a => a == "--test" || a == "--live-test")) { Tests.Run(Array.Exists(args, a => a == "--live-test")).GetAwaiter().GetResult(); return; }
        var scope = Native.Scope() + (Array.Exists(args, a => a == "--smoke-test") ? "-smoke-" + Environment.ProcessId : ""); using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\MochiCompanion-v1-open-" + scope); using var mutex = new Mutex(true, "Local\\MochiCompanion-v1-" + scope, out var first);
        if (!first) { signal.Set(); return; }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { MessageBox.Show(e.Exception.Message, "Mochi"); e.Handled = true; };
        var studio = new Studio(); app.MainWindow = studio;
        if (Array.Exists(args, a => a == "--settings")) studio.Loaded += (_, _) => studio.ShowSettings();
        var signalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) }; signalTimer.Tick += (_, _) => { if (signal.WaitOne(0)) studio.BringBack(); }; signalTimer.Start();
        if (Array.Exists(args, a => a == "--smoke-test")) {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; timer.Tick += (_, _) => { timer.Stop(); studio.RenderPreviews(); studio.Close(); }; timer.Start();
        }
        app.Run(studio);
    }
}
public static class Ui
{
    public static readonly Brush Shell = new SolidColorBrush(Color.FromRgb(247,245,251));
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(61,44,67));
    public static TextBlock Text(string value, int size = 14) => new() { Text = value, FontSize = size, Foreground = Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,5,0,5) };
    public static Button Button(string label, Action action) { var b = new Button { Content = label, Padding = new Thickness(16,10,16,10), Margin = new Thickness(4), Background = new SolidColorBrush(Color.FromRgb(233,220,249)), BorderThickness = new Thickness(0) }; b.Click += (_, _) => action(); return b; }
}
public static class Dialogue
{
    public const string MochiVoice = "4f61a043daea4c3ab0d9070dfbd69f3c", AzkiVoice = "5f8f82504223455f906c53e6d3e6b8cd";
    public static string Speaker(int turn) => turn % 2 == 0 ? "Mochi" : "AZKi";
    public static string Voice(int turn) => turn % 2 == 0 ? MochiVoice : AzkiVoice;
    public static string Prompt(string topic, IReadOnlyList<string> history, int turn, int total) => $"Topic supplied by the user: {topic}\nConversation so far:\n{string.Join("\n", history)}\nNow write only {Speaker(turn)}'s next turn ({turn + 1}{(total > 0 ? " of " + total : " · ongoing")}). " + (turn == 0 ? "Open the topic and invite AZKi's opinion." : total > 0 && turn == total - 1 ? "Respond to your partner and end the conversation naturally." : "Respond to your partner and add a fresh observation or question.") + " If output language is Auto, use the language of the topic supplied by the user, not the language of these instructions.";
    public static readonly string[] LengthOptions = { "Shorter · 4 turns", "Normal · 6 turns", "Longer · 12 turns", "Extended · 20 turns", "Ultimate · until Stop" };
    public static bool IsUltimate(string option) => option.StartsWith("Ultimate", StringComparison.OrdinalIgnoreCase);
    public static int TurnCount(string option) => option.StartsWith("Shorter") ? 4 : option.StartsWith("Longer") ? 12 : option.StartsWith("Extended") ? 20 : 6;
    public static string Sprite(bool azki, string emotion) => azki ? emotion switch { "puffyface" => "puffyface", "angry" => "angry", _ => "normal" } : emotion == "angry" ? "puffyface" : emotion;
}
public sealed class Actor : Window
{
    readonly Image image = new() { Stretch = Stretch.Uniform, Cursor = Cursors.Hand }; readonly TextBlock text = Ui.Text(""); readonly Dictionary<string, BitmapSource> sprites = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    public Actor(Action show)
    {
        Width = 310; Height = 520; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        var grid = new Grid(); grid.RowDefinitions.Add(new() { Height = new GridLength(100) }); grid.RowDefinitions.Add(new());
        var bubble = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), Padding = new Thickness(12), Margin = new Thickness(4), Child = text }; grid.Children.Add(bubble); Grid.SetRow(image,1); grid.Children.Add(image); Content = grid;
        image.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) show(); else DragMove(); }; bubble.MouseLeftButtonDown += (_, _) => show();
        timer.Tick += (_, _) => { if (IsVisible && !Topmost && !IsMouseOver) Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(1),0,0,0,0,0x13); }; timer.Start(); Closed += (_, _) => timer.Stop();
    }
    public void Load(CompanionProfile profile) { sprites.Clear(); foreach (var pair in profile.Images) sprites[pair.Key] = CompanionLibrary.LoadImage(pair.Value); Title = profile.Name; Set("happy", profile.Name + " · double-click to chat", false); }
    public void Set(string emotion, string line, bool speaking) { image.Source = sprites.TryGetValue(emotion,out var sprite) ? sprite : sprites["happy"]; text.Text = line.Length > 150 ? line[..147] + "…" : line; Animate(speaking); }
    public void Animate(bool active) { var transform = new TranslateTransform(); image.RenderTransform = transform; if (active) transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-4,TimeSpan.FromSeconds(.3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }); }
}
public sealed partial class Studio : Window
{
    readonly Settings config = Settings.Load(); readonly AiClient ai = new(); readonly MediaPlayer player = new(); readonly Actor mochi, azki;
    readonly TextBox topic = new() { MaxLength = 2000, MinHeight = 62, MaxHeight = 100, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    readonly ComboBox language = new() { ItemsSource = AiClient.Languages }; readonly ComboBox turns = new() { ItemsSource = Dialogue.LengthOptions, SelectedIndex = 1 };
    readonly CheckBox speech = new() { Content = "Speak replies", Margin = new Thickness(4,8,4,8) }; readonly StackPanel messages = new(); readonly TextBlock status = Ui.Text("Ready when you are.",12);
    readonly Button start; readonly TabControl tabs = new(); readonly ChatArchive archive = new(); readonly TextBox historyView = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    readonly TextBlock inputLabel = Ui.Text(""); readonly ScrollViewer transcript; CancellationTokenSource? run; string? audioFile; bool closing;
    public Studio()
    {
        Title = "Mochi · Desktop Anime Companion"; Width = Math.Min(1120,SystemParameters.WorkArea.Width); Height = Math.Min(820,SystemParameters.WorkArea.Height); MinWidth = Math.Min(920,SystemParameters.WorkArea.Width); MinHeight = Math.Min(680,SystemParameters.WorkArea.Height); WindowStartupLocation = WindowStartupLocation.CenterScreen; FontFamily = new FontFamily("Segoe UI"); Background = Ui.Shell;
        try { Icon = BitmapFrame.Create(new Uri(Path.Combine(AppContext.BaseDirectory,"MochiDuo.ico"))); } catch { }
        InstallTheme(); mochi = new Actor(BringBack); azki = new Actor(BringBack); mochi.Topmost = azki.Topmost = config.OnTop; BuildShell();
        var chat = new DockPanel(); var controls = new StackPanel(); controls.Children.Add(inputLabel); controls.Children.Add(topic);
        var options = new Grid(); options.ColumnDefinitions.Add(new()); options.ColumnDefinitions.Add(new());
        var left = new StackPanel(); left.Children.Add(Ui.Text("Reply language",12)); language.SelectedItem = config.OutputLanguage; left.Children.Add(language); options.Children.Add(left);
        var right = new StackPanel { Margin = new Thickness(12,0,0,0) }; right.Children.Add(Ui.Text("Conversation duration",12)); right.Children.Add(turns); Grid.SetColumn(right,1); options.Children.Add(right); controls.Children.Add(options);
        speech.IsChecked = config.Speak; var buttons = new WrapPanel(); buttons.Children.Add(speech); start = Ui.Button("Send", () => _ = Start()); buttons.Children.Add(start); buttons.Children.Add(Ui.Button("Stop", () => run?.Cancel())); controls.Children.Add(buttons); controls.Children.Add(status); DockPanel.SetDock(controls,Dock.Bottom); chat.Children.Add(controls);
        messages.Children.Add(Ui.Text("Make yourself at home",22)); messages.Children.Add(Ui.Text("Say hello in solo mode, or start a conversation between two companions. Choose your cast in Companions and your mode in Settings.",14));
        transcript = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; chat.Children.Add(transcript);
        tabs.Items.Add(new TabItem { Content = chat }); tabs.Items.Add(new TabItem { Content = BuildSettings() }); tabs.Items.Add(new TabItem { Content = BuildCompanions() });
        var history = new DockPanel(); var tools = new StackPanel(); tools.Children.Add(Ui.Text("Chat history",23)); tools.Children.Add(Ui.Text("Last 500 messages · encrypted on this PC",12)); tools.Children.Add(Ui.Button("Clear history", () => { if (run != null) return; if (MessageBox.Show(this,"Delete all saved chat history?","Clear history",MessageBoxButton.YesNo) == MessageBoxResult.Yes) try { archive.Clear(); ai.Clear(); messages.Children.Clear(); RefreshHistory(); } catch(Exception e) { status.Text = e.Message; } })); DockPanel.SetDock(tools,Dock.Top); history.Children.Add(tools); history.Children.Add(historyView); tabs.Items.Add(new TabItem { Content = history }); tabs.SelectedIndex = 0;
        ai.Notice += notice => status.Text = notice; LoadCompanions(); ApplyMode(); RefreshHistory();
        if (Settings.LoadWarning.Length > 0 || archive.Warning.Length > 0) status.Text = Settings.LoadWarning + " " + archive.Warning;
        Loaded += (_, _) => { PlaceActors(); BringBack(); }; Closing += (_, _) => { closing = true; run?.Cancel(); StopAudio(); mochi.Close(); azki.Close(); ai.Dispose(); };
    }
    public void BringBack() { Show(); WindowState = WindowState.Normal; Topmost = true; Activate(); Native.SetForegroundWindow(new WindowInteropHelper(this).Handle); Dispatcher.BeginInvoke(new Action(() => Topmost = false)); }
    public void ShowSettings() => tabs.SelectedIndex = 1;
    void PlaceActors() { mochi.Show(); if (config.Mode == "Two characters") azki.Show(); else azki.Hide(); var area = SystemParameters.WorkArea; mochi.Left = area.Right - (config.Mode == "Two characters" ? 640 : 320); azki.Left = area.Right - 320; mochi.Top = azki.Top = Math.Max(area.Top,area.Bottom - 520); }
    void ApplyMode() { start.Content = config.Mode == "Two characters" ? "Start conversation →" : "Send message →"; inputLabel.Text = config.Mode == "Two characters" ? "Give your companions a topic" : "Message " + Primary.Name; turns.IsEnabled = config.Mode == "Two characters"; if (IsLoaded) PlaceActors(); }
    void RefreshHistory() { historyView.Text = string.Join("\n\n",archive.Lines.ConvertAll(line => $"{line.At:g} · {line.Speaker}\n{line.Text}")); historyView.ScrollToEnd(); }
    void Remember(string speaker,string text) { try { archive.Add(speaker,text); RefreshHistory(); } catch(Exception e) { status.Text = "History could not be saved: " + e.Message; } }
    UIElement BuildSettings()
    {
        var dock = new DockPanel(); var footer = new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); dock.Children.Add(footer); var body = new StackPanel { Margin = new Thickness(8) };
        body.Children.Add(Ui.Text("Settings",24)); body.Children.Add(Ui.Text("Connections, voices, and your desktop",13));
        var mode = new ComboBox { ItemsSource = new[] { "One character", "Two characters" }, SelectedItem = config.Mode == "Two characters" ? "Two characters" : "One character" }; body.Children.Add(Ui.Text("Companion mode")); body.Children.Add(mode);
        PasswordBox Key(string label,string value) { body.Children.Add(Ui.Text(label)); var box = new PasswordBox { Password = value, Padding = new Thickness(10) }; body.Children.Add(box); return box; }
        var google = Key("Google AI Studio API key",config.GoogleKey); body.Children.Add(Ui.Text("Gemini model")); var model = new TextBox { Text = config.GoogleModel }; body.Children.Add(model); var fish = Key("Fish Audio API key",config.FishKey);
        body.Children.Add(Ui.Text("Fish model")); var fishModel = new ComboBox { ItemsSource = new[] { "s2.1-pro-free", "s2-pro" }, SelectedItem = config.FishModel }; body.Children.Add(fishModel);
        var fallback = new CheckBox { Content = "Enable fallback API keys", IsChecked = config.EnableFallback, Margin = new Thickness(0,12,0,8) }; body.Children.Add(fallback); var googleBackup = Key("Fallback Google key",config.GoogleFallbackKey); var fishBackup = Key("Fallback Fish key",config.FishFallbackKey);
        body.Children.Add(Ui.Text("Retries once for rejected credentials, rate limits, or server errors. Provider quotas and pricing still apply. Keys are encrypted on this PC.",12));
        var top = new CheckBox { Content = "Keep companions above other windows", IsChecked = config.OnTop, Margin = new Thickness(0,12,0,12) }; body.Children.Add(top); body.Children.Add(Ui.Text("Voice IDs and personalities live in Companions. Language and speech controls are on the chat page.",12));
        var result = Ui.Text("",12); footer.Children.Add(result); footer.Children.Add(Ui.Button("Save settings", () => {
            if (run != null) { result.Text = "Stop chatting before saving settings."; return; }
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Text.Trim(),@"^[a-zA-Z0-9.\-]+$")) { result.Text = "Enter a valid Gemini model name."; return; }
            config.GoogleKey = google.Password.Trim(); config.FishKey = fish.Password.Trim(); config.GoogleModel = model.Text.Trim(); config.FishModel = (string)fishModel.SelectedItem; config.EnableFallback = fallback.IsChecked == true; config.GoogleFallbackKey = googleBackup.Password.Trim(); config.FishFallbackKey = fishBackup.Password.Trim(); config.OnTop = top.IsChecked == true;
            if (config.Mode != (string)mode.SelectedItem) ai.Clear(); config.Mode = (string)mode.SelectedItem; config.OutputLanguage = (string)language.SelectedItem; config.Speak = speech.IsChecked == true;
            try { config.Save(); mochi.Topmost = azki.Topmost = config.OnTop; ApplyMode(); result.Text = "Settings saved ✓"; } catch(Exception e) { result.Text = e.Message; }
        })); dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); return dock;
    }
    Settings Snapshot(int turn) => new() { CharacterName = Profile(turn).Name, Personality = Profile(turn).Personality, ReplyLength = config.ReplyLength, Mode = config.Mode, GoogleKey = config.GoogleKey, FishKey = config.FishKey, GoogleFallbackKey = config.GoogleFallbackKey, FishFallbackKey = config.FishFallbackKey, EnableFallback = config.EnableFallback, GoogleModel = config.GoogleModel, FishModel = config.FishModel, Voice = Profile(turn).Voice, Speak = config.Speak, OutputLanguage = config.OutputLanguage };
    sealed record Prepared(Reply Reply, byte[]? Audio, int Turn);
    async Task<Prepared> Prepare(string subject, IReadOnlyList<string> history,int turn,int total,CancellationToken ct)
    {
        using var client = new AiClient(); client.Notice += notice => Dispatcher.BeginInvoke(new Action(() => status.Text = notice)); var settings = Snapshot(turn);
        var reply = await client.Chat(settings,CompanionPrompt(subject,history,turn,total),null,ct); var audio = settings.Speak ? await client.Speech(settings,reply,ct) : null; return new(reply,audio,turn);
    }
    void Reveal(Prepared prepared)
    {
        var profile = Profile(prepared.Turn); var reply = prepared.Reply; Remember(profile.Name,reply.Text);
        var block = new StackPanel(); block.Children.Add(Ui.Text(profile.Name + " · " + reply.VoiceEmotion,12)); block.Children.Add(Ui.Text(reply.Text,15)); messages.Children.Add(new Border { Child = block, Background = new SolidColorBrush(prepared.Turn % 2 == 0 ? Color.FromRgb(251,226,237) : Color.FromRgb(235,227,246)), Padding = new Thickness(16), CornerRadius = new CornerRadius(14), Margin = new Thickness(0,6,8,6) }); transcript.ScrollToEnd();
        (prepared.Turn % 2 == 0 ? mochi : azki).Set(reply.VoiceEmotion == "angry" ? "angry" : reply.Emotion,reply.Text,prepared.Audio != null);
    }
    async Task Present(Prepared prepared,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); status.Text = Profile(prepared.Turn).Name + (prepared.Audio == null ? " replied" : " is speaking…");
        try { if (prepared.Audio != null) await Play(prepared.Audio, () => Reveal(prepared),ct); else { Reveal(prepared); await Task.Delay(1800,ct); } }
        finally { (prepared.Turn % 2 == 0 ? mochi : azki).Animate(false); }
    }
    async Task Start()
    {
        if (run != null) return; var subject = topic.Text.Trim(); if (subject.Length == 0) { status.Text = "Type a message or topic first."; return; }
        if (config.GoogleKey.Length == 0 || (speech.IsChecked == true && config.FishKey.Length == 0)) { status.Text = "Add your API keys in Settings first."; ShowSettings(); return; }
        if (speech.IsChecked == true && (Primary.Voice.Length == 0 || (config.Mode == "Two characters" && Secondary.Voice.Length == 0))) { status.Text = "Add a voice ID in Companions or switch off Speak replies."; return; }
        using var cts = new CancellationTokenSource(); run = cts; start.IsEnabled = topic.IsEnabled = language.IsEnabled = speech.IsEnabled = turns.IsEnabled = false; Task<Prepared>? pending = null;
        try {
            config.OutputLanguage = (string)language.SelectedItem; config.Speak = speech.IsChecked == true; config.Save(); bool solo = config.Mode != "Two characters";
            Remember(solo ? "You" : "Topic",subject); messages.Children.Add(Ui.Text((solo ? "You  ·  " : "Topic  ·  ") + subject,15)); transcript.ScrollToEnd(); if (solo) topic.Clear(); status.Text = Primary.Name + " is thinking and preparing a reply…";
            if (solo) { var settings = Snapshot(0); var reply = await ai.Chat(settings,subject,null,cts.Token); var audio = settings.Speak ? await ai.Speech(settings,reply,cts.Token) : null; await Present(new(reply,audio,0),cts.Token); }
            else {
                string choice = (string)turns.SelectedItem; int total = Dialogue.IsUltimate(choice) ? -1 : Dialogue.TurnCount(choice); var history = new List<string>(); var current = await Prepare(subject,history,0,total,cts.Token);
                for (int turn = 0; total < 0 || turn < total; turn++) {
                    cts.Token.ThrowIfCancellationRequested(); history.Add(Profile(turn).Name + ": " + current.Reply.Text); if (history.Count > 24) history.RemoveAt(0);
                    // Prepare exactly one reply ahead, with all preceding dialogue in context.
                    pending = total < 0 || turn + 1 < total ? Prepare(subject,history.ToArray(),turn + 1,total,cts.Token) : null;
                    await Present(current,cts.Token); if (pending == null) break; status.Text = Profile(turn + 1).Name + " is preparing a reply…"; current = await pending; pending = null;
                }
            }
            status.Text = "Ready for your next conversation.";
        } catch(OperationCanceledException) { status.Text = cts.IsCancellationRequested ? "Conversation stopped." : "Request timed out. Try again."; }
        catch(Exception e) { status.Text = e.Message + " You can turn off Speak replies to use text only."; }
        finally { cts.Cancel(); if (pending != null) try { await pending; } catch { } StopAudio(); mochi.Animate(false); azki.Animate(false); run = null; if (!closing) { start.IsEnabled = topic.IsEnabled = language.IsEnabled = speech.IsEnabled = true; ApplyMode(); } }
    }
    async Task Play(byte[] audio,Action reveal,CancellationToken ct)
    {
        StopAudio(); audioFile = Path.Combine(Path.GetTempPath(),"mochi-" + Guid.NewGuid() + ".mp3"); var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler opened = (_, _) => { if (ct.IsCancellationRequested) return; reveal(); player.Play(); }; EventHandler ended = (_, _) => done.TrySetResult(); EventHandler<ExceptionEventArgs> failed = (_,e) => done.TrySetException(e.ErrorException);
        player.MediaOpened += opened; player.MediaEnded += ended; player.MediaFailed += failed;
        try { await File.WriteAllBytesAsync(audioFile,audio,ct); ct.ThrowIfCancellationRequested(); player.Open(new Uri(audioFile)); await done.Task.WaitAsync(TimeSpan.FromMinutes(4),ct); }
        finally { player.MediaOpened -= opened; player.MediaEnded -= ended; player.MediaFailed -= failed; StopAudio(); }
    }
    void StopAudio() { player.Stop(); player.Close(); if (audioFile != null) { try { File.Delete(audioFile); } catch { } audioFile = null; } }
    public void RenderPreviews()
    {
        for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; UpdateLayout(); var image = new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32); image.Render(this); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(AppContext.BaseDirectory,$"preview-{i}.png")); png.Save(file); }
    }
}
