# Sovereign Syndicate RU Audio — инструкции для агента

MelonLoader-мод + Python XTTS worker. Стек: **C# (.NET 4.7.2)**, **Python 3.11**, **pip** (не npm).

## Зависимости (Python)

Прямые зависимости — только в `requirements-voice.txt`. PyTorch ставится отдельно (см. README).

### Правила для AI-ассистента

1. **Новые pip-пакеты** — только с явной версией или нижней границей; после изменения `requirements-voice.txt` сразу запускать `pip-audit`.
2. **Не угадывать версии «из памяти»** — проверять актуальную на PyPI:
   ```powershell
   python -m pip index versions <package>
   ```
3. **Обновление зависимостей** — сначала `pip-audit`, затем smoke-тест XTTS (`install_voice_env.bat` + реплика в игре).
4. **`transformers`** — `>=5.3.0` (CVE-2026-4372 и др.); после изменения прогнать `pip-audit` и smoke-тест XTTS.

### Локальная проверка

```powershell
.\scripts\check_security.ps1
```

Скрипт: `pip-audit` по `requirements-voice.txt` + `gitleaks detect` по истории git.

### CI

`.github/workflows/security.yml` — на каждый push/PR: pip-audit (fail на находках) + gitleaks.

## Секреты

- **Не коммитить** API-ключи, токены, пароли, `.env` с credentials.
- Перед коммитом: `gitleaks detect --source .` (или `check_security.ps1`).
- Pre-commit (опционально): `pip install pre-commit && pre-commit install` — см. `.pre-commit-config.yaml`.

## Деплой в игру

См. `.cursor/rules/no-game-update-without-permission.mdc` — без явного запроса пользователя **не** писать в `C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate\`.

Сборка и установка: `install_voice_mod.bat`, окружение: `install_voice_env.bat`.

## Security review

По запросу `/security review` — subagent `security-review` по diff ветки. Фокус: command injection, path traversal, subprocess, сеть.

## Структура

| Путь | Назначение |
| --- | --- |
| `mods/SovereignSyndicateVoice/` | C# MelonLoader mod |
| `scripts/` | XTTS worker (Python) |
| `requirements-voice.txt` | pip-зависимости venv |
| `install_voice_*.bat` | сборка / DLL / venv |
