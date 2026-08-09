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

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
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
