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

echo [1/3] Сборка SovereignSyndicateVoice...
dotnet build "%ROOT%mods\SovereignSyndicateVoice\SovereignSyndicateVoice.csproj" -c Release /p:GameDir="%GAME_DIR%"
if errorlevel 1 (
  echo Ошибка сборки
  pause
  exit /b 1
)

echo [2/3] Копирование DLL и scripts...
set "MODS=%GAME_DIR%\Mods"
set "VOICE_MOD=%MODS%\SovereignSyndicateVoice"
if not exist "%MODS%" mkdir "%MODS%"
copy /Y "%ROOT%mods\SovereignSyndicateVoice\bin\SovereignSyndicateVoice.dll" "%MODS%\SovereignSyndicateVoice.dll" >nul
if not exist "%VOICE_MOD%\voice\atticus" mkdir "%VOICE_MOD%\voice\atticus"
if not exist "%VOICE_MOD%\voice\clara" mkdir "%VOICE_MOD%\voice\clara"
if not exist "%VOICE_MOD%\voice\otto" mkdir "%VOICE_MOD%\voice\otto"
if not exist "%VOICE_MOD%\scripts" mkdir "%VOICE_MOD%\scripts"
if not exist "%VOICE_MOD%\refs" mkdir "%VOICE_MOD%\refs"
copy /Y "%ROOT%scripts\generate_dialogue_batch.py" "%VOICE_MOD%\scripts\" >nul
copy /Y "%ROOT%scripts\xtts_audio.py" "%VOICE_MOD%\scripts\" >nul
copy /Y "%ROOT%scripts\prepare_voice_refs_piper.py" "%VOICE_MOD%\scripts\" >nul

echo [3/3] XTTS окружение (venv + refs)...
call "%ROOT%install_voice_env.bat" "%GAME_DIR%"
if errorlevel 1 (
  echo Warning: install_voice_env.bat failed — озвучка на лету не заработает, пока не установите venv.
)

echo.
echo Готово. DLL: %MODS%\SovereignSyndicateVoice.dll
echo Окружение: %VOICE_MOD%  (venv, refs, scripts, voice)
echo Wav-кэш сохраняется между сессиями.
pause
