"""Generate 20-60s Russian voice reference clips via Piper TTS (free, local)."""
from __future__ import annotations

import argparse
import subprocess
import urllib.request
from pathlib import Path

DEFAULT_OUT = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate"
    r"\Mods\SovereignSyndicateVoice\refs"
)
MODEL_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\piper_models")
BASE = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/ru/ru_RU"

CHARACTERS = {
    "atticus": {
        "voice": "dmitri",
        "text": (
            "Меня зовут Аттикус Дейли. Я минотавр, иллюзионист и, если уж на то пошло, "
            "человек, который слишком хорошо знает вкус джина. Лондон не прощает слабости, "
            "но я давно перестал ждать прощения. Сегодня я иду по туманным улочкам Ист-Энда, "
            "где каждый шаг может стать последним, а каждая встреча — новой сделкой с судьбой. "
            "Я говорю спокойно, низким голосом, без лишней суеты, но в каждом слове — усталость "
            "и твёрдое решение не сдаваться."
        ),
    },
    "clara": {
        "voice": "irina",
        "text": (
            "Клара Рид. Корсар, охотница и женщина, которой есть что скрывать. "
            "Я привыкла говорить прямо, потому что в доках ложь пахнет так же гнилой, "
            "как и канализация под ногами. Меня не пугают ни пистолеты, ни маски, ни обещания "
            "богатых господ. Я ищу правду о серийном убийце, и если для этого придётся "
            "переступить через чужие секреты — я переступлю. Мой голос твёрдый, быстрый, "
            "с лёгкой насмешкой, но без пустой бравады."
        ),
    },
    "otto": {
        "voice": "denis",
        "text": (
            "Отто. Автоматон, созданный для службы, но не для молчания. "
            "Я наблюдаю мир через шестерёнки, масло и точные расчёты, однако внутри меня "
            "есть вопросы, которые не решаются механикой. Тедди называет меня надёжным, "
            "а я стараюсь оправдать это слово каждый день. Говорю ровно, чётко, "
            "с лёгкой металлической сдержанностью, будто каждое слово проверено "
            "прежде, чем покинуть мои губы."
        ),
        "length_scale": "1.08",
    },
}


def download_model(voice: str) -> tuple[Path, Path]:
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    model = MODEL_DIR / f"ru_RU-{voice}-medium.onnx"
    config = MODEL_DIR / f"ru_RU-{voice}-medium.onnx.json"
    if not model.exists():
        urllib.request.urlretrieve(
            f"{BASE}/{voice}/medium/ru_RU-{voice}-medium.onnx",
            model,
        )
    if not config.exists():
        urllib.request.urlretrieve(
            f"{BASE}/{voice}/medium/ru_RU-{voice}-medium.onnx.json",
            config,
        )
    return model, config


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    out_dir: Path = args.out_dir
    out_dir.mkdir(parents=True, exist_ok=True)

    for name, cfg in CHARACTERS.items():
        model, config = download_model(cfg["voice"])
        out_wav = out_dir / f"{name}_ref.wav"
        text_file = out_dir / f"{name}_ref.txt"
        text_file.write_text(cfg["text"], encoding="utf-8")

        cmd = [
            "python",
            "-m",
            "piper",
            "-m",
            str(model),
            "-c",
            str(config),
            "-i",
            str(text_file),
            "-f",
            str(out_wav),
        ]
        length_scale = cfg.get("length_scale")
        if length_scale:
            cmd.extend(["--length-scale", length_scale])

        subprocess.run(cmd, check=True)
        print(f"CREATED {out_wav}")


if __name__ == "__main__":
    main()
