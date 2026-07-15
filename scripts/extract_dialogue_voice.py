"""Pair voiced English titles with nearest preceding Russian localization (same actor)."""
from __future__ import annotations

import csv
import re
import struct
from pathlib import Path

GAME_DATA = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate\Sovereign Syndicate_Data"
)
OUT_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\lines_ru")

ACTOR_TOKENS = {
    "atticus": ("400004", "atticus", "daley", "daily"),
    "clara": ("400011", "clara"),
    "teddy": ("400012", "teddy", "ted", "redgrave"),
    "otto": ("400003", "otto"),
}
MAX_PAIR_DISTANCE = 80_000


def clean_ru(text: str) -> str:
    t = text.strip().strip("«»\"' ")
    t = re.sub(r"\{([^}]+)\}", r"\1", t)
    t = re.sub(r"\s+", " ", t).strip()
    return t


def has_cyrillic(text: str) -> bool:
    return bool(re.search(r"[\u0400-\u04ff]", text))


def map_actor(window: bytes) -> str | None:
    low = window.decode("utf-8", errors="ignore").lower()
    for character, tokens in ACTOR_TOKENS.items():
        if any(t in low for t in tokens):
            return character
    return None


def read_field_string(blob: bytes, field: bytes, start: int = 0) -> tuple[str, int, int]:
    marker = field + b"\x00"
    idx = blob.find(marker, start)
    if idx < 0:
        return "", -1, start
    pos = idx + len(marker)
    while pos < len(blob) and blob[pos] == 0:
        pos += 1
    if pos + 4 > len(blob):
        return "", idx, idx + 1
    ln = struct.unpack_from("<i", blob, pos)[0]
    if ln <= 0 or ln > 2000 or pos + 4 + ln > len(blob):
        return "", idx, idx + 1
    raw = blob[pos + 4 : pos + 4 + ln].split(b"\x00")[0]
    return raw.decode("utf-8", errors="ignore").strip(), idx, idx + 1


def is_voiced_title(title: str) -> bool:
    if not title or len(title) < 10:
        return False
    if title.startswith(("START", "Blood", "A {", "END")):
        return False
    if title.endswith(("-3", "-4")):
        return False
    return title.endswith("-2") or (title.startswith('"') and "?" in title)


def find_entry_id(blob: bytes, anchor: int) -> int | None:
    win = blob[max(0, anchor - 512) : anchor]
    for off in range(len(win) - 8, 0, -4):
        if win[off : off + 2] != b"\x05\x00":
            continue
        value = struct.unpack_from("<i", win, off + 4)[0]
        if 1 <= value <= 500_000:
            return value
    return None


def scan_voiced_titles(blob: bytes) -> list[dict]:
    rows: list[dict] = []
    for field in (b"Title",):
        pos = 0
        while True:
            title, idx, pos = read_field_string(blob, field, pos)
            if idx < 0:
                break
            if not is_voiced_title(title):
                continue
            character = map_actor(blob[max(0, idx - 4096) : idx + 256])
            if not character:
                continue
            rows.append(
                {
                    "offset": idx,
                    "character": character,
                    "title": title,
                    "entry_id": find_entry_id(blob, idx),
                }
            )
    return rows


def scan_russian_lines(blob: bytes) -> list[dict]:
    rows: list[dict] = []
    marker = b"CustomFieldType_Localization"
    pos = 0
    while True:
        idx = blob.find(marker, pos)
        if idx < 0:
            break
        pos = idx + 1
        ru_idx = blob.find(b"Russian\x00", idx, idx + 128)
        if ru_idx < 0:
            continue
        ru, _, _ = read_field_string(blob, b"Russian", ru_idx)
        ru = clean_ru(ru)
        if len(ru) < 12 or not has_cyrillic(ru):
            continue
        character = map_actor(blob[max(0, idx - 8192) : idx + 64])
        if not character:
            continue
        rows.append({"offset": idx, "character": character, "text_ru": ru})
    return rows


def pair_titles_with_russian(titles: list[dict], russian: list[dict]) -> list[dict]:
    paired: list[dict] = []
    for title_row in sorted(titles, key=lambda r: r["offset"]):
        best = None
        best_dist = MAX_PAIR_DISTANCE + 1
        for ru_row in russian:
            if ru_row["character"] != title_row["character"]:
                continue
            if ru_row["offset"] >= title_row["offset"]:
                continue
            dist = title_row["offset"] - ru_row["offset"]
            if dist < best_dist:
                best_dist = dist
                best = ru_row
        if best is None:
            continue
        entry_id = title_row.get("entry_id")
        key = f"e{entry_id}" if entry_id else title_row["title"][:120]
        paired.append(
            {
                "character": title_row["character"],
                "key": key,
                "text_ru": best["text_ru"],
                "title": title_row["title"],
            }
        )
    return paired


def extract_asset(path: Path) -> list[dict]:
    blob = path.read_bytes()
    titles = scan_voiced_titles(blob)
    russian = scan_russian_lines(blob)
    if not titles or not russian:
        return []
    return pair_titles_with_russian(titles, russian)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    all_rows: dict[tuple[str, str], dict] = {}
    for path in sorted(GAME_DATA.glob("*.assets")):
        if path.stat().st_size < 100_000:
            continue
        try:
            found = extract_asset(path)
        except OSError:
            continue
        for row in found:
            all_rows[(row["character"], row["key"])] = row
        if found:
            print(f"{path.name}: {len(found)}")

    grouped: dict[str, list[dict]] = {c: [] for c in ACTOR_TOKENS}
    for (_, _), row in sorted(all_rows.items(), key=lambda x: (x[0][0], x[0][1])):
        grouped[row["character"]].append(row)

    for character, items in grouped.items():
        out = OUT_DIR / f"dialogue_{character}.tsv"
        with out.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f, delimiter="\t")
            w.writerow(["key", "text_ru", "title"])
            for e in items:
                w.writerow([e["key"], e["text_ru"], e.get("title", "")])
        print(f"{character}: {len(items)} dialogue -> {out}")

    for character in ACTOR_TOKENS:
        rows: dict[str, str] = {}
        for src in (OUT_DIR / f"{character}.tsv", OUT_DIR / f"dialogue_{character}.tsv"):
            if not src.is_file():
                continue
            with src.open("r", encoding="utf-8", newline="") as f:
                for row in csv.DictReader(f, delimiter="\t"):
                    k = (row.get("key") or "").strip()
                    t = (row.get("text_ru") or "").strip()
                    if k and t:
                        rows[k] = t
        runtime = OUT_DIR / f"runtime_{character}.tsv"
        if runtime.is_file():
            with runtime.open("r", encoding="utf-8", newline="") as f:
                for row in csv.DictReader(f, delimiter="\t"):
                    k = (row.get("key") or "").strip()
                    t = (row.get("text_ru") or "").strip()
                    if k and t:
                        rows[k] = t
        if not rows:
            continue
        merged = OUT_DIR / f"all_{character}.tsv"
        with merged.open("w", encoding="utf-8", newline="") as f:
            w = csv.writer(f, delimiter="\t")
            w.writerow(["key", "text_ru"])
            for k, t in sorted(rows.items()):
                w.writerow([k, t])
        print(f"merged {character}: {len(rows)} -> {merged}")


if __name__ == "__main__":
    main()
