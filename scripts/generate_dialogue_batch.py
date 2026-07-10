"""Generate missing dialogue wavs from prefetch_queue.tsv (spawned by voice mod)."""
from __future__ import annotations

import argparse
import csv
import os
import sys
import time
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from xtts_audio import cleanup_wav, normalize_tts_text

REFS_DIR = Path(r"C:\Temp\SovereignSyndicateVoice\refs")
DEFAULT_QUEUE = Path(r"C:\Temp\SovereignSyndicateVoice\prefetch_queue.tsv")
DEFAULT_PRIORITY = Path(r"C:\Temp\SovereignSyndicateVoice\prefetch_priority.tsv")
DEFAULT_OUT = Path(
    r"C:\Program Files (x86)\Steam\steamapps\common\Sovereign Syndicate"
    r"\Mods\SovereignSyndicateVoice\voice"
)
LOCK_PATH = Path(r"C:\Temp\SovereignSyndicateVoice\prefetch.lock")
SHUTDOWN_PATH = Path(r"C:\Temp\SovereignSyndicateVoice\prefetch_shutdown")
LOG_PATH = Path(r"C:\Temp\SovereignSyndicateVoice\prefetch.log")


def log(msg: str) -> None:
    LOG_PATH.parent.mkdir(parents=True, exist_ok=True)
    with LOG_PATH.open("a", encoding="utf-8") as f:
        f.write(msg + "\n")
    print(msg)


def normalize_row(row: dict[str, str]) -> dict[str, str]:
    return {(k or "").lstrip("\ufeff").strip(): (v or "").strip() for k, v in row.items()}


def read_tsv(path: Path) -> list[tuple[str, str, str]]:
    if not path.is_file():
        return []

    rows: list[tuple[str, str, str]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f, delimiter="\t")
        for raw in reader:
            row = normalize_row(raw)
            character = row.get("character", "")
            key = row.get("key", "")
            text = row.get("text_ru", "")
            if character and key and text:
                rows.append((character, key, text))
    return rows


def merge_rows(priority: list[tuple[str, str, str]], queue: list[tuple[str, str, str]]) -> list[tuple[str, str, str]]:
    merged: list[tuple[str, str, str]] = []
    seen: set[str] = set()
    for source in (priority, queue):
        for row in source:
            key = row[1]
            if key in seen:
                continue
            seen.add(key)
            merged.append(row)
    return merged


def pop_priority_key(key: str, priority_path: Path) -> None:
    if not priority_path.is_file():
        return

    rows = read_tsv(priority_path)
    remaining = [row for row in rows if row[1] != key]
    if len(remaining) == len(rows):
        return

    if remaining:
        with priority_path.open("w", encoding="utf-8", newline="") as f:
            f.write("character\tkey\ttext_ru\n")
            for character, row_key, text in remaining:
                f.write(f"{character}\t{row_key}\t{text}\n")
    else:
        priority_path.unlink(missing_ok=True)


def configure_torch_runtime() -> None:
    os.environ.setdefault("OMP_NUM_THREADS", "4")
    os.environ.setdefault("MKL_NUM_THREADS", "4")
    import torch

    torch.set_num_threads(4)


def load_tts():
    os.environ.setdefault("COQUI_TOS_AGREED", "1")
    configure_torch_runtime()
    import torch
    from TTS.api import TTS

    device = "cuda" if torch.cuda.is_available() else "cpu"
    log(f"XTTS device: {device}")
    return TTS("tts_models/multilingual/multi-dataset/xtts_v2").to(device)


def unload_tts(tts) -> None:
    import gc

    import torch

    del tts
    gc.collect()
    if torch.cuda.is_available():
        torch.cuda.empty_cache()


def generate_one(
    tts,
    out_dir: Path,
    priority_path: Path,
    character: str,
    key: str,
    text: str,
) -> bool:
    out_wav = out_dir / character / f"{key}.wav"
    if out_wav.exists():
        pop_priority_key(key, priority_path)
        return False

    ref = REFS_DIR / f"{character}_ref.wav"
    if not ref.is_file():
        log(f"[skip] no ref: {ref}")
        return False

    tts_text = normalize_tts_text(text)
    if len(tts_text) < 2:
        log(f"[skip] empty after normalize: {key}")
        return False

    out_wav.parent.mkdir(parents=True, exist_ok=True)
    log(f"[gen] {character} {key} ({len(tts_text)} chars)")
    tts.tts_to_file(
        text=tts_text,
        speaker_wav=str(ref),
        language="ru",
        file_path=str(out_wav),
        split_sentences=False,
    )
    cleanup_wav(out_wav)
    pop_priority_key(key, priority_path)
    return True


def next_job(
    args: argparse.Namespace,
    pending: list[tuple[str, str, str]],
) -> tuple[list[tuple[str, str, str]], tuple[str, str, str] | None]:
    priority = read_tsv(args.priority)
    if priority:
        hot = priority[0]
        if hot not in pending:
            pending.insert(0, hot)
        elif pending and pending[0] != hot:
            pending.remove(hot)
            pending.insert(0, hot)

    while pending:
        character, key, text = pending.pop(0)
        out_wav = args.out_dir / character / f"{key}.wav"
        if out_wav.exists():
            pop_priority_key(key, args.priority)
            continue
        return pending, (character, key, text)

    merged = merge_rows(read_tsv(args.priority), read_tsv(args.queue))
    if args.limit > 0:
        merged = merged[: args.limit]
    fresh: list[tuple[str, str, str]] = []
    for row in merged:
        character, key, text = row
        if not (args.out_dir / character / f"{key}.wav").exists():
            fresh.append(row)
    if not fresh:
        return [], None
    return fresh[1:], fresh[0]


def run_batch(tts, args: argparse.Namespace) -> tuple[int, int]:
    created = skipped = 0
    pending: list[tuple[str, str, str]] = []
    while True:
        pending, job = next_job(args, pending)
        if job is None:
            break
        character, key, text = job
        if generate_one(tts, args.out_dir, args.priority, character, key, text):
            created += 1
        else:
            skipped += 1
        if args.limit > 0 and (created + skipped) >= args.limit:
            break
    return created, skipped


def should_shutdown() -> bool:
    return SHUTDOWN_PATH.is_file()


def write_lock_pid() -> None:
    LOCK_PATH.write_text(f"pid:{os.getpid()}", encoding="utf-8")


def run_daemon(args: argparse.Namespace) -> None:
    LOCK_PATH.parent.mkdir(parents=True, exist_ok=True)
    SHUTDOWN_PATH.unlink(missing_ok=True)
    write_lock_pid()
    log("XTTS warm worker started")
    tts = None
    try:
        tts = load_tts()
        idle_loops = 0
        skip_streak = 0
        max_idle = max(1, args.idle_exit_sec * 2)
        pending: list[tuple[str, str, str]] = []
        while idle_loops < max_idle:
            if should_shutdown():
                log("XTTS worker shutdown requested")
                break
            pending, job = next_job(args, pending)
            if job is None:
                idle_loops += 1
                time.sleep(0.5)
                continue
            idle_loops = 0
            character, key, text = job
            if generate_one(tts, args.out_dir, args.priority, character, key, text):
                skip_streak = 0
            else:
                skip_streak += 1
                if skip_streak >= 8:
                    idle_loops += 1
            if should_shutdown():
                log("XTTS worker shutdown requested")
                break
        log("XTTS worker exit")
    finally:
        if tts is not None:
            unload_tts(tts)
        SHUTDOWN_PATH.unlink(missing_ok=True)
        if LOCK_PATH.exists():
            LOCK_PATH.unlink()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--queue", type=Path, default=DEFAULT_QUEUE)
    parser.add_argument("--priority", type=Path, default=DEFAULT_PRIORITY)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--daemon", action="store_true", help="Keep model loaded; poll queue")
    parser.add_argument("--idle-exit-sec", type=int, default=300)
    args = parser.parse_args()

    if args.daemon:
        run_daemon(args)
        return

    LOCK_PATH.parent.mkdir(parents=True, exist_ok=True)
    if not LOCK_PATH.exists():
        write_lock_pid()
    log(f"XTTS batch started (limit={args.limit})")
    tts = None
    try:
        if not args.queue.is_file() and not args.priority.is_file():
            log("queue missing")
            return
        tts = load_tts()
        created, skipped = run_batch(tts, args)
        log(f"prefetch done: created={created} skipped={skipped}")
    finally:
        if tts is not None:
            unload_tts(tts)
        if LOCK_PATH.exists():
            LOCK_PATH.unlink()


if __name__ == "__main__":
    main()
