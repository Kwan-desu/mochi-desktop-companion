using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MochiDuo;

public static class TaskbarMotion
{
    public static double ClampX(double x, double width, Rect area) => Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - width));
    public static double Step(double x, double target, double seconds) => x + Math.Sign(target - x) * Math.Min(Math.Abs(target - x), 42 * Math.Clamp(seconds, 0, .1));
}

public sealed partial class Actor
{
    readonly DispatcherTimer activityTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly Stopwatch activityClock = Stopwatch.StartNew();
    bool wandering, dragging, speaking, walking;
    double targetX, lastTick, phase;
    DateTime nextAction = DateTime.UtcNow.AddSeconds(4);
    void InitializeActivities()
    {
        activityTimer.Tick += (_, _) => ActivityTick(); activityTimer.Start();
        var menu = new System.Windows.Controls.ContextMenu();
        var open = new System.Windows.Controls.MenuItem { Header = "Open companion chat" }; open.Click += (_, _) => { var studio = Application.Current.MainWindow as Studio; studio?.BringBack(); }; menu.Items.Add(open);
        var pause = new System.Windows.Controls.MenuItem { Header = "Pause / resume walking" }; pause.Click += (_, _) => { wandering = !wandering; Rest(); }; menu.Items.Add(pause); image.ContextMenu = menu;
    }
    public void ConfigureActivities(bool enabled, bool onTop)
    {
        wandering = enabled; Topmost = onTop; Width = enabled ? 190 : 310; Height = enabled ? 320 : 520; Rest();
    }
    void Rest() { walking = false; nextAction = DateTime.UtcNow.AddSeconds(Random.Shared.Next(5, 12)); }
    void ActivityTick()
    {
        double now = activityClock.Elapsed.TotalSeconds; double elapsed = now - lastTick; lastTick = now;
        if (!IsVisible || !wandering || dragging || speaking || IsMouseOver || image.ContextMenu?.IsOpen == true) return;
        var area = SystemParameters.WorkArea;
        Top = Math.Max(area.Top, area.Bottom - Height); Left = TaskbarMotion.ClampX(double.IsNaN(Left) ? area.Left : Left, Width, area);
        if (!walking && DateTime.UtcNow >= nextAction) {
            if (Random.Shared.Next(3) == 0) {
                var transform = new TranslateTransform(); image.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, -12, TimeSpan.FromSeconds(.25)) { AutoReverse = true, RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2) });
                Rest(); return;
            }
            targetX = TaskbarMotion.ClampX(Left + Random.Shared.Next(-260, 261), Width, area); walking = true;
        }
        if (walking) {
            Left = TaskbarMotion.Step(Left, targetX, elapsed); phase += elapsed * 12;
            image.RenderTransform = new TranslateTransform(0, -Math.Abs(Math.Sin(phase)) * 4);
            if (Math.Abs(Left - targetX) < .5) { image.RenderTransform = Transform.Identity; Rest(); }
        }
    }
}

public sealed partial class Studio
{
    readonly DispatcherTimer idleTimer = new() { Interval = TimeSpan.FromMinutes(3) };
    void ApplyActivities() { mochi.ConfigureActivities(config.Wander,config.OnTop); azki.ConfigureActivities(config.Wander,config.OnTop); }
    void InitializeIdleTalk()
    {
        idleTimer.Tick += async (_, _) => {
            if (closing || run != null || !config.IdleTalk || WindowState != WindowState.Minimized || topic.Text.Trim().Length > 0 || config.GoogleKey.Length == 0) return;
            if (speech.IsChecked == true && (config.FishKey.Length == 0 || Primary.Voice.Length == 0)) return;
            string[] ideas = { "Share a small cheerful thought about taking a break.", "Make a playful observation about spending a quiet day together.", "Share a tiny imaginary adventure from your life as a desktop companion.", "Offer one gentle, friendly encouragement." };
            topic.Text = ideas[Random.Shared.Next(ideas.Length)]; await Start(true);
        }; idleTimer.Start();
    }
}
