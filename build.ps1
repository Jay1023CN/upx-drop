# SPDX-License-Identifier: GPL-2.0-or-later
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler required on Windows.' }
$archive = Join-Path $root 'tools\upx-5.2.1-win64.zip'
if (!(Test-Path -LiteralPath $archive)) { & "$root\download-upx.ps1" }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'eabc6792a347d45e945be7748423e7868fd01b0d2bcaa2f4b1031fd71ff69bda') { throw 'Embedded UPX archive SHA-256 mismatch.' }
$output = Join-Path $root 'dist\UpxDrop'
New-Item -ItemType Directory -Force -Path $output, "$root\work" | Out-Null
$references = @('System.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$resources = @("/resource:$root\src\Assets\AppIcon.xaml,AppIcon.xaml", "/resource:$root\src\MainWindow.xaml,MainWindow.xaml", "/resource:$archive,upx.zip", "/resource:$root\LICENSE,LICENSE", "/resource:$root\THIRD_PARTY_NOTICES.md,THIRD_PARTY_NOTICES.md", "/resource:$root\tools\UPX-LICENSE,UPX-LICENSE")
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /codepage:65001 "/out:$output\UpxDrop.exe" "/win32manifest:$root\app.manifest" "/win32icon:$root\src\Assets\AppIcon.ico" @resources @references "$root\src\Queue.cs" "$root\src\App.cs" "$root\src\Appearance.cs" "$root\src\Tutorial.cs" "$root\src\Core.cs" "$root\src\EngineDownload.cs" "$root\src\Updater.cs" "$root\src\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\QueueTests.exe" "$root\src\Queue.cs" "$root\tests\QueueTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Queue test compilation failed.' }
 & "$root\work\QueueTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Queue tests failed.' }
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\CoreTests.exe" "$root\src\Core.cs" "$root\tests\CoreTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed.' }
 & "$root\work\CoreTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\UpdaterTests.exe" /reference:System.Web.Extensions.dll "$root\src\Updater.cs" "$root\tests\UpdaterTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Updater test compilation failed.' }
 & "$root\work\UpdaterTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Updater tests failed.' }
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\EngineTests.exe" @references "/resource:$archive,upx.zip" "$root\src\EngineDownload.cs" "$root\tests\EngineTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Engine test compilation failed.' }
 & "$root\work\EngineTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Engine tests failed.' }
 foreach ($fixture in @(@{Name='UpdateFixture';Define='ORIGINAL'}, @{Name='UpdatedFixture';Define='UPDATED'})) {
  & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\$($fixture.Name).exe" "/define:$($fixture.Define)" /reference:System.Web.Extensions.dll "$root\src\Updater.cs" "$root\tests\UpdateFixture.cs"
  if ($LASTEXITCODE -ne 0) { throw 'Update fixture compilation failed.' }
 }
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\WindowsUpdateTests.exe" /reference:System.Web.Extensions.dll "$root\src\Updater.cs" "$root\tests\WindowsUpdateTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'Windows update test compilation failed.' }
 & "$root\work\WindowsUpdateTests.exe" "$root\work\UpdateFixture.exe" "$root\work\UpdatedFixture.exe"
 if ($LASTEXITCODE -ne 0) { throw 'Windows update replacement/rollback tests failed.' }
 & $compiler /nologo /target:exe /main:UiFlowTests /utf8output /codepage:65001 "/out:$root\work\UiFlowTests.exe" @resources @references "$root\src\App.cs" "$root\src\Appearance.cs" "$root\src\Tutorial.cs" "$root\src\Queue.cs" "$root\src\Core.cs" "$root\src\EngineDownload.cs" "$root\src\Updater.cs" "$root\tests\UiFlowTests.cs"
 if ($LASTEXITCODE -ne 0) { throw 'UI flow test compilation failed.' }
 & "$root\work\UiFlowTests.exe" "$root\work\CoreTests.exe"
 if ($LASTEXITCODE -ne 0) { throw 'UI flow tests failed.' }
 & $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\UiSmoke.exe" @references "$root\tests\UiSmoke.cs"
 if ($LASTEXITCODE -ne 0) { throw 'UI smoke compilation failed.' }
 & "$root\work\UiSmoke.exe" "$root\src\MainWindow.xaml" "$root\work\preview.png" "$output\UpxDrop.exe"
 if ($LASTEXITCODE -ne 0) { throw 'UI smoke test failed.' }
}
Write-Host "Built single executable: $output\UpxDrop.exe"
