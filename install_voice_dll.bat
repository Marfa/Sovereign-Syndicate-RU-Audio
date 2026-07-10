@echo off
setlocal
chcp 65001 >nul

set "ROOT=%~dp0"
set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate"

if not "%~1"=="" (
  set "GAME_DIR=%~1"
)

if not exist "%GAME_DIR%\Sovereign Syndicate.exe" (
  echo Не найдена игра: %GAME_DIR%
  pause
  exit /b 1
)

if not exist "%GAME_DIR%\MelonLoader" (
  echo MelonLoader не установлен. Сначала установите MelonLoader на Sovereign Syndicate.exe
  pause
  exit /b 1
)

if not exist "%ROOT%SovereignSyndicateVoice.dll" (
  echo Не найден SovereignSyndicateVoice.dll рядом с bat-файлом
  pause
  exit /b 1
)

set "MODS=%GAME_DIR%\Mods"
set "VOICE_MOD=%MODS%\SovereignSyndicateVoice"
if not exist "%MODS%" mkdir "%MODS%"
copy /Y "%ROOT%SovereignSyndicateVoice.dll" "%MODS%\SovereignSyndicateVoice.dll" >nul
if not exist "%VOICE_MOD%\voice\atticus" mkdir "%VOICE_MOD%\voice\atticus"
if not exist "%VOICE_MOD%\voice\clara" mkdir "%VOICE_MOD%\voice\clara"
if not exist "%VOICE_MOD%\voice\otto" mkdir "%VOICE_MOD%\voice\otto"

echo.
echo Готово: %MODS%\SovereignSyndicateVoice.dll
echo Нужен XTTS venv: C:\Temp\SovereignSyndicateVoice\venv
echo При выходе из игры сгенерированные wav удаляются.
pause
