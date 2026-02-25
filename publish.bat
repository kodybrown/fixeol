@echo off

cd "%~dp0"
dotnet publish -c Debug -r win-x64 --self-contained false /p:PublishSingleFile=true /p:PublishTrimmed=true -o ./publish
cp .\publish\fixeol.* "%UserProfile%\Bin\"
