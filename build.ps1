param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler not found.' }
$output = Join-Path $root 'dist\UpxDrop'
New-Item -ItemType Directory -Force -Path $output | Out-Null
New-Item -ItemType Directory -Force -Path "$root\work" | Out-Null
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$output\UpxDrop.exe" "/win32manifest:$root\app.manifest" "/resource:$root\src\MainWindow.xaml,MainWindow.xaml" @references "$root\src\App.cs" "$root\src\Core.cs" "$root\src\EngineDownload.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
foreach ($file in @('LICENSE','README.md','THIRD_PARTY_NOTICES.md')) { if (Test-Path "$root\$file") { Copy-Item -LiteralPath "$root\$file" -Destination $output -Force } }
if (Test-Path "$root\tools") { Copy-Item -LiteralPath "$root\tools" -Destination $output -Recurse -Force }
if ($Test) {
 & $compiler /nologo /target:exe /utf8output "/out:$root\work\CoreTests.exe" "$root\src\Core.cs" "$root\tests\CoreTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
 & "$root\work\CoreTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
 & $compiler /nologo /target:exe /utf8output "/out:$root\work\UiSmoke.exe" @references "$root\tests\UiSmoke.cs"
 if ($LASTEXITCODE -ne 0) { throw 'UI smoke compilation failed.' }
 & "$root\work\UiSmoke.exe" "$root\src\MainWindow.xaml" "$root\work\preview.png"
 if ($LASTEXITCODE -ne 0) { throw 'UI smoke test failed.' }
}
Write-Host "Built: $output\UpxDrop.exe"
