@echo off

set "AppName=fixeol"
set "Configuration=Release"
set "Runtime=win-x64"

cd /d "%~dp0"
if errorlevel 1 exit /b %errorlevel%

echo dotnet publish ".\%AppName%.csproj" -c %Configuration% -r %Runtime% --self-contained false

dotnet publish ".\%AppName%.csproj" -c %Configuration% -r %Runtime% --self-contained false

if errorlevel 1 exit /b %errorlevel%

@REM Query the evaluated PublishDir before enabling the optional installation below.
@REM for /f "delims=" %%D in ('dotnet msbuild ".\%AppName%.csproj" -nologo -p:Configuration=%Configuration% -p:RuntimeIdentifier=%Runtime% -getProperty:PublishDir') do set "PublishDir=%%D"
@REM if exist "%UserProfile%\Bin\" (
@REM   if not exist "%UserProfile%\Bin\apps\%AppName%\" mkdir "%UserProfile%\Bin\apps\%AppName%\"
@REM   copy /Y "%PublishDir%*" "%UserProfile%\Bin\apps\%AppName%\"
@REM   if %ERRORLEVEL% NEQ 0 pause
@REM )

exit /b 0
