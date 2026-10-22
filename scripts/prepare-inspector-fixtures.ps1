param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out = Join-Path $repo 'artifacts\inspector-media'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(128,96,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.FillRectangle([Drawing.Brushes]::Blue,0,0,64,96)
    $bitmap.Save((Join-Path $out 'alpha.png'),[Drawing.Imaging.ImageFormat]::Png)
    $graphics.Clear([Drawing.Color]::Red)
    $bitmap.Save((Join-Path $out 'background.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $bitmap.Dispose() }
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
foreach ($codec in @('vp8','vp9')) {
    $encoder = if ($codec -eq 'vp8') { 'libvpx' } else { 'libvpx-vp9' }
    & $ffmpeg -hide_banner -loglevel error -y -loop 1 -i (Join-Path $out 'alpha.png') -t 1 -r 12 -c:v $encoder -pix_fmt yuva420p -auto-alt-ref 0 -an (Join-Path $out ($codec+'.webm'))
    if ($LASTEXITCODE -ne 0) { throw 'Unable to encode video fixture.' }
}
[IO.File]::WriteAllText((Join-Path $out 'invalid.webm'),'This is not a WebM video.')
Write-Output "Created opaque background and VP8/VP9 per-pixel-alpha fixtures at $out."
