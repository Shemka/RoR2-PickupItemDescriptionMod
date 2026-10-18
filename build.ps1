param(
    [string]$GameDirectory = 'C:\Program Files (x86)\Steam\steamapps\common\Risk of Rain 2',
    [string]$BepInExCore = "$PSScriptRoot\.deps\BepInExPack\BepInExPack\BepInEx\core"
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDirectory 'Risk of Rain 2_Data\Managed'
$compiler = Join-Path $PSScriptRoot '.deps\Roslyn\tasks\net472\csc.exe'
if (!(Test-Path $compiler)) { throw 'Missing compiler. See README.md for dependency setup.' }
if (!(Test-Path "$BepInExCore\BepInEx.dll")) { throw "BepInEx.dll not found in $BepInExCore" }
$output = Join-Path $PSScriptRoot 'dist\PickupDescriptionPanel'
New-Item -ItemType Directory -Force $output | Out-Null
$refs = @('mscorlib', 'System', 'System.Core', 'netstandard', 'RoR2', 'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.InputLegacyModule', 'UnityEngine.TextRenderingModule', 'UnityEngine.UI', 'UnityEngine.UNETModule', 'com.unity.multiplayer-hlapi.Runtime') | ForEach-Object { '/reference:' + (Join-Path $managed ($_ + '.dll')) }
$refs += '/reference:' + (Join-Path $BepInExCore 'BepInEx.dll')
& $compiler /nologo /target:library /optimize+ /nostdlib+ /langversion:latest "/out:$output\PickupDescriptionPanel.dll" $refs "$PSScriptRoot\src\PickupDescriptionPanel.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item "$PSScriptRoot\README.md" $output
Copy-Item "$PSScriptRoot\packaging\manifest.json", "$PSScriptRoot\packaging\icon.png" $output
# Keep metadata at the ZIP root so Import Local Mod can identify this package.
$packageFiles = @('PickupDescriptionPanel.dll', 'README.md', 'manifest.json', 'icon.png') | ForEach-Object { Join-Path $output $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath "$PSScriptRoot\dist\PickupDescriptionPanel-1.0.0.zip" -Force
# Include the ready-to-import package in source downloads from GitHub.
$releaseDirectory = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force $releaseDirectory | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot\dist\PickupDescriptionPanel-1.0.0.zip" -Destination $releaseDirectory
Write-Host "Built $output\PickupDescriptionPanel.dll"
