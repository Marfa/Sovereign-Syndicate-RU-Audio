# Sovereign Syndicate Voice (MelonLoader)

Прототип мода: воспроизводит `wav` по ключу локализации из CSV.

## Требования

1. [MelonLoader](https://melonwiki.xyz) установлен на `Sovereign Syndicate.exe` (Mono).
2. Сгенерированные файлы (см. `scripts/generate_voice_xtts.py`).

## Установка

```powershell
# Сборка (нужен MelonLoader в папке игры)
cd mods\SovereignSyndicateVoice
dotnet build -c Release

# DLL → Mods
Copy-Item bin\SovereignSyndicateVoice.dll "$env:GameDir\Mods\"

# Озвучка
$modVoice = "$env:LOCALAPPDATA\..\LocalLow\Artificial Agony\Sovereign Syndicate\MelonLoader\Mods\SovereignSyndicateVoice\voice"
# или путь из MelonLoader ModsDirectory — см. лог при старте
```

Проще для пилота: скопировать wav в `C:\Temp\SovereignSyndicateVoice\voice\` — мод подхватит dev-папку, если Mods-папка пуста.

Структура:

```
voice/
  atticus/LOADSCREEN_S001_ATT_DEFAULT.wav
  clara/...
  otto/LOADSCREEN_S001_TED_DEFAULT.wav
```

## Хуки

- `UILocalizationManager.LookupLocalizedValue` — load screens, UI
- `onConversationLine` / `onBarkLine` — Dialogue System

Подробнее: `docs/VOICEOVER_AUDIO_HOOKS.md`
