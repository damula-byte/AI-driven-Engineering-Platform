import sys
import os
import json
import time
import random
from pathlib import Path

# ─── PyInstaller-safe base directory ─────────────────────────────────────────
if getattr(sys, 'frozen', False):
    exe_dir = Path(sys.executable).resolve().parent
    BASE_DIR = exe_dir
    for candidate in [exe_dir, exe_dir.parent, exe_dir.parent.parent, exe_dir.parent.parent.parent]:
        if (candidate / "chroma_db_data").exists() or (candidate / "api_cooldown.json").exists():
            BASE_DIR = candidate
            break
else:
    BASE_DIR = Path(__file__).resolve().parent

chatgpt_key = "" #insert your key here
list_keys_gemini = [] #insert your keys here, e.g. ["key1", "key2", "key3"]

# ─── Persistent cooldown file ─────────────────────────────────────────────────
_COOLDOWN_FILE = BASE_DIR / "api_cooldown.json"

# ─── ChromaDB path ────────────────────────────────────────────────────────────
CHROMA_DB_PATH = str(BASE_DIR / "chroma_db_data")

def get_gpt_key():
    return chatgpt_key

# KEY SELECTION — random two-draw (your original logic, unchanged)
def get_next_api_key() -> str:
    if not list_keys_gemini:
        raise RuntimeError("[app_secrets] list_keys_gemini is empty.")
    if len(list_keys_gemini) < 2:
        return list_keys_gemini[0]
    key1 = random.choice(list_keys_gemini)
    key2 = random.choice(list_keys_gemini)
    while key1 == key2:
        key2 = random.choice(list_keys_gemini)
    return key1 if random.random() < 0.5 else key2




# COOLDOWN — disk-persisted, subprocess-safe
def _read_last_call_time() -> float:
    try:
        data = json.loads(_COOLDOWN_FILE.read_text(encoding="utf-8"))
        return float(data.get("last_call", 0.0))
    except Exception:
        return 0.0

def record_api_call():
    """Call immediately AFTER a successful llm.invoke()."""
    try:
        _COOLDOWN_FILE.write_text(
            json.dumps({"last_call": time.time()}),
            encoding="utf-8"
        )
    except Exception:
        pass

def enforce_cooldown(min_seconds: float = 5.0):
    """Call BEFORE llm.invoke(). Blocks until min_seconds have elapsed since last call."""
    last_call = _read_last_call_time()
    elapsed   = time.time() - last_call
    if elapsed < min_seconds:
        wait = round(min_seconds - elapsed, 1)
        print(f"[COOLDOWN] Last call was {elapsed:.1f}s ago — waiting {wait}s...",
              file=sys.stderr, flush=True)
        time.sleep(wait)


# ERROR CLASSIFICATION
def classify_api_error(exc: Exception) -> str:
    """
    Returns: 'RPM' | 'RPD' | '503' | 'AUTH' | 'OTHER'

    RPM  → rate limit per minute → request rejected, RPD unaffected → can retry
    RPD  → daily quota gone      → only key rotation helps
    503  → server overload       → not a quota event, RPD unaffected → can retry
    AUTH → bad key               → rotate immediately
    """
    msg = str(exc).lower()
    if "503" in msg or "service unavailable" in msg or "overloaded" in msg:
        return "503"
    if "429" in msg or "resource_exhausted" in msg or "rate_limit" in msg:
        if "per day" in msg or "daily" in msg or "quota" in msg:
            return "RPD"
        return "RPM"
    if "401" in msg or "403" in msg or ("invalid" in msg and "key" in msg):
        return "AUTH"
    return "OTHER"