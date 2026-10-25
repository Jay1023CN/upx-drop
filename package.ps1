# SPDX-License-Identifier: GPL-2.0-or-later
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& "$root\build.ps1"
$package = Join-Path $root ('work\package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $package, "$package\source", "$root\outputs" | Out-Null
Copy-Item -Path "$root\dist\UpxDrop\*" -Destination $package -Recurse -Force
foreach ($name in @('src','tests','.github','app.manifest','build.ps1','package.ps1','download-upx.ps1','LICENSE','README.md','THIRD_PARTY_NOTICES.md','.gitignore')) {
 Copy-Item -LiteralPath (Join-Path $root $name) -Destination "$package\source" -Recurse -Force
}
New-Item -ItemType Directory -Force "$package\source\tools" | Out-Null
Copy-Item -LiteralPath "$root\tools\UPX-LICENSE" -Destination "$package\source\tools" -Force
# Keep matching UPX source alongside any bundled UPX binary.
if ((Test-Path "$package\tools\upx.exe") -and !(Test-Path "$package\tools\upx-5.2.1-src.tar.xz")) { throw 'Bundled UPX requires its matching source archive. Run download-upx.ps1 first.' }
Compress-Archive -Path "$package\*" -DestinationPath "$root\outputs\UpxDrop-1.0.0.zip" -Force
Write-Host "Package: $root\outputs\UpxDrop-1.0.0.zip"
