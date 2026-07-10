# Sovereign Syndicate RU Audio

**Нейро-озвучка русской локализации Sovereign Syndicate на лету — Atticus, Clara, Otto через MelonLoader + XTTS.**

```bat
install_voice_mod.bat
install_voice_mod.bat "D:\SteamLibrary\steamapps\common\Sovereign Syndicate"
```

## Что входит

| Файл | Назначение |
| --- | --- |
| `install_voice_mod.bat` | Сборка из исходников и установка DLL |
| `install_voice_dll.bat` | Установка готового DLL из релиза (без .NET SDK) |
| `mods/SovereignSyndicateVoice/` | Исходники MelonLoader-мода |
| `scripts/generate_dialogue_batch.py` | XTTS worker (prefetch на лету) |
| `scripts/generate_voice_xtts.py` | Офлайн-батч по TSV |
| `scripts/xtts_audio.py` | Нормализация текста и постобработка wav |
| `scripts/prepare_voice_refs_piper.py` | Подготовка референс-голосов |
| `requirements-voice.txt` | Python-зависимости (coqui-tts) |

## Требования

| Компонент | Версия |
| --- | --- |
| Windows | 10/11 |
| [MelonLoader](https://melonwiki.xyz) | на `Sovereign Syndicate.exe` (Mono) |
| .NET SDK | для сборки мода |
| Python | 3.10–3.11 |
| NVIDIA GPU | рекомендуется (XTTS на CUDA) |

## Быстрый старт

1. Закройте игру.
2. Установите MelonLoader на Sovereign Syndicate (если ещё нет).
3. Настройте XTTS (один раз):

```bat
python -m venv C:\Temp\SovereignSyndicateVoice\venv
C:\Temp\SovereignSyndicateVoice\venv\Scripts\pip install -r requirements-voice.txt
python scripts\prepare_voice_refs_piper.py
```

4. Запустите `install_voice_mod.bat`.
5. Запустите игру с русской локализацией.

## Как работает

- Диалоги Pixel Crushers Dialogue System → русский текст → XTTS → `C:\Temp\SovereignSyndicateVoice\voice\{персонаж}\c{convId}_e{entryId}.wav`
- Ключи привязаны к разговору (`c400044_e17`), без коллизий `e12` / `Tarot Fail` между сценами
- Prefetch подгружает следующие реплики по текущей ветке диалога
- Озвучка ищется в `Mods\...\voice` и в dev-папке `C:\Temp\SovereignSyndicateVoice\voice`
- При выходе из игры wav в `Mods\...\voice` **удаляются**; кэш в `C:\Temp\...` сохраняется
- Loadscreen не озвучивается

## v0.4.9 — исправления озвучки

| Проблема | Решение |
| --- | --- |
| Чужая фраза (`e12`, `Tarot Fail`, меню vs реплика `-2`) | Уникальные ключи `c{convId}_e{entryId}`, title убран из поиска |
| `speaker=?` у Atticus | Fallback по `ActorID` из базы диалогов |
| Prefetch не генерировал wav | Вывод в `C:\Temp\...`, сброс зависшего `prefetch.lock` |

## Персонажи

| Персонаж | Папка | Голос |
| --- | --- | --- |
| Atticus | `voice/atticus/` | клон по ref |
| Clara | `voice/clara/` | клон по ref |
| Otto | `voice/otto/` | клон по ref |

Референсы: `C:\Temp\SovereignSyndicateVoice\refs\{персонаж}_ref.wav`

## Логи и отладка

| Путь | Содержимое |
| --- | --- |
| `MelonLoader\Latest.log` | VO play / miss / prefetch |
| `C:\Temp\SovereignSyndicateVoice\prefetch.log` | XTTS worker |
| `C:\Temp\SovereignSyndicateVoice\lines_ru\` | дамп реплик из игры |

## Лицензия

CC BY-NC-SA 4.0 — см. `LICENSE`.

## От автора

Код подготовлен с помощью Cursor.

Поддержка проекта Донат: https://www.donationalerts.com/r/themarfa  
Донат криптой: https://nowpayments.io/donation/themarfa
