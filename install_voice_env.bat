@echo off
setlocal
chcp 65001 >nul

rem Installs XTTS venv + Piper refs into Mods\SovereignSyndicateVoice
rem Usage: install_voice_env.bat ["D:\Steam\...\Sovereign Syndicate"]

set "ROOT=%~dp0"
set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate"
if not "%~1"=="" set "GAME_DIR=%~1"

set "VOICE_MOD=%GAME_DIR%\Mods\SovereignSyndicateVoice"
set "VENV=%VOICE_MOD%\venv"
set "SCRIPTS=%VOICE_MOD%\scripts"
set "REFS=%VOICE_MOD%\refs"
set "PY_SYS=%LocalAppData%\Programs\Python\Python311\python.exe"
if not exist "%PY_SYS%" set "PY_SYS=python"

if not exist "%GAME_DIR%\Sovereign Syndicate.exe" (
  echo Game not found: %GAME_DIR%
  exit /b 1
)

echo [1/4] Folders + scripts...
if not exist "%VOICE_MOD%\voice\atticus" mkdir "%VOICE_MOD%\voice\atticus"
if not exist "%VOICE_MOD%\voice\clara" mkdir "%VOICE_MOD%\voice\clara"
if not exist "%VOICE_MOD%\voice\teddy" mkdir "%VOICE_MOD%\voice\teddy"
if not exist "%VOICE_MOD%\voice\otto" mkdir "%VOICE_MOD%\voice\otto"
if not exist "%SCRIPTS%" mkdir "%SCRIPTS%"
if not exist "%REFS%" mkdir "%REFS%"
copy /Y "%ROOT%scripts\generate_dialogue_batch.py" "%SCRIPTS%\" >nul
copy /Y "%ROOT%scripts\xtts_audio.py" "%SCRIPTS%\" >nul
copy /Y "%ROOT%scripts\prepare_voice_refs_piper.py" "%SCRIPTS%\" >nul
if exist "%ROOT%requirements-voice.txt" copy /Y "%ROOT%requirements-voice.txt" "%VOICE_MOD%\" >nul

echo [2/4] Python venv...
if exist "%VENV%\Scripts\python.exe" (
  echo venv already exists: %VENV%
) else (
  if exist "C:\Temp\SovereignSyndicateVoice\venv\Scripts\python.exe" (
    echo Linking legacy Temp venv...
    mklink /J "%VENV%" "C:\Temp\SovereignSyndicateVoice\venv"
    if errorlevel 1 (
      echo Junction failed — creating new venv in Mods ^(needs write access^)...
      "%PY_SYS%" -m venv --system-site-packages "%VENV%"
    )
  ) else (
    "%PY_SYS%" -m venv --system-site-packages "%VENV%"
  )
)

echo [3/4] pip install coqui-tts / silero-stress / piper...
"%VENV%\Scripts\python.exe" -m pip install -r "%ROOT%requirements-voice.txt" piper-tts
if errorlevel 1 (
  echo pip failed
  exit /b 1
)

echo [3b/4] pip-audit (dependency vulnerabilities)...
"%VENV%\Scripts\python.exe" -m pip install -q pip-audit
"%VENV%\Scripts\python.exe" -m pip_audit -r "%ROOT%requirements-voice.txt"
if errorlevel 1 (
  echo WARNING: pip-audit reported vulnerabilities — see AGENTS.md / scripts\check_security.ps1
)
"%VENV%\Scripts\python.exe" -c "import silero_stress; print('silero-stress OK')"
if errorlevel 1 (
  echo WARNING: silero-stress import failed — ударения перед XTTS будут отключены
)

echo [4/4] Voice refs ^(Piper^)...
"%VENV%\Scripts\python.exe" "%SCRIPTS%\prepare_voice_refs_piper.py" --out-dir "%REFS%"
if errorlevel 1 (
  echo refs failed
  exit /b 1
)

echo.
echo Ready.
echo   Mod root: %VOICE_MOD%
echo   venv:     %VENV%
echo   refs:     %REFS%
echo   scripts:  %SCRIPTS%
exit /b 0
