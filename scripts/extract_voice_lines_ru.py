import csv
from pathlib import Path

CSV_PATH = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate\Sovereign Syndicate_Data\StreamingAssets\Localization\SyndicateLocalization.csv"
)
OUT_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\lines_ru")

CHAR_TAGS = {
    "atticus": ("_ATT_", "ATTICUS", "_AC_"),
    "clara": ("_CLA_", "CLARA", "_CR_"),
    "otto": ("_OTT_", "_OTTO_", "_TED_", "OTTO", "TEDDY"),
}

SKIP_PREFIXES = (
    "BUTTON_",
    "MENU_",
    "SETTINGS_",
    "INPUT_",
    "CONTROLS_",
    "UI_",
    "TUTORIAL_",
    "MAJOR_ARCANA_",
    "MINOR_ARCANA_",
)


def looks_like_dialogue(text: str) -> bool:
    if not text or len(text.strip()) < 20:
        return False
    # Keep lines that look like spoken or narrative text.
    return any(mark in text for mark in (".", "?", "!", "—", ":", ";"))


def match_character(key: str) -> str | None:
    upper = key.upper()
    for name, tags in CHAR_TAGS.items():
        if any(tag in upper for tag in tags):
            return name
    return None


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    with CSV_PATH.open("r", encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f))

    header = rows[0]
    key_idx = header.index("Keys")
    ru_idx = header.index("Russian")

    grouped: dict[str, list[tuple[str, str]]] = {name: [] for name in CHAR_TAGS}

    for row in rows[1:]:
        if max(key_idx, ru_idx) >= len(row):
            continue
        key = row[key_idx].strip()
        text = row[ru_idx].strip()
        if not key or not text:
            continue
        if key.startswith(SKIP_PREFIXES):
            continue
        if not looks_like_dialogue(text):
            continue

        character = match_character(key)
        if not character:
            continue

        grouped[character].append((key, text.replace("\r\n", " ").replace("\n", " ")))

    for character, lines in grouped.items():
        out_file = OUT_DIR / f"{character}.tsv"
        with out_file.open("w", encoding="utf-8", newline="") as f:
            f.write("key\ttext_ru\n")
            for key, text in lines:
                f.write(f"{key}\t{text}\n")
        print(f"{character}: {len(lines)} -> {out_file}")


if __name__ == "__main__":
    main()
