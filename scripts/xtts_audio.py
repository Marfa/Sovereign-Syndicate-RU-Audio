"""XTTS text prep + WAV edge cleanup (trim breath, fade clicks)."""
from __future__ import annotations

import os
import re
from pathlib import Path

_QUOTE_RE = re.compile(r'[""«»\'\u201C\u201D\u2018\u2019]')
_TAG_RE = re.compile(r"\{([^}]+)\}")
_HTML_RE = re.compile(r"<[^>]+>")
_ACTION_RE = re.compile(r"\*+([^*]+)\*+")
_SPACE_RE = re.compile(r"\s+")
# Proper names that need forced 1st-syllable stress for XTTS.
_OTTO_NAME_RE = re.compile(
    r"(?i)\bо\u0301?[-\u00ad]?тт?о\u0301?\b|\bo\u0301?[-\u00ad]?tto\u0301?\b"
)
_SILAS_NAME_RE = re.compile(
    r"(?i)\bс\u0301?а\u0301?й[-\u00ad]?лас\u0301?\b|\bs\u0301?i\u0301?[-\u00ad]?las\u0301?\b"
)
_MOLLY_NAME_RE = re.compile(
    r"(?i)\bм\u0301?о\u0301?о?\u0301?[-\u00ad]?лл?и\u0301?\b|\bm\u0301?o\u0301?o?\u0301?[-\u00ad]?ll?y\u0301?\b"
)
# Silero marks stress as '+' before the stressed vowel: Мен+я → Меня́
_SILERO_PLUS_RE = re.compile(r"\+([АЕЁИОУЫЭЮЯаеёиоуыэюяAEIOUYaeiouy])")
_STRESS = "\u0301"  # combining acute
_CYR_WORD_RE = re.compile(r"[А-Яа-яЁё]")

_accentor = None
_accentor_failed = False


def unwrap_game_tags(text: str) -> str:
    """{Континента} -> Континента — game localization vars, not silent markup."""
    return _TAG_RE.sub(r"\1", text or "")


def _plain_name(word: str) -> str:
    return word.replace(_STRESS, "").replace("-", "").replace("\u00ad", "")


def stress_otto_name(text: str) -> str:
    """Force first-syllable stress for Отто (О́о-тто)."""

    def repl(match: re.Match[str]) -> str:
        plain = _plain_name(match.group(0))
        if plain.lower() == "otto":
            if plain.isupper():
                return f"O{_STRESS}O-TTO"
            if plain[0].isupper():
                return f"O{_STRESS}o-tto"
            return f"o{_STRESS}o-tto"
        if plain.isupper():
            return f"О{_STRESS}О-ТТО"
        if plain[0].isupper():
            return f"О{_STRESS}о-тто"
        return f"о{_STRESS}о-тто"

    return _OTTO_NAME_RE.sub(repl, text or "")


def stress_silas_name(text: str) -> str:
    """Force first-syllable stress for Сайлас (Са́й-лас)."""

    def repl(match: re.Match[str]) -> str:
        plain = _plain_name(match.group(0))
        if plain.lower() == "silas":
            if plain.isupper():
                return f"SI{_STRESS}-LAS"
            if plain[0].isupper():
                return f"Si{_STRESS}-las"
            return f"si{_STRESS}-las"
        if plain.isupper():
            return f"СА{_STRESS}Й-ЛАС"
        if plain[0].isupper():
            return f"Са{_STRESS}й-лас"
        return f"са{_STRESS}й-лас"

    return _SILAS_NAME_RE.sub(repl, text or "")


def stress_molly_name(text: str) -> str:
    """Force first-syllable stress for Молли (Мо́о-лли)."""

    def repl(match: re.Match[str]) -> str:
        plain = _plain_name(match.group(0))
        low = plain.lower()
        # Мо́о-лли → моолли after strip; normalize doubled first vowel.
        if low.startswith("моо"):
            low = "мо" + low[3:]
        if low.startswith("moo"):
            low = "mo" + low[3:]
        if low in ("molly", "molli"):
            if plain[0].isupper() and plain.isupper():
                return f"MO{_STRESS}O-LLY"
            if plain[0].isupper():
                return f"Mo{_STRESS}o-lly"
            return f"mo{_STRESS}o-lly"
        if low == "молли":
            if plain.isupper():
                return f"МО{_STRESS}О-ЛЛИ"
            if plain[0].isupper():
                return f"Мо{_STRESS}о-лли"
            return f"мо{_STRESS}о-лли"
        return match.group(0)
    return _MOLLY_NAME_RE.sub(repl, text or "")


def stress_proper_names(text: str) -> str:
    """Game proper names XTTS mis-stresses without explicit first-syllable cues."""
    value = stress_otto_name(text or "")
    value = stress_silas_name(value)
    return stress_molly_name(value)


def silero_plus_to_combining(text: str) -> str:
    """Convert Silero `+V` stress markers to Unicode combining acute on V."""
    return _SILERO_PLUS_RE.sub(lambda m: m.group(1) + _STRESS, text or "")


def _stress_enabled() -> bool:
    flag = (os.environ.get("SS_VOICE_STRESS") or os.environ.get("VOICE_STRESS") or "yo").strip().lower()
    return flag not in ("0", "false", "off", "no")


def get_accentor():
    """Lazy-load Silero Stress accentor (optional; None if missing/disabled)."""
    global _accentor, _accentor_failed
    if not _stress_enabled() or _accentor_failed:
        return None
    if _accentor is not None:
        return _accentor
    try:
        from silero_stress import load_accentor

        _accentor = load_accentor()
        return _accentor
    except Exception as exc:  # pragma: no cover - optional dependency
        _accentor_failed = True
        print(f"[stress] silero-stress unavailable ({exc}); continuing without auto-stress", flush=True)
        return None


def apply_auto_stress(text: str) -> str:
    """Prepare Russian text for XTTS via silero-stress.

    Default (SS_VOICE_STRESS=yo): only place «ё». Full per-word Unicode stress
    makes Coqui XTTS insert pauses between words.

      yo | 1 | true  — ё only (default, fluent)
      full           — stress + ё with combining acute (may pause)
      0 | off        — disabled

    Отто → О́о-тто, Сайлас → Са́й-лас, Молли → Мо́о-лли always after Silero.
    """
    value = text or ""
    if not value or not _CYR_WORD_RE.search(value):
        return stress_proper_names(value)

    mode = (os.environ.get("SS_VOICE_STRESS") or os.environ.get("VOICE_STRESS") or "yo").strip().lower()
    if mode in ("0", "false", "off", "no"):
        return stress_proper_names(value)

    accentor = get_accentor()
    if accentor is not None:
        try:
            if mode in ("full", "all", "stress"):
                marked = accentor(
                    value,
                    put_stress=True,
                    put_stress_homo=True,
                    put_yo=True,
                    put_yo_homo=True,
                    stress_single_vowel=False,
                )
                if isinstance(marked, str) and marked.strip():
                    value = silero_plus_to_combining(marked)
            else:
                marked = accentor(
                    value,
                    put_stress=False,
                    put_stress_homo=False,
                    put_yo=True,
                    put_yo_homo=True,
                    stress_single_vowel=False,
                )
                if isinstance(marked, str) and marked.strip():
                    value = re.sub(r"\+", "", marked)
        except Exception as exc:  # pragma: no cover
            print(f"[stress] accentor failed: {exc}", flush=True)

    value = stress_proper_names(value)
    return value


def normalize_tts_text(text: str) -> str:
    """Strip game/UI markup that confuses XTTS edge phonemes (и, о clicks)."""
    value = _ACTION_RE.sub(" ", text or "")
    value = _HTML_RE.sub("", value)
    value = unwrap_game_tags(value)
    value = _QUOTE_RE.sub("", value)
    value = value.replace("\u2014", " ").replace("\u2013", " ").replace("—", " ")
    value = _SPACE_RE.sub(" ", value).strip(" \t\n.,;:!?…-")
    value = apply_auto_stress(value)
    value = _SPACE_RE.sub(" ", value).strip()
    return value.strip()


if __name__ == "__main__":
    sample = "«Вы же с {Континента}? Испанец, не так ли?»"
    assert "Континента" in normalize_tts_text(sample)
    assert "{" not in normalize_tts_text(sample)
    assert "Вздыхает" not in normalize_tts_text("*Вздыхает* «Привет»")
    assert "Привет" in normalize_tts_text("*Вздыхает* «Привет»")
    otto = normalize_tts_text("Слушай, Отто, погоди.")
    assert f"О{_STRESS}о-тто" in otto, otto
    assert "Отто" not in otto.replace(f"О{_STRESS}о-тто", "")
    silas = normalize_tts_text("Сайлас, подожди.")
    assert f"Са{_STRESS}й-лас" in silas, silas
    molly = normalize_tts_text("Молли, здесь?")
    assert f"Мо{_STRESS}о-лли" in molly, molly
    converted = silero_plus_to_combining("Мен+я зов+ут")
    assert converted == f"Меня{_STRESS} зову{_STRESS}т", converted
    print("ok otto", otto.encode("unicode_escape").decode("ascii"))
    print("ok silas", silas.encode("unicode_escape").decode("ascii"))
    print("ok molly", molly.encode("unicode_escape").decode("ascii"))


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

    sample_rate, samples = _read_mono_f32(path)
    if samples is None or samples.size == 0:
        return

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

    _write_int16(path, sample_rate, trimmed)


def apply_character_voice_fx(path: Path, character: str) -> None:
    """Character-specific colour after XTTS / on Piper refs."""
    name = (character or "").strip().lower()
    if name == "otto":
        robotize_wav(path)
    elif name == "teddy":
        gnomeize_wav(path)
    elif name == "atticus":
        horseify_wav(path)


def _read_mono_f32(path: Path):
    import numpy as np
    from scipy.io import wavfile

    sample_rate, data = wavfile.read(path)
    if data.size == 0:
        return sample_rate, None

    if data.dtype == np.int16:
        samples = data.astype(np.float32) / 32768.0
    elif data.dtype == np.int32:
        samples = data.astype(np.float32) / 2147483648.0
    else:
        samples = data.astype(np.float32)

    if samples.ndim > 1:
        samples = samples.mean(axis=1)
    return sample_rate, samples.astype(np.float32, copy=False)


def _write_int16(path: Path, sample_rate: int, samples) -> None:
    import numpy as np
    from scipy.io import wavfile

    out = np.clip(samples, -1.0, 1.0)
    wavfile.write(path, sample_rate, (out * 32767.0).astype(np.int16))


def _normalize_peak(samples, peak_target: float = 0.92):
    import numpy as np

    peak = float(np.max(np.abs(samples)))
    if peak < 1e-6:
        return samples
    return (samples * (peak_target / peak)).astype(np.float32)


def _pitch_shift_keep_length(samples, semitones: float):
    """Crude pitch shift via resample (keeps duration, slight artefacts OK for stylization)."""
    import numpy as np
    from scipy.signal import resample

    if abs(semitones) < 0.05:
        return samples.astype(np.float32)
    factor = float(2.0 ** (semitones / 12.0))
    pitched_len = max(8, int(round(len(samples) / factor)))
    pitched = resample(samples, pitched_len)
    restored = resample(pitched, len(samples))
    return restored.astype(np.float32)


def robotize_wav(
    path: Path,
    *,
    carrier_hz: float = 55.0,
    wet: float = 0.55,
    comb_ms: float = 6.5,
    comb_feedback: float = 0.28,
    lowpass_hz: float = 3200.0,
    highpass_hz: float = 180.0,
) -> None:
    """Mechanical automaton (ring-mod + metallic comb)."""
    import numpy as np
    from scipy.signal import butter, lfilter, sosfilt

    sample_rate, samples = _read_mono_f32(path)
    if samples is None or samples.size == 0:
        return

    peak = float(np.max(np.abs(samples)))
    if peak < 1e-6:
        return
    samples = samples / peak

    nyq = sample_rate * 0.5
    hp = max(20.0, min(highpass_hz, nyq * 0.45))
    lp = max(hp + 200.0, min(lowpass_hz, nyq * 0.95))
    sos_hp = butter(2, hp / nyq, btype="highpass", output="sos")
    sos_lp = butter(2, lp / nyq, btype="lowpass", output="sos")
    band = sosfilt(sos_lp, sosfilt(sos_hp, samples))

    t = np.arange(len(band), dtype=np.float32) / float(sample_rate)
    carrier = np.sin(2.0 * np.pi * carrier_hz * t).astype(np.float32)
    ring = band * carrier
    trem = 0.72 + 0.28 * np.sign(np.sin(2.0 * np.pi * 28.0 * t))
    ring = ring * trem.astype(np.float32)

    delay = max(1, int(sample_rate * comb_ms / 1000.0))
    if delay < len(ring):
        b = np.array([1.0] + [0.0] * (delay - 1) + [comb_feedback], dtype=np.float32)
        a = np.array([1.0] + [0.0] * (delay - 1) + [-comb_feedback * 0.85], dtype=np.float32)
        comb = lfilter(b, a, ring).astype(np.float32)
    else:
        comb = ring

    drive = np.tanh(comb * 1.8).astype(np.float32)
    wet = float(np.clip(wet, 0.0, 1.0))
    out = ((1.0 - wet) * band + wet * drive).astype(np.float32)
    _write_int16(path, sample_rate, _normalize_peak(out))


def gnomeize_wav(
    path: Path,
    *,
    semitones: float = 5.5,
    bright_hz: float = 2800.0,
    bright_gain: float = 1.45,
    wet: float = 0.85,
) -> None:
    """Smaller / higher 'gnome' voice: pitch up + bright shelf + light bounce."""
    import numpy as np
    from scipy.signal import butter, sosfilt

    sample_rate, samples = _read_mono_f32(path)
    if samples is None or samples.size == 0:
        return

    peak = float(np.max(np.abs(samples)))
    if peak < 1e-6:
        return
    dry = samples / peak

    pitched = _pitch_shift_keep_length(dry, semitones)

    nyq = sample_rate * 0.5
    sos_hp = butter(2, min(220.0, nyq * 0.4) / nyq, btype="highpass", output="sos")
    body = sosfilt(sos_hp, pitched)

    sos_br = butter(2, min(bright_hz, nyq * 0.9) / nyq, btype="highpass", output="sos")
    bright = sosfilt(sos_br, body) * bright_gain
    gnome = body + bright

    t = np.arange(len(gnome), dtype=np.float32) / float(sample_rate)
    bounce = 0.88 + 0.12 * np.sin(2.0 * np.pi * 7.5 * t)
    gnome = gnome * bounce.astype(np.float32)

    wet = float(np.clip(wet, 0.0, 1.0))
    out = ((1.0 - wet) * dry + wet * gnome).astype(np.float32)
    _write_int16(path, sample_rate, _normalize_peak(out))


def horseify_wav(
    path: Path,
    *,
    semitones: float = -4.0,
    vibrato_hz: float = 5.2,
    vibrato_depth: float = 0.035,
    nasal_hz: float = 1450.0,
    chest_hz: float = 420.0,
    wet: float = 0.8,
) -> None:
    """Deep equine colour: lower pitch, chest/nasal formants, soft nicker vibrato."""
    import numpy as np
    from scipy.signal import butter, lfilter, sosfilt

    sample_rate, samples = _read_mono_f32(path)
    if samples is None or samples.size == 0:
        return

    peak = float(np.max(np.abs(samples)))
    if peak < 1e-6:
        return
    dry = samples / peak

    pitched = _pitch_shift_keep_length(dry, semitones)

    nyq = sample_rate * 0.5
    sos_chest = butter(
        2,
        [max(80.0, chest_hz * 0.55) / nyq, min(chest_hz * 1.7, nyq * 0.9) / nyq],
        btype="bandpass",
        output="sos",
    )
    chest = sosfilt(sos_chest, pitched) * 1.35

    sos_nasal = butter(
        2,
        [max(600.0, nasal_hz * 0.65) / nyq, min(nasal_hz * 1.45, nyq * 0.95) / nyq],
        btype="bandpass",
        output="sos",
    )
    nasal = sosfilt(sos_nasal, pitched) * 1.15

    horse = pitched * 0.55 + chest * 0.9 + nasal * 0.75

    t = np.arange(len(horse), dtype=np.float32) / float(sample_rate)
    vib = (1.0 + vibrato_depth * np.sin(2.0 * np.pi * vibrato_hz * t)).astype(np.float32)
    horse = horse * vib

    delay = max(1, int(sample_rate * 0.014))
    if delay < len(horse):
        b = np.array([1.0] + [0.0] * (delay - 1) + [0.22], dtype=np.float32)
        a = np.array([1.0] + [0.0] * (delay - 1) + [-0.18], dtype=np.float32)
        horse = lfilter(b, a, horse).astype(np.float32)

    sos_lp = butter(2, min(3800.0, nyq * 0.92) / nyq, btype="lowpass", output="sos")
    horse = sosfilt(sos_lp, horse)

    wet = float(np.clip(wet, 0.0, 1.0))
    out = ((1.0 - wet) * dry + wet * horse).astype(np.float32)
    _write_int16(path, sample_rate, _normalize_peak(out))
