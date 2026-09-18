using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Mochi;

namespace MochiDuo;

public sealed class CompanionProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New companion";
    public string Personality { get; set; } = "Warm, curious, and supportive.";
    public string Voice { get; set; } = "";
    public Dictionary<string, string> Images { get; set; } = new();
    public bool BuiltIn { get; set; }
    public override string ToString() => Name;
    public string ImagePath(string emotion) => Images.TryGetValue(emotion, out var file) ? file : Images["happy"];
}

public sealed class CompanionLibrary
{
    readonly string folder;
    public List<CompanionProfile> Items { get; } = new();
    public string Warning { get; private set; } = "";
    public CompanionLibrary(string? directory = null)
    {
        folder = directory ?? Path.Combine(Settings.Folder, "companions");
        string Asset(string who, string image) => Path.Combine(AppContext.BaseDirectory, who, image + ".png");
        Items.Add(new() { Id = "mochi", Name = "Mochi", BuiltIn = true, Voice = Dialogue.MochiVoice, Personality = "Playful, curious and energetic; a cheerful cat-eared desktop friend.", Images = new() { ["happy"] = Asset("mochi", "happy"), ["sad"] = Asset("mochi", "sad"), ["puffyface"] = Asset("mochi", "puffyface"), ["angry"] = Asset("mochi", "puffyface") } });
        Items.Add(new() { Id = "azki", Name = "AZKi", BuiltIn = true, Voice = Dialogue.AzkiVoice, Personality = "Thoughtful, warm and gently witty; enjoys music and exploring new ideas.", Images = new() { ["happy"] = Asset("azki", "normal"), ["sad"] = Asset("azki", "normal"), ["puffyface"] = Asset("azki", "puffyface"), ["angry"] = Asset("azki", "angry") } });
        try {
            var path = Path.Combine(folder, "library.json");
            if (File.Exists(path)) foreach (var profile in JsonSerializer.Deserialize<List<CompanionProfile>>(File.ReadAllText(path)) ?? new()) {
                if (profile.Images == null || !profile.Images.ContainsKey("happy") || !profile.Images.Values.All(File.Exists)) { Warning = "Some companion images are missing. Re-import them in Companions."; continue; }
                profile.BuiltIn = false; Items.Add(profile);
            }
        } catch { Warning = "The companion library could not be read. Its file has been preserved."; }
    }
    public CompanionProfile Find(string id, int fallback = 0) => Items.Find(p => p.Id == id) ?? Items[fallback];
    public static BitmapImage LoadImage(string path)
    {
        var info = new FileInfo(path); if (!info.Exists || info.Length > 25 * 1024 * 1024) throw new InvalidOperationException("Choose an image smaller than 25 MB.");
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit();
        if (bitmap.PixelWidth > 8192 || bitmap.PixelHeight > 8192) throw new InvalidOperationException("Images must be no larger than 8192 pixels per side.");
        bitmap.Freeze(); return bitmap;
    }
    public CompanionProfile Save(string name, string personality, string voice, Dictionary<string, string> images, string? replaceId = null)
    {
        if (Warning.Length > 0) throw new InvalidOperationException(Warning + " Back up and repair library.json before saving.");
        if (string.IsNullOrWhiteSpace(name) || !images.ContainsKey("happy")) throw new InvalidOperationException("Enter a name and choose a main image.");
        if (voice.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(voice, "^[a-fA-F0-9]{32}$")) throw new InvalidOperationException("Fish voice IDs contain 32 hexadecimal characters. Leave blank for text-only use.");
        foreach (var path in images.Values) LoadImage(path);
        var profile = new CompanionProfile { Name = name.Trim(), Personality = personality.Trim(), Voice = voice.Trim() };
        // Each revision owns its images so an interrupted save cannot damage an existing companion.
        var target = Path.Combine(folder, profile.Id); Directory.CreateDirectory(target);
        foreach (var pair in images) { var dest = Path.Combine(target, pair.Key + Path.GetExtension(pair.Value)); File.Copy(pair.Value, dest); profile.Images[pair.Key] = dest; }
        var updated = Items.Where(p => !p.BuiltIn && p.Id != replaceId).ToList(); updated.Add(profile);
        var file = Path.Combine(folder, "library.json"); File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(updated)); File.Move(file + ".tmp", file, true);
        if (replaceId != null) Items.RemoveAll(p => p.Id == replaceId && !p.BuiltIn); Items.Add(profile); return profile;
    }
}
