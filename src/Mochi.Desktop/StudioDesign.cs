using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using Microsoft.Win32;
using Mochi;

namespace MochiDuo;
public sealed partial class Studio
{
    readonly CompanionLibrary library = new();
    readonly StackPanel roster = new();
    readonly ComboBox primaryPicker = new() { MinWidth = 160 };
    readonly ComboBox secondaryPicker = new() { MinWidth = 160 };
    CompanionProfile Primary => library.Find(config.PrimaryCompanion);
    CompanionProfile Secondary => library.Find(config.SecondaryCompanion, 1);
    CompanionProfile Profile(int turn) => turn % 2 == 0 ? Primary : Secondary;
    string CompanionPrompt(string subject, IReadOnlyList<string> context, int turn, int total) =>
        $"User topic: {subject}\nConversation so far:\n{string.Join("\n", context)}\nWrite only {Profile(turn).Name}'s next turn, speaking with {Profile(turn + 1).Name}. " +
        (turn == 0 ? "Introduce the topic and invite your partner's opinion. " : total > 0 && turn == total - 1 ? "Respond and end naturally. " : "Respond to their last point with a fresh thought or question. ") + "Use the language of the topic if output language is Auto.";

    void InstallTheme()
    {
        Resources = (ResourceDictionary)XamlReader.Parse("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button"><Setter Property="Cursor" Value="Hand"/><Setter Property="Foreground" Value="#392C50"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Opacity" Value="0.8"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="TextBox"><Setter Property="Background" Value="White"/><Setter Property="Foreground" Value="#392C50"/><Setter Property="BorderBrush" Value="#DED8E9"/><Setter Property="Padding" Value="10"/><Setter Property="FontSize" Value="14"/></Style>
          <Style TargetType="PasswordBox"><Setter Property="BorderBrush" Value="#DED8E9"/></Style>
          <Style TargetType="ComboBox"><Setter Property="Padding" Value="8"/><Setter Property="Margin" Value="0,3,0,6"/><Setter Property="MinHeight" Value="34"/></Style>
          <Style TargetType="TabControl"><Setter Property="Background" Value="Transparent"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TabControl"><ContentPresenter ContentSource="SelectedContent"/></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType="ScrollViewer"><Setter Property="PanningMode" Value="VerticalOnly"/></Style>
        </ResourceDictionary>
        """);
    }
    void BuildShell()
    {
        var shell = new Grid(); shell.ColumnDefinitions.Add(new() { Width = new GridLength(205) }); shell.ColumnDefinitions.Add(new());
        var rail = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(42, 34, 60)), Margin = new Thickness(0) };
        var branding = new StackPanel { Margin = new Thickness(22, 28, 16, 26) };
        branding.Children.Add(new TextBlock { Text = "mochi", FontSize = 34, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        branding.Children.Add(new TextBlock { Text = "DESKTOP COMPANION", FontSize = 10, Foreground = Brushes.Thistle, Margin = new Thickness(0, 6, 0, 0) }); DockPanel.SetDock(branding, Dock.Top); rail.Children.Add(branding);
        var note = new StackPanel { Margin = new Thickness(18) }; note.Children.Add(new TextBlock { Text = "A little company.\nA world of personality.", Foreground = Brushes.Thistle, TextWrapping = TextWrapping.Wrap, FontSize = 12 }); note.Children.Add(Ui.Button("Show on desktop", () => { PlaceActors(); WindowState = WindowState.Minimized; })); DockPanel.SetDock(note, Dock.Bottom); rail.Children.Add(note);
        var nav = new StackPanel { Margin = new Thickness(12) };
        foreach (var entry in new[] { ("♡   Companion chat", 0), ("✧   Companions", 2), ("◷   Chat history", 3), ("⚙   Settings", 1) }) {
            var button = Ui.Button(entry.Item1, () => tabs.SelectedIndex = entry.Item2); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Background = new SolidColorBrush(Color.FromRgb(66, 54, 86)); button.Foreground = Brushes.White; button.Margin = new Thickness(0, 4, 0, 4); nav.Children.Add(button);
            tabs.SelectionChanged += (_, e) => { if (e.Source == tabs) button.Background = new SolidColorBrush(tabs.SelectedIndex == entry.Item2 ? Color.FromRgb(119, 86, 153) : Color.FromRgb(66, 54, 86)); };
        }
        rail.Children.Add(nav); shell.Children.Add(rail);
        var main = new DockPanel { Margin = new Thickness(24, 20, 24, 18) }; Grid.SetColumn(main, 1); shell.Children.Add(main);
        var heading = new StackPanel(); heading.Children.Add(Ui.Text("Your everyday anime companions", 25)); heading.Children.Add(Ui.Text("Chat, share a topic, or let your companions keep each other company.", 13)); DockPanel.SetDock(heading, Dock.Top); main.Children.Add(heading);
        var cards = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Margin = new Thickness(0, 12, 0, 12), Child = roster }; DockPanel.SetDock(cards, Dock.Top); main.Children.Add(cards); main.Children.Add(tabs); Content = shell;
    }
    void LoadCompanions()
    {
        if (config.Mode == "AZKi only") { config.PrimaryCompanion = "azki"; config.Mode = "One character"; }
        mochi.Load(Primary); azki.Load(Secondary); roster.Children.Clear();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var profile in new[] { Primary, Secondary }) {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Width = 270 };
            item.Children.Add(new Image { Source = CompanionLibrary.LoadImage(profile.ImagePath("happy")), Height = 85, Width = 65, Stretch = Stretch.Uniform });
            var copy = new StackPanel { Margin = new Thickness(12, 4, 0, 0), Width = 180 }; copy.Children.Add(Ui.Text(profile.Name, 19)); copy.Children.Add(Ui.Text(profile.Personality.Length > 74 ? profile.Personality[..71] + "…" : profile.Personality, 12)); item.Children.Add(copy); row.Children.Add(item);
        }
        roster.Children.Add(row); primaryPicker.ItemsSource = library.Items.ToArray(); secondaryPicker.ItemsSource = library.Items.ToArray(); primaryPicker.SelectedItem = Primary; secondaryPicker.SelectedItem = Secondary;
        if (library.Warning.Length > 0) status.Text = library.Warning;
    }
    UIElement BuildCompanions()
    {
        var dock = new DockPanel(); var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        var body = new StackPanel { Margin = new Thickness(8) }; body.Children.Add(Ui.Text("Your companion collection", 23)); body.Children.Add(Ui.Text("Choose who stays on your desktop. The first companion is used in solo mode.", 13));
        body.Children.Add(Ui.Text("First companion")); body.Children.Add(primaryPicker); body.Children.Add(Ui.Text("Second companion")); body.Children.Add(secondaryPicker);
        body.Children.Add(Ui.Button("Use selected companions", () => {
            if (run != null) { status.Text = "Stop chatting before changing companions."; return; }
            if (primaryPicker.SelectedItem is not CompanionProfile first || secondaryPicker.SelectedItem is not CompanionProfile second) return;
            if (first.Id == second.Id) { MessageBox.Show("Choose two different companions. Solo mode uses only the first."); return; }
            try { config.PrimaryCompanion = first.Id; config.SecondaryCompanion = second.Id; config.Save(); ai.Clear(); LoadCompanions(); ApplyMode(); status.Text = "Companions saved."; } catch (Exception e) { MessageBox.Show(e.Message); }
        }));
        var length = new ComboBox { ItemsSource = new[] { "Short", "Normal", "Long" }, SelectedItem = config.ReplyLength }; body.Children.Add(Ui.Text("Reply length · applies to solo and duo")); body.Children.Add(length);
        length.SelectionChanged += (_, _) => { if (run == null) { config.ReplyLength = (string)length.SelectedItem; try { config.Save(); } catch (Exception e) { status.Text = e.Message; } } };
        body.Children.Add(Ui.Text("Create someone new", 21)); body.Children.Add(Ui.Text("Bring your own artwork, personality, and Fish Audio voice. Transparent PNGs keep the desktop background visible. Optional expressions fall back to the main image.", 13));
        body.Children.Add(Ui.Button("+  Create companion", () => EditCompanion(null)));
        body.Children.Add(Ui.Button("Edit / duplicate first companion", () => EditCompanion(primaryPicker.SelectedItem as CompanionProfile)));
        footer.Children.Add(Ui.Text("Your imported artwork is copied locally, so moving the original files won’t break your companion.", 12)); dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); return dock;
    }
    void EditCompanion(CompanionProfile? existing)
    {
        if (run != null) { status.Text = "Stop chatting before editing companions."; return; }
        var dialog = new Window { Owner = this, Title = "Companion creator", Width = 650, Height = 730, MinWidth = 500, MinHeight = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Ui.Shell, Resources = Resources };
        var dock = new DockPanel { Margin = new Thickness(22) }; dialog.Content = dock;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        var body = new StackPanel(); body.Children.Add(Ui.Text(existing == null ? "Meet your new companion" : "Make this companion your own", 24));
        var name = new TextBox { Text = existing == null ? "" : existing.Name + (existing.BuiltIn ? " remix" : ""), MaxLength = 60 }; body.Children.Add(Ui.Text("Name")); body.Children.Add(name);
        var personality = new TextBox { Text = existing?.Personality ?? "", MaxLength = 2000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 85 }; body.Children.Add(Ui.Text("Personality · how should they talk and behave?")); body.Children.Add(personality);
        var voice = new TextBox { Text = existing?.Voice ?? "", MaxLength = 32 }; body.Children.Add(Ui.Text("Fish Audio voice ID · optional for text-only chat")); body.Children.Add(voice);
        var images = existing == null ? new Dictionary<string, string>() : new Dictionary<string, string>(existing.Images);
        var preview = new Image { Height = 130, Stretch = Stretch.Uniform }; body.Children.Add(preview); if (images.TryGetValue("happy", out var initial)) preview.Source = CompanionLibrary.LoadImage(initial);
        foreach (var key in new[] { "happy", "sad", "puffyface", "angry" }) {
            var label = Ui.Text(images.ContainsKey(key) ? "Image selected" : "No image selected", 12); var row = new DockPanel();
            var button = Ui.Button(key == "happy" ? "Choose main image *" : "Choose " + key + " image", () => {
                var picker = new OpenFileDialog { Filter = "Images|*.png;*.webp;*.jpg;*.jpeg;*.bmp", Title = "Choose companion artwork" };
                if (picker.ShowDialog(dialog) == true) try { var image = CompanionLibrary.LoadImage(picker.FileName); images[key] = picker.FileName; preview.Source = image; label.Text = System.IO.Path.GetFileName(picker.FileName); } catch (Exception e) { MessageBox.Show(dialog, e.Message); }
            }); DockPanel.SetDock(button, Dock.Left); row.Children.Add(button); row.Children.Add(label); body.Children.Add(row);
        }
        var result = Ui.Text("", 12); footer.Children.Add(result);
        footer.Children.Add(Ui.Button("Save companion", () => { try { var saved = library.Save(name.Text, personality.Text, voice.Text.Trim(), images, existing?.BuiltIn == false ? existing.Id : null); if (existing?.Id == config.PrimaryCompanion) config.PrimaryCompanion = saved.Id; if (existing?.Id == config.SecondaryCompanion) config.SecondaryCompanion = saved.Id; config.Save(); LoadCompanions(); ApplyMode(); dialog.Close(); status.Text = saved.Name + " saved. Select them in your companion collection."; } catch (Exception e) { result.Text = e.Message; } }));
        dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.ShowDialog();
    }
}
