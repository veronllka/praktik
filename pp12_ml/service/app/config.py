from dataclasses import dataclass
from pathlib import Path
import os


SERVICE_ROOT = Path(__file__).resolve().parents[1]
PP12_ROOT = SERVICE_ROOT.parent
DEFAULT_LMSTUDIO_BASE_URL = "http://127.0.0.1:1234"
DEFAULT_LMSTUDIO_MODEL = "vikhr-qwen-2.5-1.5b-instruct"


def _optional_env(name: str) -> str | None:
    value = os.getenv(name)
    return value.strip() if value and value.strip() else None


def _env_path(name: str, fallback: Path) -> Path:
    value = _optional_env(name)
    return Path(value) if value else fallback


def _env_float(name: str, fallback: float) -> float:
    value = _optional_env(name)
    if value is None:
        return fallback
    try:
        return float(value)
    except ValueError:
        return fallback


def _env_int(name: str, fallback: int) -> int:
    value = _optional_env(name)
    if value is None:
        return fallback
    try:
        return int(value)
    except ValueError:
        return fallback


@dataclass(frozen=True)
class Settings:
    dataset_path: Path = _env_path(
        "PP12_DATASET_PATH",
        PP12_ROOT / "data" / "construction_tasks_dataset.csv",
    )
    artifacts_dir: Path = _env_path(
        "PP12_ARTIFACTS_DIR",
        PP12_ROOT / "artifacts",
    )
    lmstudio_base_url: str = _optional_env("LMSTUDIO_BASE_URL") or DEFAULT_LMSTUDIO_BASE_URL
    lmstudio_model: str = _optional_env("LMSTUDIO_MODEL") or DEFAULT_LMSTUDIO_MODEL
    lmstudio_api_token: str | None = _optional_env("LMSTUDIO_API_TOKEN")
    lmstudio_timeout_seconds: float = _env_float("LMSTUDIO_TIMEOUT_SECONDS", 45.0)
    max_similar_examples: int = _env_int("PP12_MAX_SIMILAR_EXAMPLES", 3)

    @property
    def model_path(self) -> Path:
        return self.artifacts_dir / "best_model.joblib"

    @property
    def vectorizer_path(self) -> Path:
        return self.artifacts_dir / "tfidf_vectorizer.joblib"

    @property
    def label_encoder_path(self) -> Path:
        return self.artifacts_dir / "label_encoder.joblib"


def get_settings() -> Settings:
    return Settings()
