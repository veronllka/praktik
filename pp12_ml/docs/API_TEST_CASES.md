# API test cases

Базовый URL при локальном запуске:

```text
http://127.0.0.1:8000
```

Перед ручной проверкой нужно выполнить notebook и убедиться, что в `pp12_ml/artifacts` есть `joblib`-артефакты.

## 1. `/predict-category`: бетонные

Запрос:

```json
{
  "title": "Заливка ростверка"
}
```

Ожидаемый результат:

```json
{
  "title": "Заливка ростверка",
  "predicted_category": "бетонные"
}
```

## 2. `/predict-category`: инженерные

Запрос:

```json
{
  "title": "Прокладка кабеля"
}
```

Ожидаемый результат:

```json
{
  "title": "Прокладка кабеля",
  "predicted_category": "инженерные"
}
```

## 3. `/predict-category`: снабженческие

Запрос:

```json
{
  "title": "Заказ сухих смесей"
}
```

Ожидаемый результат:

```json
{
  "title": "Заказ сухих смесей",
  "predicted_category": "снабженческие"
}
```

## 4. `/generate-description`: инженерные, без LM Studio

Запрос:

```json
{
  "title": "Монтаж водоснабжения",
  "site_name": "ЖК Северный"
}
```

Ожидаемый результат:

```json
{
  "title": "Монтаж водоснабжения",
  "predicted_category": "инженерные",
  "similar_examples": [
    {
      "title": "Монтаж водоснабжения",
      "category": "инженерные"
    }
  ],
  "prompt_preview": "Сгенерируй краткое деловое описание строительной задачи...",
  "generated_description": null,
  "used_model_info": {
    "llm_provider": "LM Studio",
    "llm_configured": false,
    "reason": "LMSTUDIO_BASE_URL or LMSTUDIO_MODEL is not configured"
  }
}
```

Проверить, что `similar_examples` содержит 3 примера, а `prompt_preview` содержит название задачи, категорию, объект и блок похожих примеров.

## 5. `/generate-description`: снабженческие, с existing_description

Запрос:

```json
{
  "title": "Заказ сухих смесей",
  "existing_description": "Нужно уточнить объем поставки."
}
```

Ожидаемый результат:

```json
{
  "title": "Заказ сухих смесей",
  "predicted_category": "снабженческие",
  "similar_examples": [
    {
      "title": "Заказ сухих смесей",
      "category": "снабженческие"
    }
  ],
  "prompt_preview": "Сгенерируй краткое деловое описание строительной задачи...",
  "generated_description": null,
  "used_model_info": {
    "llm_configured": false
  }
}
```

Проверить, что `prompt_preview` содержит строку `Текущее описание для учета контекста`.

## 6. `/generate-description`: отделочные, с LM Studio

Предусловие:

```powershell
$env:LMSTUDIO_BASE_URL = "http://127.0.0.1:1234"
$env:LMSTUDIO_MODEL = "vikhr-qwen-2.5-1.5b-instruct"
```

Запрос:

```json
{
  "title": "Шпаклевка стен",
  "site_name": "Корпус 2"
}
```

Ожидаемый результат:

```json
{
  "title": "Шпаклевка стен",
  "predicted_category": "отделочные",
  "similar_examples": [
    {
      "category": "отделочные"
    }
  ],
  "prompt_preview": "Сгенерируй краткое деловое описание строительной задачи...",
  "generated_description": "Краткое описание от LM Studio",
  "used_model_info": {
    "llm_provider": "LM Studio",
    "llm_configured": true
  }
}
```

Если LM Studio недоступен, допускается `generated_description: null`, но `used_model_info.error` должен содержать ошибку подключения.

## PowerShell-команды для ручной проверки

```powershell
$body = @{ title = "Заливка ростверка" } | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8000/predict-category" -ContentType "application/json; charset=utf-8" -Body $body
```

```powershell
$body = @{
  title = "Монтаж водоснабжения"
  site_name = "ЖК Северный"
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8000/generate-description" -ContentType "application/json; charset=utf-8" -Body $body
```
