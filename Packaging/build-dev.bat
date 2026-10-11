@echo off
rem Builds the DEV launcher. Runs build-dev.ps1 with the execution policy bypassed for this run only.
rem   build-dev.bat          build
rem   build-dev.bat -Run     build and start it
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-dev.ps1" %*
