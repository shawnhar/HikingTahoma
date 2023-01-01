@echo off
setlocal

call put.bat

set whatToBuild=C:\Users\shawnhar\AppData\Local\Packages\166f3b82-cc97-4303-9f90-f858d8b28773_g9091v33cm94a\LocalCache\build.txt

if "%1" == "" (
    echo Building everything
    if exist %whatToBuild% del %whatToBuild%
) else (
    echo Building %*
    echo %* > %whatToBuild%
)

devenv /RunExit builder\builder.sln
