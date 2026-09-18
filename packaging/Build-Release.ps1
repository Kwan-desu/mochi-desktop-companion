param([string]$InnoCompiler = 'ISCC.exe')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$project = 'src/Mochi.Desktop/MochiDuo.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true -o artifacts/portable -p:Version=1.0.0 -p:DebugType=None
if ($LASTEXITCODE) { throw 'Self-contained publish failed.' }
dotnet publish $project -c Release -r win-x64 --self-contained true -o artifacts/single -p:Version=1.0.0 -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None
if ($LASTEXITCODE) { throw 'Single-file publish failed.' }
& $InnoCompiler packaging/Mochi.iss
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
Copy-Item artifacts/single/MochiDuo.exe artifacts/release/Mochi-Desktop-Companion-1.0.0-win-x64-Portable.exe -Force
Get-ChildItem artifacts/release/*.exe | Get-FileHash -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content artifacts/release/SHA256SUMS.txt -Encoding ascii
