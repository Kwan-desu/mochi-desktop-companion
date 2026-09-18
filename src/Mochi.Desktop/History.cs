using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Mochi;

namespace MochiDuo;
public record HistoryLine(DateTime At, string Speaker, string Text);
public sealed class ChatArchive
{
    public List<HistoryLine> Lines { get; private set; } = new();
    readonly string path;
    public string Warning { get; private set; } = "";
    public ChatArchive(string? file = null) {
        path = file ?? Path.Combine(Settings.Folder, "history.bin");
        if (!File.Exists(path)) return;
        try { Lines = JsonSerializer.Deserialize<List<HistoryLine>>(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)) ?? new(); }
        catch { Warning = "Saved history could not be read. The existing file will not be overwritten until you clear history."; }
    }
    public void Add(string speaker, string text) { Lines.Add(new(DateTime.Now, speaker, text)); if (Lines.Count > 500) Lines.RemoveRange(0, Lines.Count - 500); if (Warning.Length == 0) Save(); }
    public void Clear() { Lines.Clear(); Warning = ""; Save(); }
    void Save() { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path + ".tmp", ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(Lines), null, DataProtectionScope.CurrentUser)); File.Move(path + ".tmp", path, true); }
}
