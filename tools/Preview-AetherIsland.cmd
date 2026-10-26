@echo off
title Aether Island - Preview Launcher
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Preview-AetherIsland.ps1"
if errorlevel 1 pause
