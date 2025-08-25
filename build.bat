@echo off
echo.
echo =================================================
echo           Building CodeSnip Release
echo =================================================
echo.
echo This script will build the project from the 'src' directory
echo and create a self-contained, single-file executable
echo for Windows (x64) in the 'release' directory at the project root.
echo.
echo Make sure you have the .NET 8 SDK installed.
echo.

REM Define source and output directories
set "SRC_DIR=src"
set "OUTPUT_DIR=release"
set "PROJECT_PATH=%SRC_DIR%\CodeSnip\CodeSnip.csproj"

REM Check if the source directory exists
if not exist "%SRC_DIR%" (
    echo ERROR: Source directory '%SRC_DIR%' not found.
    echo Please run this script from the root directory that contains the 'src' folder.
    pause
    goto :eof
)

REM Clean the previous release directory if it exists
if exist "%OUTPUT_DIR%" (
    echo Deleting old release directory...
    rmdir /s /q "%OUTPUT_DIR%"
)

echo.
echo Publishing the application...
echo.

dotnet publish "%PROJECT_PATH%" -c Release -r win-x64 --self-contained true -o "%OUTPUT_DIR%" /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
echo.

if %errorlevel% neq 0 (
    echo.
    echo  BUILD FAILED! Please check the output above for errors.
    goto :eof
)

echo.
echo Copying external formatters...
xcopy "%SRC_DIR%\CodeSnip\Tools" "%OUTPUT_DIR%\Tools\" /E /I /Y /Q > nul
if %errorlevel% neq 0 (
    echo.
    echo  FAILED TO COPY TOOLS! Check permissions and paths.
    goto :eof
)
echo Tools copied successfully.
echo.

echo  BUILD SUCCESSFUL! The application is in the '%cd%\%OUTPUT_DIR%' directory.
echo.
pause

