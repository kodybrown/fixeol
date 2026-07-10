@echo off

set "AppName=FixEol"

cd "%~dp0"

REM dotnet publish .\%AppName%.csproj -c Debug -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o ./publish/win-x64

dotnet publish .\%AppName%.csproj -c Debug -r win-x64 -o ./publish/win-x64

if %ERRORLEVEL% NEQ 0 pause & exit /B

if exist "%UserProfile%\Bin\" (
  copy ".\publish\win-x64\*" "%UserProfile%\Bin\apps\%AppName%\"
  if %ERRORLEVEL% NEQ 0 pause
)
