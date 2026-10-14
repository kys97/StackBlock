$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path $projectRoot 'Temp/MaterialValidation'
$report = Get-Content "$projectRoot/Logs/material-applied.json" -Raw | ConvertFrom-Json
$proof = Get-Content "$projectRoot/Logs/material-apply-validation.txt" -Raw
if (!$report.applied -or $report.remapCount -ne 0 -or !$proof.Contains('every planned slot')) { throw 'Validated no-remap application is required.' }
$baseline = Get-Content "$projectRoot/Logs/material-no-remap-baseline.json" -Raw | ConvertFrom-Json
foreach ($entry in $baseline | Where-Object { $_.path -match '^Assets[\\/](Resources|Scenes)[\\/]' }) {
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot $entry.path)) -or (Get-FileHash -LiteralPath (Join-Path $projectRoot $entry.path)).Hash -ne $entry.hash) {
        throw "Original asset changed during validation: $($entry.path)"
    }
}
$prefabs = @($report.prefabs | ForEach-Object { $_ -replace ' \(slots=\d+\)$', '' })
$materials = @($report.materials | Where-Object { $_.StartsWith('EXTRACT ') } | ForEach-Object { ($_ -split ' -> ', 2)[1] })
$newFiles = @($materials | ForEach-Object { $_ + '.meta'; $_ })
$folders = @($materials | ForEach-Object { (Split-Path $_ -Parent).Replace('\','/') } | Sort-Object -Unique)
foreach ($folder in $folders) { if (!(Test-Path -LiteralPath (Join-Path $projectRoot $folder))) { $newFiles += $folder + '.meta' } }
foreach ($path in @($prefabs) + @($newFiles)) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $projectRoot $path))
    if (!$absolute.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $path.Contains('..')) { throw "Invalid path: $path" }
    if ($path -notmatch '^Assets/Resources/(Complete|Puzzle|Ground)/.+\.prefab$' -and $path -notmatch '^Assets/Resources/Fbx/.+_Materials(?:/[^/]+\.mat)?\.?(?:meta)?$') { throw "Unexpected output: $path" }
    if (!(Test-Path -LiteralPath (Join-Path $validationRoot $path))) { throw "Missing validated file: $path" }
}
foreach ($path in $newFiles) { if (Test-Path -LiteralPath (Join-Path $projectRoot $path)) { throw "Refusing to overwrite: $path" } }
$backup = Join-Path $projectRoot ('Library/StackBlockMaterialBackups/promoted-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach ($path in $prefabs) {
    $target = Join-Path $backup $path
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot $path) -Destination $target
}
$written = [Collections.Generic.List[string]]::new()
try {
    foreach ($folder in $folders) { New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot $folder) | Out-Null }
    # Install GUID metadata before materials; prefab references are copied last.
    foreach ($path in @($newFiles | Where-Object { $_.EndsWith('.meta') }) + @($newFiles | Where-Object { !$_.EndsWith('.meta') })) {
        $written.Add($path)
        Copy-Item -LiteralPath (Join-Path $validationRoot $path) -Destination (Join-Path $projectRoot $path)
    }
    foreach ($path in $prefabs) { Copy-Item -LiteralPath (Join-Path $validationRoot $path) -Destination (Join-Path $projectRoot $path) -Force }
    foreach ($path in @($prefabs) + @($newFiles)) {
        if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $path)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $validationRoot $path)).Hash) { throw "Copy verification failed: $path" }
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $backup 'operation.json')
    $newFiles | Set-Content (Join-Path $backup 'created-files.txt')
    "PASS: promoted $($materials.Count) new materials and $($prefabs.Count) prefabs; every byte matches Unity-validated outputs. Backup: $backup" | Set-Content "$projectRoot/Logs/material-promotion-validation.txt"
}
catch {
    foreach ($path in $prefabs) { Copy-Item -LiteralPath (Join-Path $backup $path) -Destination (Join-Path $projectRoot $path) -Force }
    foreach ($path in $written) { Remove-Item -LiteralPath (Join-Path $projectRoot $path) -Force -ErrorAction SilentlyContinue }
    throw
}
Get-Content "$projectRoot/Logs/material-promotion-validation.txt"
