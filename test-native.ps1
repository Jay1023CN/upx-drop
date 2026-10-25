# SPDX-License-Identifier: GPL-2.0-or-later
# Optional Windows integration checks. Run in a Visual C++ developer shell.
$ErrorActionPreference = 'Stop'
try {
$root = $PSScriptRoot

New-Item -ItemType Directory -Force "$root\work" | Out-Null
if (Get-Command cl.exe -ErrorAction SilentlyContinue) {
& cl.exe /nologo /O2 /MT "/Fe:$root\work\NativeFixture.exe" "/Fo:$root\work\NativeFixture.obj" "$root\tests\NativeFixture.c"
} elseif (Get-Command gcc.exe -ErrorAction SilentlyContinue) {
 & gcc.exe -O2 -static "-Wl,--dynamicbase" -o "$root\work\NativeFixture.exe" "$root\tests\NativeFixture.c"
} else { throw 'Native integration tests require MSVC or MinGW GCC.' }
if ($LASTEXITCODE -ne 0) { throw 'Native fixture compilation failed.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /utf8output /codepage:65001 "/out:$root\work\RealEngineTests.exe" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$root\tools\upx-5.2.1-win64.zip,upx.zip" "$root\src\Core.cs" "$root\src\EngineDownload.cs" "$root\tests\RealEngineTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Real engine test compilation failed.' }
& "$root\work\RealEngineTests.exe" "$root\work\NativeFixture.exe"
if ($LASTEXITCODE -ne 0) { throw 'Real Windows UPX integration tests failed.' }
} catch {
 $message = ($_.ToString() + "`n" + $_.ScriptStackTrace).Replace('%','%25').Replace("`r",'%0D').Replace("`n",'%0A')
 Write-Output "::error::$message"
 throw
}
