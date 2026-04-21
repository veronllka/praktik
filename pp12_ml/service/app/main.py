from fastapi import FastAPI, HTTPException, status

from .config import get_settings
from .lmstudio_client import LmStudioClient
from .ml_utils import ModelArtifactsUnavailable, TaskClassifier
from .prompt_builder import build_description_prompt
from .schemas import (
    GenerateDescriptionRequest,
    GenerateDescriptionResponse,
    HealthResponse,
    PredictCategoryRequest,
    PredictCategoryResponse,
)


settings = get_settings()
classifier = TaskClassifier(settings)
lmstudio_client = LmStudioClient(settings)

app = FastAPI(
    title="PP12 ML Task Description Service",
    version="0.1.0",
    description="Task title classifier and LM Studio prompt helper for PP12.",
)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    classifier.try_load()
    return HealthResponse(
        status="ok",
        artifacts_loaded=classifier.artifacts_loaded,
        artifacts_available=classifier.artifacts_available,
        dataset_available=classifier.dataset_available,
        lmstudio_configured=lmstudio_client.is_configured,
        lmstudio_base_url=settings.lmstudio_base_url,
        lmstudio_model=settings.lmstudio_model,
        model_path=str(settings.model_path),
        vectorizer_path=str(settings.vectorizer_path),
        label_encoder_path=str(settings.label_encoder_path),
    )


@app.post("/predict-category", response_model=PredictCategoryResponse)
def predict_category(request: PredictCategoryRequest) -> PredictCategoryResponse:
    try:
        predicted_category = classifier.predict_category(request.title)
    except ModelArtifactsUnavailable as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail=str(exc),
        ) from exc

    return PredictCategoryResponse(
        title=request.title,
        predicted_category=predicted_category,
    )


@app.post("/generate-description", response_model=GenerateDescriptionResponse)
async def generate_description(request: GenerateDescriptionRequest) -> GenerateDescriptionResponse:
    try:
        predicted_category = classifier.predict_category(request.title)
        similar_examples = classifier.find_similar_examples(
            title=request.title,
            predicted_category=predicted_category,
            top_k=settings.max_similar_examples,
        )
    except ModelArtifactsUnavailable as exc:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail=str(exc),
        ) from exc

    prompt = build_description_prompt(
        title=request.title,
        predicted_category=predicted_category,
        site_name=request.site_name,
        existing_description=request.existing_description,
        similar_examples=similar_examples,
    )

    generated_description, llm_info = await lmstudio_client.generate_description(prompt)
    used_model_info = {
        **classifier.model_info(),
        **llm_info,
    }

    return GenerateDescriptionResponse(
        title=request.title,
        predicted_category=predicted_category,
        similar_examples=similar_examples,
        prompt_preview=prompt,
        generated_description=generated_description,
        used_model_info=used_model_info,
    )
