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

if not exist "%GAME_DIR%\Sovereign Syndicate.exe" goto :game_missing

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

echo [2/4] Python venv in Mods\SovereignSyndicateVoice\venv ...
set "LEGACY_VENV=C:\Temp\SovereignSyndicateVoice\venv"
set "MIG_VENV=%VOICE_MOD%\_venv_migrate"

rem Broken junction or empty venv folder
if exist "%VENV%" if not exist "%VENV%\Scripts\python.exe" rmdir "%VENV%" 2>nul

rem Junction at Mods\venv -> copy into real Mods folder (no C:\Temp dependency)
if not exist "%VENV%\Scripts\python.exe" goto :check_legacy_venv
fsutil reparsepoint query "%VENV%" >nul 2>&1
if not errorlevel 1 goto :migrate_junction_venv
goto :venv_ready

:check_legacy_venv
if exist "%LEGACY_VENV%\Scripts\python.exe" goto :migrate_legacy_venv

echo Creating venv in Mods...
"%PY_SYS%" -m venv --system-site-packages "%VENV%"
if errorlevel 1 goto :venv_create_failed
goto :venv_ready

:venv_create_failed
echo venv creation failed - run as Administrator if game is under Program Files
exit /b 1

:migrate_junction_venv
echo Migrating junction venv to real Mods folder...
if exist "%MIG_VENV%" rmdir /s /q "%MIG_VENV%"
robocopy "%VENV%" "%MIG_VENV%" /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS >nul
if errorlevel 8 goto :robocopy_junction_failed
rmdir "%VENV%"
move "%MIG_VENV%" "%VENV%" >nul
echo venv migrated from junction to %VENV%
goto :venv_ready

:robocopy_junction_failed
echo robocopy failed during venv migration
exit /b 1

:migrate_legacy_venv
echo Migrating legacy C:\Temp venv to Mods...
if exist "%VENV%" rmdir "%VENV%" 2>nul
robocopy "%LEGACY_VENV%" "%VENV%" /E /COPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS >nul
if errorlevel 8 goto :robocopy_legacy_failed
echo venv migrated from %LEGACY_VENV% to %VENV%
goto :venv_ready

:robocopy_legacy_failed
echo robocopy failed during legacy venv migration
exit /b 1

:venv_ready
if not exist "%VENV%\Scripts\python.exe" goto :venv_missing
echo venv OK: %VENV%
echo [3/4] pip install coqui-tts / silero-stress / piper...
"%VENV%\Scripts\python.exe" -m pip install -r "%ROOT%requirements-voice.txt" piper-tts
if errorlevel 1 goto :pip_failed

echo [3b/4] pip-audit (dependency vulnerabilities)...
"%VENV%\Scripts\python.exe" -m pip install -q pip-audit
"%VENV%\Scripts\python.exe" -m pip_audit -r "%ROOT%requirements-voice.txt"
if errorlevel 1 (
  echo WARNING: pip-audit reported vulnerabilities - see AGENTS.md / scripts\check_security.ps1
)
"%VENV%\Scripts\python.exe" -c "import silero_stress; print('silero-stress OK')"
if errorlevel 1 (
  echo WARNING: silero-stress import failed - stress marks disabled before XTTS
)

echo [4/4] Voice refs ^(Piper^)...
"%VENV%\Scripts\python.exe" "%SCRIPTS%\prepare_voice_refs_piper.py" --out-dir "%REFS%"
if errorlevel 1 goto :refs_failed

echo.
echo Ready.
echo   Mod root: %VOICE_MOD%
echo   venv:     %VENV%
echo   refs:     %REFS%
echo   scripts:  %SCRIPTS%
exit /b 0

:venv_missing
echo venv missing after setup: %VENV%
exit /b 1

:pip_failed
echo pip failed
exit /b 1

:refs_failed
echo refs failed
exit /b 1

:game_missing
echo Game not found: %GAME_DIR%
exit /b 1
