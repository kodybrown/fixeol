@echo off

cd "%~dp0"
rem dotnet publish -c Debug -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishTrimmed=true -o ./publish
dotnet publish -c Debug -r win-x64 --self-contained true /p:PublishSingleFile=true -o ./publish
cp .\publish\fixeol.* "%UserProfile%\Bin\"
