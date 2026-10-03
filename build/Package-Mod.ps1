[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][string] $AssemblyName,
    [Parameter(Mandatory = $true)][string] $Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path
$fileNames = @('Info.json', "$AssemblyName.dll", 'Newtonsoft.Json.dll')
foreach ($fileName in $fileNames) {
    if (-not (Test-Path -LiteralPath (Join-Path $outputPath $fileName) -PathType Leaf)) {
        throw "Missing required mod package file: $fileName"
    }
}
$manifest = Get-Content -LiteralPath (Join-Path $outputPath 'Info.json') -Raw | ConvertFrom-Json
if ($manifest.Version -ne $Version -or $manifest.AssemblyName -ne "$AssemblyName.dll") {
    throw 'The generated Info.json does not match the built assembly name/version.'
}

$zipPath = Join-Path $outputPath "${AssemblyName}_${Version}.zip"
# Build separately so a failed compression leaves the previous archive intact.
$temporaryPath = Join-Path $outputPath ('.package-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    $archive = [System.IO.Compression.ZipFile]::Open($temporaryPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($fileName in $fileNames) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, (Join-Path $outputPath $fileName), $fileName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporaryPath -Destination $zipPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath }
}
