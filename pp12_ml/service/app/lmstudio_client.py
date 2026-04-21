from typing import Any

import httpx

from .config import Settings


SYSTEM_PROMPT = (
    "Ты пишешь краткие деловые описания строительных задач. "
    "Отвечай только на русском языке и только итоговым текстом."
)
RESPONSE_PREVIEW_LIMIT = 800


class LmStudioClient:
    def __init__(self, settings: Settings):
        self.settings = settings

    @property
    def is_configured(self) -> bool:
        return bool(self.settings.lmstudio_base_url and self.settings.lmstudio_model)

    async def generate_description(self, prompt: str) -> tuple[str | None, dict[str, Any]]:
        if not self.is_configured:
            return None, {
                "llm_provider": "LM Studio",
                "llm_configured": False,
                "mode": "prompt_only",
                "reason": "LMSTUDIO_BASE_URL or LMSTUDIO_MODEL is not configured",
            }

        base_url = self.settings.lmstudio_base_url.rstrip("/")
        headers = {"Content-Type": "application/json"}
        if self.settings.lmstudio_api_token:
            headers["Authorization"] = f"Bearer {self.settings.lmstudio_api_token}"

        attempts: list[dict[str, Any]] = []
        async with httpx.AsyncClient(
            timeout=self.settings.lmstudio_timeout_seconds,
            trust_env=False,
        ) as client:
            generated, attempt = await self._try_openai_compatible(client, base_url, headers, prompt)
            attempts.append(attempt)
            if generated:
                return generated, _success_info(
                    endpoint=attempt["endpoint"],
                    api_mode=attempt["api_mode"],
                    model=self.settings.lmstudio_model,
                    fallback_used=False,
                    attempts=attempts,
                )

            generated, attempt = await self._try_lmstudio_rest(client, base_url, headers, prompt)
            attempts.append(attempt)
            if generated:
                return generated, _success_info(
                    endpoint=attempt["endpoint"],
                    api_mode=attempt["api_mode"],
                    model=self.settings.lmstudio_model,
                    fallback_used=True,
                    attempts=attempts,
                )

        last_error = _last_error_attempt(attempts)
        return None, {
            "llm_provider": "LM Studio",
            "llm_configured": True,
            "mode": _failure_mode(attempts),
            "endpoint": last_error.get("endpoint") if last_error else None,
            "attempted_endpoints": [attempt["endpoint"] for attempt in attempts],
            "api_mode": last_error.get("api_mode") if last_error else None,
            "model": self.settings.lmstudio_model,
            "fallback_used": len(attempts) > 1,
            "error_type": last_error.get("error_type") if last_error else "unknown_error",
            "error": last_error.get("error") if last_error else "LM Studio request failed",
            "attempts": attempts,
        }

    async def _try_openai_compatible(
        self,
        client: httpx.AsyncClient,
        base_url: str,
        headers: dict[str, str],
        prompt: str,
    ) -> tuple[str | None, dict[str, Any]]:
        endpoint = f"{base_url}/v1/chat/completions"
        payload = {
            "model": self.settings.lmstudio_model,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": prompt},
            ],
            "temperature": 0.3,
            "max_tokens": 240,
            "stream": False,
        }
        return await _post_and_extract(
            client=client,
            endpoint=endpoint,
            headers=headers,
            payload=payload,
            api_mode="openai-compatible",
            extractor=_extract_openai_compatible_text,
        )

    async def _try_lmstudio_rest(
        self,
        client: httpx.AsyncClient,
        base_url: str,
        headers: dict[str, str],
        prompt: str,
    ) -> tuple[str | None, dict[str, Any]]:
        endpoint = f"{base_url}/api/v1/chat"
        payload = {
            "model": self.settings.lmstudio_model,
            "input": prompt,
            "system_prompt": SYSTEM_PROMPT,
            "temperature": 0.3,
            "max_output_tokens": 240,
            "stream": False,
            "store": False,
        }
        return await _post_and_extract(
            client=client,
            endpoint=endpoint,
            headers=headers,
            payload=payload,
            api_mode="lmstudio-rest",
            extractor=_extract_lmstudio_rest_text,
        )


async def _post_and_extract(
    client: httpx.AsyncClient,
    endpoint: str,
    headers: dict[str, str],
    payload: dict[str, Any],
    api_mode: str,
    extractor: Any,
) -> tuple[str | None, dict[str, Any]]:
    attempt: dict[str, Any] = {
        "api_mode": api_mode,
        "endpoint": endpoint,
        "success": False,
    }

    try:
        response = await client.post(endpoint, headers=headers, json=payload)
        attempt["status_code"] = response.status_code
        response.raise_for_status()
    except httpx.HTTPStatusError as exc:
        _add_response_error(attempt, "http_status", exc.response)
        return None, attempt
    except httpx.TimeoutException as exc:
        attempt.update({"error_type": "timeout", "error": str(exc)})
        return None, attempt
    except httpx.RequestError as exc:
        attempt.update({"error_type": "request_error", "error": str(exc)})
        return None, attempt

    try:
        data = response.json()
    except ValueError as exc:
        attempt.update(
            {
                "error_type": "invalid_json",
                "error": str(exc),
                "response_preview": _preview(response.text),
            }
        )
        return None, attempt

    generated = extractor(data)
    if not generated:
        attempt.update(
            {
                "error_type": "empty_response",
                "error": "LM Studio returned an empty generation payload",
                "response_preview": _preview(response.text),
            }
        )
        return None, attempt

    attempt["success"] = True
    return generated, attempt


def _success_info(
    endpoint: str,
    api_mode: str,
    model: str,
    fallback_used: bool,
    attempts: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "llm_provider": "LM Studio",
        "llm_configured": True,
        "mode": "generated",
        "endpoint": endpoint,
        "api_mode": api_mode,
        "model": model,
        "fallback_used": fallback_used,
        "attempted_endpoints": [attempt["endpoint"] for attempt in attempts],
        "attempts": attempts,
    }


def _add_response_error(attempt: dict[str, Any], error_type: str, response: httpx.Response) -> None:
    attempt.update(
        {
            "status_code": response.status_code,
            "error_type": error_type,
            "error": response.reason_phrase or f"HTTP {response.status_code}",
            "response_preview": _preview(response.text),
        }
    )


def _failure_mode(attempts: list[dict[str, Any]]) -> str:
    if any(attempt.get("error_type") == "empty_response" for attempt in attempts):
        return "empty_response"
    return "request_failed"


def _last_error_attempt(attempts: list[dict[str, Any]]) -> dict[str, Any] | None:
    for attempt in reversed(attempts):
        if attempt.get("error_type"):
            return attempt
    return attempts[-1] if attempts else None


def _preview(text: str) -> str:
    normalized = " ".join(text.split())
    return normalized[:RESPONSE_PREVIEW_LIMIT]


def _extract_openai_compatible_text(payload: dict[str, Any]) -> str | None:
    choices = payload.get("choices")
    if not choices:
        return None

    first_choice = choices[0]
    if not isinstance(first_choice, dict):
        return None

    message = first_choice.get("message") or {}
    if isinstance(message, dict):
        content = _normalize_text_content(message.get("content"))
        if content:
            return content

    return _normalize_text_content(first_choice.get("text"))


def _extract_lmstudio_rest_text(payload: dict[str, Any]) -> str | None:
    output = payload.get("output")
    if isinstance(output, list):
        parts = [_normalize_text_content(item) for item in output]
        text = "\n".join(part for part in parts if part)
        return text.strip() if text.strip() else None

    for key in ("output", "content", "message", "text", "response"):
        text = _normalize_text_content(payload.get(key))
        if text:
            return text

    return None


def _normalize_text_content(value: Any) -> str | None:
    if isinstance(value, str):
        return value.strip() if value.strip() else None

    if isinstance(value, dict):
        for key in ("content", "text", "message"):
            text = _normalize_text_content(value.get(key))
            if text:
                return text
        return None

    if isinstance(value, list):
        parts = [_normalize_text_content(item) for item in value]
        text = "\n".join(part for part in parts if part)
        return text.strip() if text.strip() else None

    return None
