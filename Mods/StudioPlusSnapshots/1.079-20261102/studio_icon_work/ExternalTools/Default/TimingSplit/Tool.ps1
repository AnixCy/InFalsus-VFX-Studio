param([Parameter(Mandatory=$true)][string]$RequestPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
try {
 Add-Type -Path (Join-Path $PSScriptRoot 'Generator.cs') -ReferencedAssemblies @('System.dll','System.Core.dll','System.Web.Extensions.dll')
 [InfStudio.External.TimingSplit]::Run($RequestPath,$OutputPath)
} catch { [Console]::Error.WriteLine($_.Exception.ToString()); exit 1 }
