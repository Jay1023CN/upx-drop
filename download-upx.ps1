# SPDX-License-Identifier: GPL-2.0-or-later
param([string]$Destination = (Join-Path $PSScriptRoot 'tools'))
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$assets = @(
 @{ Name='upx-5.2.1-win64.zip'; Hash='eabc6792a347d45e945be7748423e7868fd01b0d2bcaa2f4b1031fd71ff69bda' },
 @{ Name='upx-5.2.1-src.tar.xz'; Hash='a7d457be4ef942e46844ee8f301206b111394cbcbde3599747a6904c54ff116b' }
)
foreach ($asset in $assets) {
 $path = Join-Path $Destination $asset.Name
 Invoke-WebRequest -UseBasicParsing -Uri ('https://github.com/upx/upx/releases/download/v5.2.1/' + $asset.Name) -OutFile $path
 if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.Hash) { throw "SHA-256 mismatch: $($asset.Name)" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $Destination $assets[0].Name))
try {
 foreach ($name in @('upx.exe','LICENSE','COPYING')) {
  $entry = $archive.GetEntry('upx-5.2.1-win64/' + $name)
  if (!$entry) { throw "Missing official archive entry: $name" }
  $outName = if ($name -eq 'LICENSE') { 'UPX-LICENSE' } else { $name }
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $Destination $outName), $true)
 }
} finally { $archive.Dispose() }
Write-Host 'Official UPX 5.2.1 and corresponding source downloaded and verified.'
