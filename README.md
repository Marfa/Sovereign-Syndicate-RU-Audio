# Sovereign Syndicate RU Audio

**Нейро-озвучка русской локализации Sovereign Syndicate на лету — Atticus, Clara, Teddy, Otto через MelonLoader + XTTS.**

## Требования (чистая система)

| Компонент | Зачем |
| --- | --- |
| Windows 10/11 | ОС |
| [Sovereign Syndicate](https://store.steampowered.com/app/1674920/) | игра |
| [MelonLoader](https://melonwiki.xyz) (Mono / Open-Beta) | загрузка мода |
| [Python 3.11](https://www.python.org/downloads/) | XTTS worker (`venv`) |
| NVIDIA GPU + актуальный драйвер | рекомендуется (CUDA); на CPU очень медленно |
| Интернет | скачивание coqui-tts, **silero-stress**, Piper-моделей, XTTS checkpoint при первом запуске |
| .NET SDK 8+ | **только** если собираете мод из исходников (`install_voice_mod.bat`) |

Озвучиваются **Atticus**, **Clara**, **Teddy** и автоматон **Otto** (отдельные голоса). NPC — нет. Loadscreen не озвучивается.

Автоударения в русском тексте перед TTS: **[silero-stress](https://github.com/snakers4/silero-stress)** (ставится вместе с `venv`).

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
- пустые `Mods\SovereignSyndicateVoice\voice\{atticus,clara,teddy,otto}\`

### 4. Один раз: XTTS-окружение (`venv` + `refs` + ударения)

Без этого шага мод загрузится, но озвучка на лету не заработает (`VO prefetch: missing python…` / нет refs).

```bat
install_voice_env.bat
install_voice_env.bat "D:\SteamLibrary\steamapps\common\Sovereign Syndicate"
```

Скрипт:

1. Создаёт **`Mods\SovereignSyndicateVoice\venv\`** — физическая папка в каталоге мода (не junction и не `C:\Temp`)
2. Ставит зависимости из `requirements-voice.txt`:
   - `coqui-tts` (XTTS)
   - **`silero-stress`** (автоударения в русском тексте перед генерацией)
   - + отдельно `piper-tts` (референсы голосов)
3. Генерирует референсы в `Mods\SovereignSyndicateVoice\refs\` (`atticus_ref.wav`, `clara_ref.wav`, `teddy_ref.wav`, `otto_ref.wav`)

**Где лежит venv:** только `Mods\SovereignSyndicateVoice\venv\`. Это обязательная часть установки — весь XTTS worker и pip-зависимости должны быть рядом с модом, чтобы переустановка игры/бэкап Mods не теряли окружение.

**Обновление со старых сборок:** если venv был junction на `C:\Temp\SovereignSyndicateVoice\venv` или только в `C:\Temp`, повторный запуск `install_voice_env.bat` **копирует** его в `Mods\...\venv\` и убирает junction. `C:\Temp` после миграции можно удалить вручную.

Если игра в `Program Files (x86)` и venv не создаётся — запустите `install_voice_env.bat` **от имени администратора** (нужна запись в `Mods\`).

Отдельная ручная установка silero не нужна — достаточно `install_voice_env.bat` (или `pip install -r requirements-voice.txt` внутри уже существующего `venv`).

Нужны Python 3.11 в PATH (или `%LocalAppData%\Programs\Python\Python311\python.exe`) и интернет. Первая установка может занять долго.

Опционально заранее (для CUDA) в системный Python:

```bat
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124
```

`install_voice_env.bat` создаёт venv с `--system-site-packages`, чтобы подхватить уже установленный torch.

Отключить автообработку текста: `SS_VOICE_STRESS=0`.  
По умолчанию silero ставит только **ё** (режим `yo`) — полные ударения (`SS_VOICE_STRESS=full`) дают паузы между словами в XTTS. Имена **Отто**, **Сайлас** и **Молли** принудительно с ударением на первый слог (`О́о-тто`, `Са́й-лас`, `Мо́о-лли`).

### 5. Запуск

1. В игре включите **русскую** локализацию.
2. Запустите игру через обычный ярлык (с MelonLoader).
3. В `MelonLoader\Latest.log` должно быть примерно:
   - `Sovereign Syndicate Voice v0.5.x`
   - `VO env root: …\Mods\SovereignSyndicateVoice`
   - без предупреждений `venv missing` / `refs missing`

### 6. Первая реплика в игре

1. Заговорите с NPC за Atticus / Clara / Teddy.
2. В логе: `VO miss` → `XTTS warm worker started` → (при успехе) `silero-stress accentor ready` → через ~20–60 с `VO replay ready` / `VO play dialogue`.
3. Wav сохраняются в `Mods\SovereignSyndicateVoice\voice\{персонаж}\` на время сессии.  
   При **первом** запуске создаётся `Mods\SovereignSyndicateVoice\settings.ini` с `delete_wav_on_exit=true` (по умолчанию кэш **удаляется** при выходе из игры).  
   Чтобы хранить wav между сессиями: `delete_wav_on_exit=false` в `settings.ini`, затем перезапуск.

---

## Структура после установки

Всё рабочее — под `Mods\SovereignSyndicateVoice\`:

| Путь | Откуда | Назначение |
| --- | --- | --- |
| `..\SovereignSyndicateVoice.dll` | installer | сам мод |
| `scripts\` | git / zip | XTTS worker |
| `venv\` | `install_voice_env.bat` | Python + Coqui XTTS + **silero-stress** (реальная папка в Mods, не `C:\Temp`) |
| `refs\` | `install_voice_env.bat` | голоса-референсы |
| `voice\` | игра (на лету) | кэш wav (по умолчанию чистится при выходе — см. `settings.ini`) |
| `settings.ini` | первый запуск мода | `delete_wav_on_exit` и др. |
| `prefetch.log` | worker | лог генерации |

`venv` и модели XTTS / silero в git/релиз не входят (слишком большие) — ставятся pip’ом при шаге 4.

---

## Проверка, что всё установлено

| Проверка | Ожидание |
| --- | --- |
| `Mods\SovereignSyndicateVoice.dll` | файл есть |
| `Mods\SovereignSyndicateVoice\venv\Scripts\python.exe` | есть (не junction на `C:\Temp`) |
| `Mods\SovereignSyndicateVoice\refs\clara_ref.wav` (и atticus/teddy/otto) | есть |
| `Mods\SovereignSyndicateVoice\scripts\generate_dialogue_batch.py` | есть |
| `venv\Scripts\python.exe -c "import silero_stress"` | без ошибки |
| Лог: `VO prefetch: XTTS warm worker started` | при первом miss |
| Лог / `prefetch.log`: `silero-stress accentor ready` | worker поднял ударения |

Если worker не стартует — смотрите `Mods\SovereignSyndicateVoice\prefetch.log`.  
Если нет ударений: переустановите зависимости — `install_voice_env.bat` или  
`Mods\...\venv\Scripts\python.exe -m pip install -r requirements-voice.txt`.

---

## Что входит в репозиторий

| Файл | Назначение |
| --- | --- |
| `install_voice_mod.bat` | Сборка + DLL + scripts + вызов `install_voice_env.bat` |
| `install_voice_dll.bat` | DLL + scripts из релиза (без .NET SDK) |
| `install_voice_env.bat` | `venv` + `refs` (один раз на машине) |
| `mods/SovereignSyndicateVoice/` | исходники MelonLoader-мода |
| `scripts/` | XTTS worker и подготовка refs |
| `requirements-voice.txt` | pip-зависимости (включая `silero-stress`) |

## Как работает

- Диалоги Dialogue System → русский текст → **silero-stress** (ударения) → XTTS → `Mods\...\voice\{персонаж}\c{convId}_e{entryId}.wav`
- Prefetch заранее ставит в очередь соседние реплики ветки / меню
- Loadscreen не озвучивается
- NPC не озвучиваются

## Персонажи

| Персонаж | Папка | Голос |
| --- | --- | --- |
| Atticus | `voice/atticus/` | dmitri + horse FX (`atticus_ref.wav`) |
| Clara | `voice/clara/` | клон по `refs/clara_ref.wav` (Piper irina) |
| Teddy (TED-маршрут) | `voice/teddy/` | denis + gnome FX (`teddy_ref.wav`) |
| Otto (автоматон) | `voice/otto/` | ruslan + robot FX (`refs/otto_ref.wav`) |

## Логи и отладка

| Путь | Содержимое |
| --- | --- |
| `MelonLoader\Latest.log` | VO play / miss / prefetch / replay |
| `Mods\SovereignSyndicateVoice\prefetch.log` | XTTS worker |
| `Mods\SovereignSyndicateVoice\lines_ru\` | дамп реплик из игры |

## Changelog (кратко)

- **v0.5.28** — venv всегда в `Mods\SovereignSyndicateVoice\venv\` (без junction на `C:\Temp`); миграция legacy Temp/junction в `install_voice_env.bat`; README и предупреждение в моде
- **v0.5.27** — security: `transformers>=5.3.0` (закрыты CVE в HuggingFace Transformers); `scripts/check_security.ps1`, `AGENTS.md`, pre-commit gitleaks; pip-audit в `install_voice_env.bat`; fix bat для путей с `(x86)`
- **v0.5.26** — при выходе удаление сгенерированных wav; настройка `settings.ini` → `delete_wav_on_exit` (по умолчанию `true`, создаётся при первом запуске)  
- **v0.5.25** — silero-stress: по умолчанию только ё (`SS_VOICE_STRESS=yo`); `full` — ударения (могут дать паузы в XTTS); Отто → О́тто; лицензии third-party в README  
- **v0.5.24** — ellipsis/паузы не отменяют pending VO replay (Tarot Fail/Passed)  
- **v0.5.22** — Teddy: голос гнома; Atticus: лошадиный тембр; Otto: робот (как раньше)  
- **v0.5.21** — голос Otto с робот-эффектом (ring-mod + metallic comb на ref и на каждую реплику)  
- **v0.5.20** — отдельный мужской голос Otto (Piper ruslan); Teddy → `voice/teddy/` (denis)  
- **v0.5.19** — снова озвучивается автоматон Otto (общий голос с Teddy в `voice/otto/`)  
- **v0.5.18** — фикс replay после смены реплики; чистый текст для TTS без HTML субтитров  
- **v0.5.17** — окружение в `Mods\SovereignSyndicateVoice\` (`venv` / `refs` / `voice` / `scripts`)  
- **v0.5.13** — (временно) автоматон Otto без VO; отмена stale replay; вырезание `*сценических ремарок*`  

## Лицензия

Код и ассеты этого репозитория: **CC BY-NC-SA 4.0** — см. `LICENSE`.

### Сторонние компоненты (ударения / TTS)

| Компонент | Лицензия | Заметка |
| --- | --- | --- |
| [silero-stress](https://github.com/snakers4/silero-stress) (Silero Team) | **MIT** | Автоударения перед XTTS. Ставится pip’ом в `venv` через `install_voice_env.bat` / `requirements-voice.txt`. Использование и распространение зависимости по MIT допустимы; исходники silero в git этого мода **не вендорятся**. |
| Coqui XTTS / Piper и др. | свои лицензии пакетов | Ставятся через pip вместе с `requirements-voice.txt` |

## От автора

Код подготовлен с помощью Cursor.

Поддержка проекта Донат: https://www.donationalerts.com/r/themarfa  
Донат криптой: https://nowpayments.io/donation/themarfa
