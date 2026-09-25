$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path $projectRoot 'Temp/FullFlowValidation'
New-Item -ItemType Directory -Force -Path $validationRoot, "$validationRoot/Assets/Tests", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/*" "$validationRoot/Assets" -Recurse -Force
# The validation copy is reused; remove only explicitly retired script files.
foreach ($retiredScript in @('CameraControl.cs', 'CameraControl.cs.meta', 'PointMove.cs', 'PointMove.cs.meta')) {
    $retiredPath = Join-Path $validationRoot "Assets/Scripts/$retiredScript"
    if (Test-Path -LiteralPath $retiredPath) { Remove-Item -LiteralPath $retiredPath }
}
Copy-Item "$projectRoot/Packages/manifest.json", "$projectRoot/Packages/packages-lock.json" "$validationRoot/Packages" -Force
Copy-Item "$projectRoot/ProjectSettings/*" "$validationRoot/ProjectSettings" -Force
Copy-Item "$PSScriptRoot/FullFlowTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/StartupRegressionTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/StageDataTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/PuzzleLightingCaptureTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/GameSessionTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/PuzzleTimerTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/PuzzleCameraTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/PauseTests.cs" "$validationRoot/Assets/Tests" -Force
Copy-Item "$PSScriptRoot/TestState.cs", "$PSScriptRoot/FinalRegressionTests.cs" "$validationRoot/Assets/Tests" -Force
'{"name":"FullFlow.Runtime","references":["UnityEngine.UI"]}' | Set-Content "$validationRoot/Assets/Scripts/FullFlow.Runtime.asmdef"
'{"name":"FullFlow.Tests","references":["FullFlow.Runtime","UnityEngine.UI"],"optionalUnityReferences":["TestAssemblies"]}' | Set-Content "$validationRoot/Assets/Tests/FullFlow.Tests.asmdef"
# Exercise the real Unity scene serializer with the OLD field names and non-default values.
$legacyScene = [IO.File]::ReadAllText("$projectRoot/Assets/Scenes/Main.unity")
$legacySelectionPattern = '  (?:topic|initialTopic): \d+\r?\n  (?:stage|initialStage): \d+'
if ($legacyScene -notmatch $legacySelectionPattern) { throw 'Update the legacy-selection fixture for the saved Main scene.' }
$legacyScene = $legacyScene -replace $legacySelectionPattern, "  topic: 1`n  stage: 7"
[IO.File]::WriteAllText("$validationRoot/Assets/Scenes/LegacySelectionValidation.unity", $legacyScene)
$legacyGuid = [guid]::NewGuid().ToString('N')
"fileFormatVersion: 2`nguid: $legacyGuid`nDefaultImporter:`n  externalObjects: {}`n  userData:`n  assetBundleName:`n  assetBundleVariant:" | Set-Content "$validationRoot/Assets/Scenes/LegacySelectionValidation.unity.meta"
$validationBuildSettings = [IO.File]::ReadAllText("$validationRoot/ProjectSettings/EditorBuildSettings.asset")
$validationBuildSettings = $validationBuildSettings.Replace('  m_configObjects:', "  - enabled: 1`n    path: Assets/Scenes/LegacySelectionValidation.unity`n    guid: $legacyGuid`n  m_configObjects:")
[IO.File]::WriteAllText("$validationRoot/ProjectSettings/EditorBuildSettings.asset", $validationBuildSettings)
Write-Output $validationRoot
