param([string]$Target='E:\RhythmGame\Infstudio\InFalsusStudio_1.0.10',[switch]$Preflight)
$ErrorActionPreference='Stop'
$taskTarget=(Resolve-Path -LiteralPath $Target).Path.TrimEnd('\')
function SafePath([string]$relative){$taskPath=[IO.Path]::GetFullPath((Join-Path $taskTarget $relative));if([IO.Path]::IsPathRooted($relative) -or !$taskPath.StartsWith($taskTarget+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Payload path leaves selected editor copy'};return $taskPath}
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
$taskJournal=SafePath 'VisualUIData\installed.json'
$taskRecord=Get-Content -LiteralPath $taskJournal -Raw -Encoding UTF8 | ConvertFrom-Json
$taskDllRelative='BepInEx\plugins\InfStudioVisualUI\InfStudioVisualUI.dll'
$taskSourceDll=Join-Path $PSScriptRoot 'Source\InfStudioVisualUI.dll'
$taskNewHash=Hash $taskSourceDll
if($Preflight){
    [pscustomobject]@{
        target=$taskTarget
        newDll=$taskSourceDll
        newHash=$taskNewHash
    }
    return
}
$timestamp=(Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$taskBackupRelative="VisualUIData\backups\${timestamp}-infstudioplus-gameplay-fixes_20261101"
$taskBackup=SafePath $taskBackupRelative
New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
$taskBackupFiles=Join-Path $taskBackup "files\$taskDllRelative"
New-Item -ItemType Directory -Path (Split-Path $taskBackupFiles -Parent) -Force | Out-Null

$taskDestDll=SafePath $taskDllRelative
if(Test-Path -LiteralPath $taskDestDll){
    Copy-Item -LiteralPath $taskDestDll -Destination $taskBackupFiles -Force
}
$cfgPath=SafePath 'BepInEx\config\local.infalsusstudio.visualui.cfg'
if(Test-Path -LiteralPath $cfgPath){
    Copy-Item -LiteralPath $cfgPath -Destination (Join-Path $taskBackup 'previous-visualui.cfg') -Force
}
Copy-Item -LiteralPath $taskJournal -Destination (Join-Path $taskBackup 'previous-installation.json') -Force

# Overwrite target DLL
Copy-Item -LiteralPath $taskSourceDll -Destination $taskDestDll -Force
$installedHash=Hash $taskDestDll
if($installedHash -ne $taskNewHash){
    throw "Checksum verification failed after install: $installedHash vs $taskNewHash"
}

# Update installed.json
foreach($f in $taskRecord.files){
    if($f.path -eq $taskDllRelative){
        $f.sha256=$taskNewHash
        $f.changed=$true
        $f.backup="$taskBackupRelative\files\$taskDllRelative"
    }
}
$taskRecord.backup=$taskBackupRelative
$taskRecord.installedAt=[DateTime]::UtcNow.ToString('o')
[IO.File]::WriteAllText($taskJournal,($taskRecord | ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))

Write-Host "Successfully installed InfStudioVisualUI.dll ($taskNewHash) to $taskDestDll"
Write-Host "Backup created at $taskBackup"
