"""XTTS text prep + WAV edge cleanup (trim breath, fade clicks)."""
from __future__ import annotations

import re
from pathlib import Path

_QUOTE_RE = re.compile(r'[""«»\'\u201C\u201D\u2018\u2019]')
_TAG_RE = re.compile(r"\{[^}]+\}")
_HTML_RE = re.compile(r"<[^>]+>")
_SPACE_RE = re.compile(r"\s+")


def normalize_tts_text(text: str) -> str:
    """Strip game/UI markup that confuses XTTS edge phonemes (и, о clicks)."""
    value = _HTML_RE.sub("", text or "")
    value = _TAG_RE.sub("", value)
    value = _QUOTE_RE.sub("", value)
    value = value.replace("\u2014", " ").replace("\u2013", " ").replace("—", " ")
    value = _SPACE_RE.sub(" ", value).strip(" \t\n.,;:!?…-")
    return value.strip()


def cleanup_wav(
    path: Path,
    *,
    fade_ms: int = 35,
    gate_ratio: float = 0.018,
    preroll_ms: int = 8,
    tail_ms: int = 12,
) -> None:
    """Trim low-level XTTS breath at edges and fade to remove vowel clicks."""
    import numpy as np
    from scipy.io import wavfile

    sample_rate, data = wavfile.read(path)
    if data.size == 0:
        return

    if data.dtype == np.int16:
        samples = data.astype(np.float32) / 32768.0
    elif data.dtype == np.int32:
        samples = data.astype(np.float32) / 2147483648.0
    else:
        samples = data.astype(np.float32)

    if samples.ndim > 1:
        samples = samples.mean(axis=1)

    peak = float(np.max(np.abs(samples)))
    if peak < 1e-6:
        return

    threshold = peak * gate_ratio
    loud = np.flatnonzero(np.abs(samples) > threshold)
    if loud.size == 0:
        return

    start = max(0, int(loud[0]) - int(sample_rate * preroll_ms / 1000))
    end = min(len(samples), int(loud[-1]) + int(sample_rate * tail_ms / 1000) + 1)
    trimmed = samples[start:end]

    fade_samples = max(1, int(sample_rate * fade_ms / 1000))
    if len(trimmed) > fade_samples * 2:
        ramp = np.linspace(0.0, 1.0, fade_samples, dtype=np.float32)
        trimmed[:fade_samples] *= ramp
        trimmed[-fade_samples:] *= ramp[::-1]

    trimmed = np.clip(trimmed, -1.0, 1.0)
    wavfile.write(path, sample_rate, (trimmed * 32767.0).astype(np.int16))
