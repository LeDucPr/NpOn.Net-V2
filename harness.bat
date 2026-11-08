@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0\.agents\harness\harness.ps1" %*

