@echo off
REM ============================================
REM Segment Application - Release Build Script
REM ============================================
REM This script builds a self-contained, single-file
REM Windows executable for production deployment.
REM ============================================

echo.
echo ========================================
echo   SEGMENT - RELEASE BUILD SCRIPT
echo ========================================
echo.

REM Navigate to the project directory
cd /d "%~dp0"

REM Step 1: Clean the solution
echo [1/3] Cleaning solution...
dotnet clean Segment\Segment.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo ERROR: Clean failed!
    pause
    exit /b 1
)
echo Clean completed successfully.
echo.

REM Step 2: Publish as Self-Contained Single File
echo [2/3] Publishing self-contained single-file executable...
dotnet publish Segment\Segment.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -o Publish

if %ERRORLEVEL% neq 0 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)
echo Publish completed successfully.
echo.

REM Step 3: Display output information
echo [3/3] Build complete!
echo.
echo ========================================
echo   OUTPUT DIRECTORY: %~dp0Publish
echo ========================================
echo.
dir /B Publish\Segment.exe 2>nul
if %ERRORLEVEL% equ 0 (
    for %%F in (Publish\Segment.exe) do echo   Segment.exe - %%~zF bytes
) else (
    echo   WARNING: Segment.exe not found in output!
)
echo.
echo Ready for Inno Setup packaging!
echo Next step: Compile setup.iss using Inno Setup Compiler
echo.
pause
