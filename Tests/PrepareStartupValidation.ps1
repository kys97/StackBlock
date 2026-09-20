$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$validationRoot = Join-Path $projectRoot 'Temp/StartupValidation'
New-Item -ItemType Directory -Force -Path "$validationRoot/Assets/Runtime", "$validationRoot/Assets/Tests", "$validationRoot/Assets/Resources/UI/Number", "$validationRoot/Packages", "$validationRoot/ProjectSettings" | Out-Null
Copy-Item "$projectRoot/Assets/Scripts/*.cs" "$validationRoot/Assets/Runtime"
foreach ($retiredScript in @('CameraControl.cs', 'CameraControl.cs.meta', 'PointMove.cs', 'PointMove.cs.meta')) {
    $retiredPath = Join-Path $validationRoot "Assets/Runtime/$retiredScript"
    if (Test-Path -LiteralPath $retiredPath) { Remove-Item -LiteralPath $retiredPath }
}
Copy-Item "$projectRoot/Assets/Resources/UI/Number/*" "$validationRoot/Assets/Resources/UI/Number"
Copy-Item "$PSScriptRoot/StartupRegressionTests.cs" "$validationRoot/Assets/Tests"
Copy-Item "$PSScriptRoot/TestState.cs" "$validationRoot/Assets/Tests"
Copy-Item "$projectRoot/ProjectSettings/ProjectVersion.txt" "$validationRoot/ProjectSettings"
'{"name":"StartupValidation.Runtime","references":["UnityEngine.UI"]}' | Set-Content "$validationRoot/Assets/Runtime/StartupValidation.Runtime.asmdef"
'{"name":"StartupValidation.Tests","references":["StartupValidation.Runtime","UnityEngine.UI"],"optionalUnityReferences":["TestAssemblies"]}' | Set-Content "$validationRoot/Assets/Tests/StartupValidation.Tests.asmdef"
'{"dependencies":{"com.unity.ugui":"2.5.0","com.unity.test-framework":"1.7.0","com.unity.modules.audio":"1.0.0","com.unity.modules.ui":"1.0.0","com.unity.modules.imgui":"1.0.0"}}' | Set-Content "$validationRoot/Packages/manifest.json"
Write-Output $validationRoot
