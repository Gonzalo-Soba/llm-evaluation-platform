import re
from pathlib import Path

PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts"
DEFAULT_PROMPT_VERSION = "v1"

_VERSION_PATTERN = re.compile(r"^v[0-9]+$")


def prompt_exists(version: str) -> bool:
    # The pattern rules out path separators, so the version can only name a file inside PROMPTS_DIR.
    return bool(_VERSION_PATTERN.match(version)) and (PROMPTS_DIR / f"{version}.txt").is_file()


def load_prompt(version: str | None) -> str:
    version = version or DEFAULT_PROMPT_VERSION
    if not prompt_exists(version):
        raise ValueError(f"Unknown prompt version: {version}")
    return (PROMPTS_DIR / f"{version}.txt").read_text(encoding="utf-8")
