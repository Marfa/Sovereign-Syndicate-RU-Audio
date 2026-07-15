# Sovereign Syndicate RU Audio

**Нейро-озвучка русской локализации Sovereign Syndicate на лету — Atticus, Clara, Teddy через MelonLoader + XTTS.**

## Требования (чистая система)

| Компонент | Зачем |
| --- | --- |
| Windows 10/11 | ОС |
| [Sovereign Syndicate](https://store.steampowered.com/app/1674920/) | игра |
| [MelonLoader](https://melonwiki.xyz) (Mono / Open-Beta) | загрузка мода |
| [Python 3.11](https://www.python.org/downloads/) | XTTS worker (`venv`) |
| NVIDIA GPU + актуальный драйвер | рекомендуется (CUDA); на CPU очень медленно |
| Интернет | скачивание coqui-tts, Piper-моделей, XTTS checkpoint при первом запуске |
| .NET SDK 8+ | **только** если собираете мод из исходников (`install_voice_mod.bat`) |

Озвучиваются только **Atticus**, **Clara** и **Teddy** (папка `voice/otto/`). Автоматон Otto и NPC — нет. Loadscreen не озвучивается.

---

## Установка на чистую систему

Закройте игру перед всеми шагами.

### 1. MelonLoader

1. Установите MelonLoader на `Sovereign Syndicate.exe` (тип рантайма **Mono**).
2. Один раз запустите игру — должны появиться папки `MelonLoader\` и `Mods\`.
3. Закройте игру.

### 2. Получите мод

**Вариант A — релиз с GitHub (без сборки):**

1. Скачайте последний zip: [Releases](https://github.com/Marfa/Sovereign-Syndicate-RU-Audio/releases).
2. Распакуйте.
3. Убедитесь, что рядом лежат `SovereignSyndicateVoice.dll`, `install_voice_dll.bat`, `install_voice_env.bat`, папка `scripts\`, файл `requirements-voice.txt`.

**Вариант B — из исходников:**

```bat
git clone https://github.com/Marfa/Sovereign-Syndicate-RU-Audio.git
cd Sovereign-Syndicate-RU-Audio
```

Нужен .NET SDK.

### 3. Установите DLL + scripts в игру

Путь к игре по умолчанию:
`C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate`

Если Steam-библиотека в другом месте — передайте путь аргументом.

**Из релиза:**

```bat
install_voice_dll.bat
install_voice_dll.bat "D:\SteamLibrary\steamapps\common\Sovereign Syndicate"
```

**Из исходников (сборка + установка):**

```bat
install_voice_mod.bat
install_voice_mod.bat "D:\SteamLibrary\steamapps\common\Sovereign Syndicate"
```

`install_voice_mod.bat` в конце сам вызывает `install_voice_env.bat` (шаг 4).  
`install_voice_dll.bat` — **нет**, шаг 4 нужно выполнить отдельно.

После этого в игре должны быть:

- `Mods\SovereignSyndicateVoice.dll`
- `Mods\SovereignSyndicateVoice\scripts\` (`generate_dialogue_batch.py`, `xtts_audio.py`, `prepare_voice_refs_piper.py`)
- пустые `Mods\SovereignSyndicateVoice\voice\{atticus,clara,otto}\`

### 4. Один раз: XTTS-окружение (`venv` + `refs`)

Без этого шага мод загрузится, но озвучка на лету не заработает (`VO prefetch: missing python…` / нет refs).

```bat
install_voice_env.bat
install_voice_env.bat "D:\SteamLibrary\steamapps\common\Sovereign Syndicate"
```

Скрипт:

1. Создаёт `Mods\SovereignSyndicateVoice\venv\`
2. Ставит зависимости из `requirements-voice.txt` (coqui-tts и др.) + `piper-tts`
3. Генерирует референсы в `Mods\SovereignSyndicateVoice\refs\` (`atticus_ref.wav`, `clara_ref.wav`, `otto_ref.wav`)

Нужны Python 3.11 в PATH (или `%LocalAppData%\Programs\Python\Python311\python.exe`) и интернет. Первая установка может занять долго.

Опционально заранее (для CUDA) в системный Python:

```bat
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124
```

`install_voice_env.bat` создаёт venv с `--system-site-packages`, чтобы подхватить уже установленный torch.

### 5. Запуск

1. В игре включите **русскую** локализацию.
2. Запустите игру через обычный ярлык (с MelonLoader).
3. В `MelonLoader\Latest.log` должно быть примерно:
   - `Sovereign Syndicate Voice v0.5.x`
   - `VO env root: …\Mods\SovereignSyndicateVoice`
   - без предупреждений `venv missing` / `refs missing`

### 6. Первая реплика в игре

1. Заговорите с NPC за Atticus / Clara / Teddy.
2. В логе: `VO miss` → `XTTS warm worker started` → через ~20–60 с `VO replay ready` / `VO play dialogue`.
3. Wav сохраняются в `Mods\SovereignSyndicateVoice\voice\{персонаж}\` и **не удаляются** при выходе — повтор той же реплики играет сразу.

---

## Структура после установки

Всё рабочее — под `Mods\SovereignSyndicateVoice\`:

| Путь | Откуда | Назначение |
| --- | --- | --- |
| `..\SovereignSyndicateVoice.dll` | installer | сам мод |
| `scripts\` | git / zip | XTTS worker |
| `venv\` | `install_voice_env.bat` | Python + Coqui XTTS |
| `refs\` | `install_voice_env.bat` | голоса-референсы |
| `voice\` | игра (на лету) | кэш wav |
| `prefetch.log` | worker | лог генерации |

`venv` и модели XTTS в git/релиз не входят (слишком большие).

---

## Проверка, что всё установлено

| Проверка | Ожидание |
| --- | --- |
| `Mods\SovereignSyndicateVoice.dll` | файл есть |
| `Mods\SovereignSyndicateVoice\venv\Scripts\python.exe` | есть |
| `Mods\SovereignSyndicateVoice\refs\clara_ref.wav` (и atticus/otto) | есть |
| `Mods\SovereignSyndicateVoice\scripts\generate_dialogue_batch.py` | есть |
| Лог: `VO prefetch: XTTS warm worker started` | при первом miss |

Если worker не стартует — смотрите `Mods\SovereignSyndicateVoice\prefetch.log`.

---

## Что входит в репозиторий

| Файл | Назначение |
| --- | --- |
| `install_voice_mod.bat` | Сборка + DLL + scripts + вызов `install_voice_env.bat` |
| `install_voice_dll.bat` | DLL + scripts из релиза (без .NET SDK) |
| `install_voice_env.bat` | `venv` + `refs` (один раз на машине) |
| `mods/SovereignSyndicateVoice/` | исходники MelonLoader-мода |
| `scripts/` | XTTS worker и подготовка refs |
| `requirements-voice.txt` | pip-зависимости |

## Как работает

- Диалоги Dialogue System → русский текст → XTTS → `Mods\...\voice\{персонаж}\c{convId}_e{entryId}.wav`
- Prefetch заранее ставит в очередь соседние реплики ветки / меню
- Loadscreen не озвучивается
- NPC и автоматон Otto не озвучиваются

## Персонажи

| Персонаж | Папка | Голос |
| --- | --- | --- |
| Atticus | `voice/atticus/` | клон по `refs/atticus_ref.wav` |
| Clara | `voice/clara/` | клон по `refs/clara_ref.wav` |
| Teddy (TED-маршрут) | `voice/otto/` | клон по `refs/otto_ref.wav` |
| Otto (автоматон) | — | не озвучивается |

## Логи и отладка

| Путь | Содержимое |
| --- | --- |
| `MelonLoader\Latest.log` | VO play / miss / prefetch / replay |
| `Mods\SovereignSyndicateVoice\prefetch.log` | XTTS worker |
| `Mods\SovereignSyndicateVoice\lines_ru\` | дамп реплик из игры |

## Changelog (кратко)

- **v0.5.18** — фикс replay после смены реплики; чистый текст для TTS без HTML субтитров  
- **v0.5.17** — окружение в `Mods\SovereignSyndicateVoice\` (`venv` / `refs` / `voice` / `scripts`)  
- **v0.5.13** — автоматон Otto без VO; отмена stale replay; вырезание `*сценических ремарок*`  

## Лицензия

CC BY-NC-SA 4.0 — см. `LICENSE`.

## От автора

Код подготовлен с помощью Cursor.

Поддержка проекта Донат: https://www.donationalerts.com/r/themarfa  
Донат криптой: https://nowpayments.io/donation/themarfa
