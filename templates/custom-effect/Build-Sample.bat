@echo off
setlocal
cd /d "%~dp0"
set "VFX_SAMPLE_OUTPUT=%~dp0Built\sample-%RANDOM%-%RANDOM%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-CustomEffect.ps1" -ShaderPath "%~dp0templates\custom-effect\ProceduralPulse.shader" -DescriptorPath "%~dp0templates\custom-effect\effect.json" -OutputDirectory "%VFX_SAMPLE_OUTPUT%" -OpenOutput
pause
