@echo off
rem Builds the reader (x86 + x64) and the LiveSplit component into build\Components.
setlocal
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "T6R_VS=%%i"
if not defined T6R_VS (
    echo Install Visual Studio Build Tools with Desktop development with C++.
    exit /b 1
)
rem VsDevCmd looks for vswhere on PATH.
set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
pushd "%~dp0"
if not exist build\obj\x86 mkdir build\obj\x86
if not exist build\obj\x64 mkdir build\obj\x64
if not exist build\Components\x86 mkdir build\Components\x86
if not exist build\Components\x64 mkdir build\Components\x64

call :native x86 || goto failed
call :native x64 || goto failed

dotnet build component\LiveSplit.T6ZM.csproj -c Release -nologo -v:minimal -o build\Components
if errorlevel 1 goto failed
echo Built build\Components
popd
exit /b 0

:native
setlocal
call "%T6R_VS%\Common7\Tools\VsDevCmd.bat" -arch=%1 -host_arch=x64 >nul
if errorlevel 1 exit /b 1
set "CFLAGS=/nologo /std:c++20 /EHsc /O2 /MT /W4 /WX /DNDEBUG /DWIN32 /D_WINDOWS /Brepro"
cl %CFLAGS% /LD /Fobuild\obj\%1\ /Febuild\Components\%1\t6zm_reader.dll reader\src\reader.cpp /link /INCREMENTAL:NO /Brepro || exit /b 1
endlocal
exit /b 0

:failed
popd
exit /b 1
