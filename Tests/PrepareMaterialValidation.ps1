$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path $projectRoot 'Temp/MaterialValidation'
if (Test-Path "$validationRoot/Assets") { throw 'Use the existing validation snapshot deliberately; this preparation does not overwrite it.' }
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/*" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$projectRoot/Packages/manifest.json", "$projectRoot/Packages/packages-lock.json" "$validationRoot/Packages" -Force
Copy-Item "$projectRoot/ProjectSettings/*" "$validationRoot/ProjectSettings" -Force
Copy-Item "$PSScriptRoot/MaterialUtilityValidation.cs" "$validationRoot/Assets/Editor" -Force
Get-ChildItem "$projectRoot/Assets/Resources" -Recurse -File | Get-FileHash |
    Select-Object Path, Hash | ConvertTo-Json | Set-Content "$projectRoot/Logs/material-source-baseline.json"
Get-ChildItem "$projectRoot/Assets" -Recurse -File | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName.Substring($projectRoot.Length + 1); hash = (Get-FileHash -LiteralPath $_.FullName).Hash }
} | ConvertTo-Json -Depth 3 | Set-Content "$projectRoot/Logs/material-no-remap-baseline.json"
Write-Output $validationRoot
