"""Generate per-line WAV files from lines_ru TSV via Coqui XTTS v2 (voice cloning)."""
from __future__ import annotations

import argparse
import csv
import os
import re
import sys
import time
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from xtts_audio import cleanup_wav, normalize_tts_text

LINES_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\lines_ru")
REFS_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\refs")
OUT_ROOT = Path(r"C:\Temp\SovereignSyndicateVoice\voice")
CHARACTERS = ("atticus", "clara", "otto")
MODEL_NAME = "tts_models/multilingual/multi-dataset/xtts_v2"
INVALID_CHARS = re.compile(r'[<>:"/\\|?*]')


def sanitize_key(key: str) -> str:
    value = key.strip()
    value = re.sub(r'[""«»\'\u201C\u201D\u2018\u2019]', "", value)
    value = re.sub(r"-\d+$", "", value)
    value = value.replace("\u2014", " ").replace("\u2013", " ").replace("—", " ")
    value = re.sub(r"\.{2,}$", "", value)
    value = re.sub(r"\s+", " ", value).strip()
    value = INVALID_CHARS.sub("_", value)
    return value.strip("-_. ")


def safe_print(message: str) -> None:
    encoding = getattr(sys.stdout, "encoding", None) or "utf-8"
    print(message.encode(encoding, errors="replace").decode(encoding, errors="replace"), flush=True)


def read_tsv(path: Path) -> list[tuple[str, str]]:
    rows: list[tuple[str, str]] = []
    with path.open("r", encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        for row in reader:
            key = (row.get("key") or "").strip()
            text = (row.get("text_ru") or "").strip()
            if key and text:
                rows.append((key, text))
    return rows


def load_tts():
    # Non-commercial CPML: https://coqui.ai/cpml — auto-accept for non-interactive batch runs.
    os.environ.setdefault("COQUI_TOS_AGREED", "1")

    try:
        import torch
        from TTS.api import TTS
    except ImportError as exc:
        raise SystemExit(
            "coqui-tts not installed. See requirements-voice.txt and docs/VOICEOVER_HANDOFF.md"
        ) from exc

    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"XTTS device: {device}")
    tts = TTS(MODEL_NAME).to(device)
    return tts


def generate_for_character(
    tts,
    character: str,
    lines: list[tuple[str, str]],
    *,
    out_root: Path,
    ref_path: Path,
    force: bool,
    dry_run: bool,
) -> tuple[int, int, int]:
    out_dir = out_root / character
    out_dir.mkdir(parents=True, exist_ok=True)
    created = skipped = failed = 0

    for idx, (key, text) in enumerate(lines, start=1):
        safe_key = sanitize_key(key)
        if len(text) < 3 or not safe_key:
            continue
        out_wav = out_dir / f"{safe_key}.wav"
        if out_wav.exists() and not force:
            skipped += 1
            safe_print(f"[skip] {character} {safe_key}")
            continue

        safe_print(f"[{idx}/{len(lines)}] {character} {safe_key} ({len(text)} chars)")
        if dry_run:
            created += 1
            continue

        try:
            tts_text = normalize_tts_text(text)
            if len(tts_text) < 3:
                continue
            tts.tts_to_file(
                text=tts_text,
                speaker_wav=str(ref_path),
                language="ru",
                file_path=str(out_wav),
                split_sentences=False,
            )
            cleanup_wav(out_wav)
            created += 1
            ru_alias = sanitize_key(text)
            if ru_alias and ru_alias != safe_key:
                alias_wav = out_dir / f"{ru_alias}.wav"
                if not alias_wav.exists() or force:
                    alias_wav.write_bytes(out_wav.read_bytes())
        except Exception as exc:  # ponytail: log and continue batch
            failed += 1
            print(f"[fail] {character} {safe_key}: {exc}", file=sys.stderr)

    return created, skipped, failed


def write_manifest(out_root: Path, characters: list[str]) -> Path:
    manifest = out_root / "manifest.tsv"
    with manifest.open("w", encoding="utf-8", newline="") as f:
        f.write("character\tkey\twav_path\n")
        for character in characters:
            char_dir = out_root / character
            if not char_dir.is_dir():
                continue
            for wav in sorted(char_dir.glob("*.wav")):
                f.write(f"{character}\t{wav.stem}\t{wav}\n")
    return manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Russian VO wav files via XTTS v2")
    parser.add_argument(
        "--characters",
        nargs="+",
        choices=CHARACTERS,
        default=list(CHARACTERS),
        help="Characters to process (default: all)",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Max lines per character (0 = all; use 5-10 for pilot)",
    )
    parser.add_argument("--force", action="store_true", help="Regenerate even if wav exists")
    parser.add_argument("--dry-run", action="store_true", help="List work without calling XTTS")
    parser.add_argument("--lines-dir", type=Path, default=LINES_DIR)
    parser.add_argument("--refs-dir", type=Path, default=REFS_DIR)
    parser.add_argument(
        "--keys",
        nargs="+",
        default=None,
        help="Only generate these localization keys (exact match)",
    )
    parser.add_argument(
        "--source",
        choices=("all", "csv", "dialogue"),
        default="all",
        help="Line source: all_{char}.tsv (default), {char}.tsv, or dialogue_{char}.tsv",
    )
    parser.add_argument("--out-dir", type=Path, default=OUT_ROOT)
    return parser.parse_args()


def resolve_tsv_path(lines_dir: Path, character: str, source: str) -> Path:
    if source == "all":
        return lines_dir / f"all_{character}.tsv"
    if source == "dialogue":
        return lines_dir / f"dialogue_{character}.tsv"
    return lines_dir / f"{character}.tsv"
def main() -> None:
    args = parse_args()
    started = time.time()

    tts = None if args.dry_run else load_tts()
    totals = {"created": 0, "skipped": 0, "failed": 0}

    for character in args.characters:
        tsv_path = resolve_tsv_path(args.lines_dir, character, args.source)
        ref_path = args.refs_dir / f"{character}_ref.wav"
        if not tsv_path.is_file():
            print(f"[warn] missing TSV: {tsv_path}", file=sys.stderr)
            continue
        if not ref_path.is_file():
            print(f"[warn] missing ref wav: {ref_path}", file=sys.stderr)
            continue

        lines = read_tsv(tsv_path)
        if args.keys:
            key_set = set(args.keys)
            lines = [(k, t) for k, t in lines if k in key_set]
        if args.limit > 0:
            lines = lines[: args.limit]

        created, skipped, failed = generate_for_character(
            tts,
            character,
            lines,
            out_root=args.out_dir,
            ref_path=ref_path,
            force=args.force,
            dry_run=args.dry_run,
        )
        totals["created"] += created
        totals["skipped"] += skipped
        totals["failed"] += failed

    manifest = write_manifest(args.out_dir, list(args.characters))
    elapsed = time.time() - started
    print(
        f"Done in {elapsed:.1f}s: created={totals['created']} "
        f"skipped={totals['skipped']} failed={totals['failed']}"
    )
    print(f"Manifest: {manifest}")


if __name__ == "__main__":
    main()
