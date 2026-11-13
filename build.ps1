$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

dotnet build (Join-Path $root 'src\FarFlora.csproj') -c Release -o (Join-Path $root 'out\bin') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

$zip = Join-Path $root 'out\farflora_1.0.0.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($name in @('FarFlora.dll', 'modinfo.json')) {
        $src = Join-Path $root "out\bin\$name"
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $src, $name, [System.IO.Compression.CompressionLevel]::Optimal)
    }
} finally {
    $archive.Dispose()
}

$f = Get-Item $zip
$h = (Get-FileHash $zip -Algorithm SHA256).Hash
"zip: $($f.FullName)  size=$($f.Length)  sha256=$h"
