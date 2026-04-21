from typing import Any

from pydantic import BaseModel, Field


class PredictCategoryRequest(BaseModel):
    title: str = Field(..., min_length=1, description="Короткое название задачи.")


class PredictCategoryResponse(BaseModel):
    title: str
    predicted_category: str


class GenerateDescriptionRequest(BaseModel):
    title: str = Field(..., min_length=1, description="Короткое название задачи.")
    site_name: str | None = Field(default=None, description="Название объекта.")
    existing_description: str | None = Field(default=None, description="Текущее описание из карточки задачи.")


class SimilarExample(BaseModel):
    id: int
    title: str
    category: str
    reference_description: str
    similarity: float | None = None
    source_type: str


class GenerateDescriptionResponse(BaseModel):
    title: str
    predicted_category: str
    similar_examples: list[SimilarExample]
    prompt_preview: str
    generated_description: str | None
    used_model_info: dict[str, Any]


class HealthResponse(BaseModel):
    status: str
    artifacts_loaded: bool
    artifacts_available: bool
    dataset_available: bool
    lmstudio_configured: bool
    lmstudio_base_url: str | None
    lmstudio_model: str | None
    model_path: str
    vectorizer_path: str
    label_encoder_path: str
