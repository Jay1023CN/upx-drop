# SPDX-License-Identifier: GPL-2.0-or-later
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& "$root\build.ps1"
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo("$root\dist\UpxDrop\UpxDrop.exe").FileVersion
$version = ([Version]$version).ToString(3)
$releaseNotes = Join-Path $root ("docs\RELEASE-$version.md")
if (!(Test-Path -LiteralPath $releaseNotes)) { throw "Missing release notes: $releaseNotes" }
$officialSource = "$root\tools\upx-5.2.1-src.tar.xz"
if (!(Test-Path -LiteralPath $officialSource)) { & "$root\download-upx.ps1" }
if ((Get-FileHash -LiteralPath $officialSource -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'a7d457be4ef942e46844ee8f301206b111394cbcbde3599747a6904c54ff116b') { throw 'UPX source archive SHA-256 mismatch.' }
New-Item -ItemType Directory -Force "$root\outputs" | Out-Null
Copy-Item -LiteralPath "$root\dist\UpxDrop\UpxDrop.exe" -Destination "$root\outputs\UpxDrop.exe" -Force
Copy-Item -LiteralPath $releaseNotes -Destination "$root\outputs\RELEASE_NOTES.md" -Force
$hash = (Get-FileHash -LiteralPath "$root\outputs\UpxDrop.exe" -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$root\outputs\UpxDrop.exe.sha256", "$hash  UpxDrop.exe`n", [Text.Encoding]::ASCII)
$source = Join-Path $root ('work\source-' + [guid]::NewGuid().ToString('N'))
try {
 New-Item -ItemType Directory -Force $source, "$source\tools" | Out-Null
 foreach ($name in @('src','tests','docs','.github','app.manifest','build.ps1','package.ps1','download-upx.ps1','test-native.ps1','LICENSE','README.md','THIRD_PARTY_NOTICES.md','.gitignore')) {
  Copy-Item -LiteralPath (Join-Path $root $name) -Destination $source -Recurse -Force
 }
 Copy-Item -LiteralPath "$root\tools\UPX-LICENSE", $officialSource -Destination "$source\tools" -Force
 Compress-Archive -Path "$source\*" -DestinationPath "$root\outputs\UpxDrop-$version-source.zip" -Force
} finally {
 $resolvedSource = [IO.Path]::GetFullPath($source)
 $workRoot = [IO.Path]::GetFullPath((Join-Path $root 'work')) + [IO.Path]::DirectorySeparatorChar
 if (!$resolvedSource.StartsWith($workRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source staging path is outside work directory.' }
 if (Test-Path -LiteralPath $resolvedSource) { Remove-Item -LiteralPath $resolvedSource -Recurse -Force }
}
Write-Host "Single-file app: outputs/UpxDrop.exe; publish its checksum and corresponding source archive with each Release."
