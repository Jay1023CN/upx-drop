# SPDX-License-Identifier: GPL-2.0-or-later
# Regenerate the Windows icon and PNG preview from the shared vector source.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$inputStream = [IO.File]::OpenRead((Join-Path $PSScriptRoot 'AppIcon.xaml'))
try { $drawing = [Windows.Markup.XamlReader]::Load($inputStream) } finally { $inputStream.Dispose() }
$drawing.Freeze()
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
foreach ($size in ($sizes + 512)) {
 $visual = New-Object Windows.Media.DrawingVisual
 $context = $visual.RenderOpen()
 $context.DrawImage($drawing, (New-Object Windows.Rect(0,0,$size,$size)))
 $context.Close()
 $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($visual)
 $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream = New-Object IO.MemoryStream
 try {
  $encoder.Save($stream)
  if ($size -eq 512) { [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'AppIcon.png'), $stream.ToArray()) }
  else { $frames += ,$stream.ToArray() }
 } finally { $stream.Dispose() }
}
$output = [IO.File]::Create((Join-Path $PSScriptRoot 'AppIcon.ico'))
$writer = New-Object IO.BinaryWriter($output)
try {
 $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
 $offset = 6 + 16 * $sizes.Count
 for ($i=0; $i -lt $sizes.Count; $i++) {
  $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
  $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
  $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32)
  $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
  $offset += $frames[$i].Length
 }
 foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
Write-Output 'Generated AppIcon.ico (16-256 px) and AppIcon.png (512 px).'
