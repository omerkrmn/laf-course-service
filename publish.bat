@echo off
chcp 65001 >nul
title LAF Course Service - Publish for IIS
echo ==========================================================
echo   LAF Course Service - Production Publish for IIS
echo ==========================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
pause
