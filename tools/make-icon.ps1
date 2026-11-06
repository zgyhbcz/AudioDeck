param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) 'app.ico')
)

Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = New-Object System.Drawing.Bitmap($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.RectangleF(8, 8, 240, 240)
$radius = 54
$diameter = $radius * 2
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($rect.X, $rect.Y, $diameter, $diameter, 180, 90)
$path.AddArc($rect.Right - $diameter, $rect.Y, $diameter, $diameter, 270, 90)
$path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
$path.CloseFigure()

$blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(15, 108, 189))
$graphics.FillPath($blue, $path)

$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$points = [System.Drawing.PointF[]]@(
    (New-Object System.Drawing.PointF(52, 104)),
    (New-Object System.Drawing.PointF(92, 104)),
    (New-Object System.Drawing.PointF(136, 60)),
    (New-Object System.Drawing.PointF(136, 196)),
    (New-Object System.Drawing.PointF(92, 152)),
    (New-Object System.Drawing.PointF(52, 152))
)
$graphics.FillPolygon($white, $points)

$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 13)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($pen, 132, 94, 62, 68, -50, 100)
$graphics.DrawArc($pen, 118, 74, 98, 108, -50, 100)

$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $stream.ToArray()

$file = [System.IO.File]::Create($Output)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]$png.Length)
$writer.Write([UInt32]22)
$writer.Write($png)
$writer.Flush()
$writer.Close()

$pen.Dispose()
$white.Dispose()
$blue.Dispose()
$path.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$stream.Dispose()

Write-Host "Wrote $Output ($($png.Length) bytes PNG payload)"
