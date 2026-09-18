param([string]$InnoCompiler = 'ISCC.exe', [string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$project = 'src/Mochi.Desktop/MochiDuo.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true -o artifacts/portable -p:Version=$Version -p:NuGetAudit=false -p:DebugType=None
if ($LASTEXITCODE) { throw 'Self-contained publish failed.' }
dotnet publish $project -c Release -r win-x64 --self-contained true -o artifacts/single -p:Version=$Version -p:NuGetAudit=false -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None
if ($LASTEXITCODE) { throw 'Single-file publish failed.' }
& $InnoCompiler "/DAppVersion=$Version" packaging/Mochi.iss
if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
Copy-Item artifacts/single/MochiDuo.exe "artifacts/release/Mochi-Desktop-Companion-$Version-win-x64-Portable.exe" -Force
Get-ChildItem "artifacts/release/Mochi-Desktop-Companion-$Version-win-x64-*.exe" | Get-FileHash -Algorithm SHA256 | ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content artifacts/release/SHA256SUMS.txt -Encoding ascii
