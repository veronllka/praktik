from pathlib import Path
import re

import joblib
import pandas as pd
from sklearn.metrics.pairwise import cosine_similarity

from .config import Settings
from .schemas import SimilarExample


class ModelArtifactsUnavailable(RuntimeError):
    pass


def preprocess_text(value: str) -> str:
    text = str(value or "").lower().replace("ё", "е")
    text = re.sub(r"[^0-9a-zа-я\s-]", " ", text)
    text = re.sub(r"[-_]+", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


class TaskClassifier:
    def __init__(self, settings: Settings):
        self.settings = settings
        self.model = None
        self.vectorizer = None
        self.label_encoder = None
        self.dataset: pd.DataFrame | None = None

    @property
    def artifacts_loaded(self) -> bool:
        return self.model is not None and self.vectorizer is not None and self.label_encoder is not None

    @property
    def artifacts_available(self) -> bool:
        return all(
            path.exists()
            for path in (
                self.settings.model_path,
                self.settings.vectorizer_path,
                self.settings.label_encoder_path,
            )
        )

    @property
    def dataset_available(self) -> bool:
        return self.settings.dataset_path.exists()

    def try_load(self) -> bool:
        if self.artifacts_loaded:
            return True

        if not self.artifacts_available:
            return False

        self.model = joblib.load(self.settings.model_path)
        self.vectorizer = joblib.load(self.settings.vectorizer_path)
        self.label_encoder = joblib.load(self.settings.label_encoder_path)
        return True

    def ensure_loaded(self) -> None:
        if self.try_load():
            return

        missing = [
            str(path)
            for path in (
                self.settings.model_path,
                self.settings.vectorizer_path,
                self.settings.label_encoder_path,
            )
            if not path.exists()
        ]
        raise ModelArtifactsUnavailable(
            "Model artifacts are not available. Run notebooks/pp12_task_classifier.ipynb first. "
            f"Missing: {', '.join(missing)}"
        )

    def load_dataset(self) -> pd.DataFrame:
        if self.dataset is not None:
            return self.dataset

        dataset_path: Path = self.settings.dataset_path
        if not dataset_path.exists():
            raise FileNotFoundError(f"Dataset not found: {dataset_path}")

        dataset = pd.read_csv(dataset_path)
        dataset["title_clean"] = dataset["title"].apply(preprocess_text)
        self.dataset = dataset
        return dataset

    def predict_category(self, title: str) -> str:
        self.ensure_loaded()
        cleaned_title = preprocess_text(title)
        features = self.vectorizer.transform([cleaned_title])
        predicted_label = self.model.predict(features)
        return str(self.label_encoder.inverse_transform(predicted_label)[0])

    def find_similar_examples(
        self,
        *,
        title: str,
        predicted_category: str,
        top_k: int | None = None,
    ) -> list[SimilarExample]:
        self.ensure_loaded()
        dataset = self.load_dataset()
        top_k = top_k or self.settings.max_similar_examples

        candidates = dataset[dataset["category"] == predicted_category].copy()
        if candidates.empty:
            candidates = dataset.copy()

        query_vector = self.vectorizer.transform([preprocess_text(title)])
        candidate_vectors = self.vectorizer.transform(candidates["title_clean"])
        similarities = cosine_similarity(query_vector, candidate_vectors).flatten()

        candidates["similarity"] = similarities
        candidates = candidates.sort_values(
            by=["similarity", "id"],
            ascending=[False, True],
        ).head(top_k)

        return [
            SimilarExample(
                id=int(row.id),
                title=str(row.title),
                category=str(row.category),
                reference_description=str(row.reference_description),
                similarity=round(float(row.similarity), 4),
                source_type=str(row.source_type),
            )
            for row in candidates.itertuples(index=False)
        ]

    def model_info(self) -> dict[str, str | bool]:
        return {
            "classifier": self.model.__class__.__name__ if self.model is not None else "not_loaded",
            "vectorizer": self.vectorizer.__class__.__name__ if self.vectorizer is not None else "not_loaded",
            "label_encoder": self.label_encoder.__class__.__name__ if self.label_encoder is not None else "not_loaded",
            "artifacts_loaded": self.artifacts_loaded,
        }
