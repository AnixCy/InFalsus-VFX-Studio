param([Parameter(Mandatory=$true)][string]$Editor,[string]$Icon=(Join-Path $PSScriptRoot '..\..\..\..\Branding\app-icon.ico'))
$ErrorActionPreference='Stop'
$target=(Resolve-Path -LiteralPath $Editor).Path.TrimEnd('\');$exe=Join-Path $target 'InFalsusStudio.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Select an InFalsus Studio editor folder'}
if(@(Get-Process -Name InFalsusStudio -ErrorAction SilentlyContinue | Where-Object {!$_.Path -or $_.Path -eq $exe}).Count){throw 'Save and close the selected editor before updating its icon'}
if((Get-AuthenticodeSignature -LiteralPath $exe).Status -ne 'NotSigned'){throw 'This helper only handles the unsigned editor target'}
if(!('ExeIconResources' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'ExeIconResources.cs')}
$beforeHash=(Get-FileHash -LiteralPath $exe).Hash;$before=[ExeIconResources]::Sections($exe)
$temporary=Join-Path $target ('.infstudioplus-icon-'+[Guid]::NewGuid().ToString('N')+'.exe')
$backup=Join-Path $target ('VisualUIData\backups\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'-infstudioplus-icon')
$changed=$false
try{
 Copy-Item -LiteralPath $exe -Destination $temporary
 [ExeIconResources]::Replace($temporary,(Resolve-Path -LiteralPath $Icon).Path)
 $after=[ExeIconResources]::Sections($temporary)
 foreach($section in $before | Where-Object Name -ne '.rsrc'){$other=@($after | Where-Object Name -eq $section.Name);if($other.Count -ne 1 -or $other[0].Hash -ne $section.Hash){throw ('Non-resource section changed: '+$section.Name)}}
 if((Get-FileHash -LiteralPath $exe).Hash -ne $beforeHash){throw 'The editor executable changed during preparation'}
 $newHash=(Get-FileHash -LiteralPath $temporary).Hash
 New-Item -ItemType Directory -Path $backup | Out-Null
 [IO.File]::Replace($temporary,$exe,(Join-Path $backup 'InFalsusStudio.exe'));$changed=$true
 if((Get-FileHash -LiteralPath $exe).Hash -ne $newHash){throw 'Installed executable checksum mismatch'}
 [ordered]@{before=$beforeHash;after=$newHash;icon=(Get-FileHash -LiteralPath $Icon).Hash;nonIconResourcesUnchanged=$true;nonResourceSectionsUnchanged=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'icon-change.json') -Encoding utf8
 Write-Output ('Updated icon. Backup: '+$backup)
}catch{if($changed){Copy-Item -LiteralPath (Join-Path $backup 'InFalsusStudio.exe') -Destination $exe -Force};throw}
finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}}
