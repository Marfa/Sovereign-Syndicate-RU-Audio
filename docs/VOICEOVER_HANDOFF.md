# Sovereign Syndicate — нейроозвучка (handoff для нового чата)

> Этот документ — контекст для отдельного чата по озвучке. В чате по локализации `е/ё` озвучку не трогаем.

## Игра

- Путь: `C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate`
- Версия (на момент анализа): `1.1.26` (`StreamingAssets/version.txt`)
- Движок: Unity (Mono)
- Локализация: `Sovereign Syndicate_Data/StreamingAssets/Localization/SyndicateLocalization.csv`

## Важно

- **Официальной озвучки диалогов в текущей сборке нет** — отдельных wav/ogg/mp3 с репликами в `StreamingAssets` нет.
- **Steam Workshop для модов не поддерживается.**
- Планируется Director's Cut с англ. озвучкой; русской VO от разработчика нет.
- Моддинг теоретически через MelonLoader/BepInEx, но готовой экосистемы модов нет.

## Уже подготовлено

### Выгрузка реплик (рус.)

Скрипт: `scripts/extract_voice_lines_ru.py`

Результат:

- `C:\Temp\SovereignSyndicateVoice\lines_ru\atticus.tsv` — 72 строки
- `C:\Temp\SovereignSyndicateVoice\lines_ru\clara.tsv` — 26 строк
- `C:\Temp\SovereignSyndicateVoice\lines_ru\otto.tsv` — 25 строк

Формат TSV: `key`, `text_ru`

### Bootstrap-референсы (Piper TTS, бесплатно, локально)

Скрипт: `scripts/prepare_voice_refs_piper.py`

Результат:

| Персонаж | Файл | Голос Piper | Длительность |
|----------|------|-------------|--------------|
| Atticus | `C:\Temp\SovereignSyndicateVoice\refs\atticus_ref.wav` | dmitri | ~24 с |
| Clara | `C:\Temp\SovereignSyndicateVoice\refs\clara_ref.wav` | irina | ~32 с |
| Otto | `C:\Temp\SovereignSyndicateVoice\refs\otto_ref.wav` | denis | ~24 с |

Это **не каноничные голоса** из игры, а стартовые референсы для XTTS/пайплайна.

Модели Piper: `C:\Temp\SovereignSyndicateVoice\piper_models\`

## Рекомендуемый пайплайн (бесплатно)

1. **Референсы** — заменить bootstrap на свои записи 20–60 с чистой речи или на VO из Director's Cut (когда появится).
2. **Генерация** — Coqui XTTS v2 (локально, voice cloning) или Piper для черновиков.
3. **Выход** — `voice/atticus/{key}.wav`, `voice/clara/{key}.wav`, `voice/otto/{key}.wav`
4. **Интеграция** — MelonLoader/BepInEx + подмена/воспроизведение по ключу локализации (требует отдельной разработки).

## Инструменты на ПК пользователя

- Python 3.11
- `ffmpeg` установлен
- `piper-tts` установлен (`pip install piper-tts`)

## Бэкап (глобальное правило проекта)

Перед правкой файлов игры — бэкап в `C:\Temp\SovereignSyndicateBackup\`.

## Следующие шаги в новом чате

1. Скрипт генерации wav из `lines_ru/*.tsv` через XTTS с 3 референсами.
2. Пилот: 5–10 реплик на персонажа, проверка качества.
3. Исследование точки подключения аудио в Unity (Adventure Creator / FMOD / Resources).
4. Прототип MelonLoader-мода для воспроизведения по ключу `Keys` из CSV.
