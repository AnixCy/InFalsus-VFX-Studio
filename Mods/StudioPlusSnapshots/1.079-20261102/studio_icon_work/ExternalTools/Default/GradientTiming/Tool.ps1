param([Parameter(Mandatory=$true)][string]$RequestPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
try {
 Add-Type -Path @((Join-Path $PSScriptRoot 'Core.cs'),(Join-Path $PSScriptRoot 'Generator.cs')) -ReferencedAssemblies @('System.dll','System.Core.dll','System.Drawing.dll','System.Web.Extensions.dll')
 [InfStudio.External.Generator]::Run($RequestPath,$OutputPath,$PSScriptRoot)
} catch { [Console]::Error.WriteLine($_.Exception.ToString()); exit 1 }
