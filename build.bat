@echo off
rem Builds a ready-to-use, self-contained Phast.exe into the "dist" folder.
rem Requirements: .NET 8 SDK. The user only needs Windows 10/11 with the WebView2 Runtime (built into Windows 11).
setlocal
cd /d "%~dp0"

if exist dist rmdir /s /q dist

dotnet publish src\Phast\Phast.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishReadyToRun=true ^
  -p:DebugType=none ^
  -o dist
if errorlevel 1 (
  echo.
  echo *** Build failed ***
  exit /b 1
)

copy /y LICENSE dist\LICENSE.txt >nul
copy /y THIRD_PARTY_NOTICES.md dist\THIRD_PARTY_NOTICES.md >nul

echo.
echo Phast built successfully: %cd%\dist\Phast.exe
endlocal
