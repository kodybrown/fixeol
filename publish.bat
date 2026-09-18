@echo off

set "AppName=fixeol"
set "Configuration=Release"
set "Runtime=win-x64"

cd "%~dp0"

dotnet publish ".\%AppName%.csproj" -c %Configuration% -r %Runtime% --self-contained false -o ".\publish\%Runtime%"

if %ERRORLEVEL% NEQ 0 pause & exit /B

if exist "%UserProfile%\Bin\" (
  if not exist "%UserProfile%\Bin\apps\%AppName%\" mkdir "%UserProfile%\Bin\apps\%AppName%\"
  copy /Y ".\publish\%Runtime%\*" "%UserProfile%\Bin\apps\%AppName%\"
  if %ERRORLEVEL% NEQ 0 pause
)
