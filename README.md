# Mochi Desktop Companion

A Windows anime desktop companion with transparent, draggable characters, Gemini conversations, and expressive Fish Audio voices.

## Start

On Windows, install the .NET 10 SDK to build, then double-click **Build.cmd**. Open **Launch Desktop Companion.cmd** to start. After building, only the .NET 10 Desktop Runtime is required. Keep the files in `app/` together.

Enter API keys in **Settings → Save settings**. Choose solo or two-character mode. Use **Companions → Create companion** to add artwork, a personality, and an optional Fish voice ID. See [the user guide](docs/USER-GUIDE.md).

## Features

- Solo chat or alternating two-character discussions
- Custom companions and expression images
- Output language selection, reply length, finite or unlimited conversations
- Text synchronized with playback and one-turn-ahead audio preparation
- Google and Fish fallback keys
- Encrypted settings and the last 500 history entries

## Project layout

| Folder | Purpose |
| --- | --- |
| `src/Mochi.Desktop/` | Current WPF application and offline tests |
| `assets/` | Character images, original reference artwork, and app icon |
| `docs/` | User guide and artwork provenance |
| `tools/IconMaker/` | Rebuildable Windows icon utility |
| `app/` | Local compiled app; generated and excluded from Git |

## Build and test

```powershell
dotnet publish src/Mochi.Desktop/MochiDuo.csproj -c Release -o app --self-contained false
Start-Process app/MochiDuo.exe -ArgumentList '--test' -Wait
Get-Content app/test-results.txt
```

Offline tests use mocked API responses and temporary test storage. `--live-test` explicitly uses saved API keys and provider quota. Do not use it in unattended CI.

## Local data and privacy

Settings, history, and custom companions live in `%LOCALAPPDATA%\MochiDuo`, outside this repository. API keys and history use Windows account encryption. Prompts/personality are sent to Google; speech text and voice IDs are sent to Fish Audio. Provider access, pricing, and quotas apply.

Bundled artwork was supplied for this project; no redistribution license or ownership is asserted. Artwork provenance is documented in [ARTWORK.md](docs/ARTWORK.md).
