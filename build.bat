@echo off
rem Builds RRACF.exe using the C# compiler that ships with Windows - no SDK needed.
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find the Windows C# compiler at:
    echo   %CSC%
    exit /b 1
)

pushd "%~dp0"

rem The icon is cosmetic, but a desktop app with none looks unfinished - and some antivirus
rem heuristics weight a missing icon and missing version info together.
rem src\RRACF.ico is the tracked copy, so a fresh clone builds an identical exe. The artwork
rem folder is a fallback and is not in the repository.
set ICON=
if exist "Textures Pictures etc\RRACF.ico" set ICON=/win32icon:"Textures Pictures etc\RRACF.ico"
if exist "src\RRACF.ico" set ICON=/win32icon:"src\RRACF.ico"

rem NOTE: builds are NOT reproducible. The in-box compiler predates /deterministic (it rejects the
rem flag outright), so every build stamps a fresh module GUID and timestamp and produces a
rem different exe from identical source. A rebuild is therefore a brand-new unknown file to any
rem antivirus, which resets whitelisting and makes a previously submitted hash meaningless.
rem Practical consequence: scan and submit the exact exe you ship, and do not rebuild afterwards.
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ %ICON% ^
    /out:RRACF.exe ^
    /reference:System.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    src\AssemblyInfo.cs ^
    src\Program.cs ^
    src\MainForm.cs ^
    src\Pipeline.cs ^
    src\UAsset.cs ^
    src\Crc.cs ^
    src\CamoMap.cs ^
    src\Manifest.cs ^
    src\SlotFile.cs ^
    src\Abilities.cs ^
    src\SaveLoad.cs ^
    src\Terrain.cs ^
    src\Settings.cs ^
    src\Tools.cs

if errorlevel 1 (
    echo.
    echo BUILD FAILED
    popd
    exit /b 1
)

echo.
echo Built RRACF.exe
popd
endlocal
