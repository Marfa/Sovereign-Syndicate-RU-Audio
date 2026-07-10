# Sovereign Syndicate — точки подключения озвучки

Исследование сборки `1.1.26` (Unity Mono). Официальной VO в `StreamingAssets` нет.

## Стек

| Компонент | DLL / путь | Роль |
|-----------|------------|------|
| Adventure Creator | `Assembly-CSharp.dll` (`AC\|…`) | Квесты, сцены, action-листы |
| Dialogue System | `Assembly-CSharp.dll` (`PixelCrushers.DialogueSystem\|…`) | Диалоги, субтитры, bark |
| FMOD | `FMODUnity.dll` | Музыка, SFX, ambient (`F_PlayOneShot`, `F_MusicPlayer`) |
| Локализация | `Localize.cs`, `UILocalizationManager` | CSV `SyndicateLocalization.csv` по ключу `Keys` |

## Ключевая находка

Текст на экране приходит через **`UILocalizationManager.LookupLocalizedValue`** (и родственные `GetLocalized*` в Dialogue System). Ключи вроде `LOADSCREEN_S001_ATT_DEFAULT` **не захардкожены** в DLL — подставляются в рантайме из CSV.

Поэтому для мода надёжнее всего:

1. **Postfix на `LookupLocalizedValue(string key)`** — ловит load screens, UI, всё, что идёт через CSV-ключ.
2. **События Dialogue System** — дублирующий хук для реплик в разговоре:
   - `DialogueManager.instance.conversationEvents.onConversationLine` (`Subtitle`)
   - `DialogueManager.instance.barkEvents.onBarkLine` (`Subtitle`)

## Subtitle (Dialogue System)

В строках сборки:

- `Invoked just before a line is delivered. Passes Subtitle.`
- `Invoked when a line has finished. Passes Subtitle.`

`Subtitle` содержит `dialogueEntry`; поле `DialogueText` часто совпадает с ключом локализации (проверить в пилоте с логом).

## FMOD

Игра использует FMOD для музыки/SFX, **не для диалоговой VO**. Для мода проще **Unity `AudioSource`** поверх субтитров, без встраивания в FMOD-банки.

## Структура файлов мода

```
UserData/MelonLoader/Mods/SovereignSyndicateVoice/
  voice/
    atticus/{key}.wav
    clara/{key}.wav
    otto/{key}.wav
  manifest.tsv   (опционально, генерируется скриптом)
```

Имя файла = ключ локализации (`LOADSCREEN_S001_ATT_DEFAULT.wav`).

## MelonLoader

В чистой установке игры **MelonLoader не стоит**. Установка: [MelonLoader](https://melonwiki.xyz) → `Sovereign Syndicate.exe` (Mono). Затем скопировать DLL мода в `Mods/`.

## Риски / открытые вопросы

- Один ключ может вызываться несколько раз подряд → нужен debounce или «не перезапускать тот же клип».
- Длинные loadscreen-тексты → одна wav на весь абзац; при обрыве сцены — `AudioSource.Stop()` на смене сцены.
- Реплики без CSV-ключа (чистый текст в Dialogue DB) — fallback: хеш текста или пропуск.

Прототип мода: `mods/SovereignSyndicateVoice/`.
