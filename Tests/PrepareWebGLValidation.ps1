$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path $projectRoot 'Temp/WebGLValidation'
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Editor", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/*" "$validationRoot/Assets" -Recurse -Force
Copy-Item "$projectRoot/Packages/manifest.json", "$projectRoot/Packages/packages-lock.json" "$validationRoot/Packages" -Force
Copy-Item "$projectRoot/ProjectSettings/*" "$validationRoot/ProjectSettings" -Force
Copy-Item "$PSScriptRoot/ProjectValidationEditor.cs" "$validationRoot/Assets/Editor" -Force
Write-Output $validationRoot
