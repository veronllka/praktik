# PP12 ML Service

FastAPI-сервис для PP12 классифицирует короткое название строительной задачи, подбирает похожие примеры из датасета и формирует улучшенный prompt для LM Studio.

## Endpoint-ы

- `GET /health`
- `POST /predict-category`
- `POST /generate-description`

## Быстрый запуск

Из `cmd.exe`:

```cmd
cd C:\Users\User\source\repos\praktik\pp12_ml\service
run_service.cmd
```

Из PowerShell:

```powershell
cd C:\Users\User\source\repos\praktik\pp12_ml\service
.\run_service.ps1
```

Скрипты используют локальное окружение `pp12_ml\.venv`, если оно уже создано. Если окружение не найдено, будет использован `python` из `PATH`.

LM Studio по умолчанию берется из настроек сервиса:

```text
LMSTUDIO_BASE_URL = http://127.0.0.1:1234
LMSTUDIO_MODEL = vikhr-qwen-2.5-1.5b-instruct
```

## Подготовка окружения

Из корня репозитория:

```powershell
cd C:\Users\User\source\repos\praktik
py -m venv pp12_ml\.venv
pp12_ml\.venv\Scripts\python.exe -m pip install --upgrade pip setuptools wheel
pp12_ml\.venv\Scripts\python.exe -m pip install -r pp12_ml\requirements-dev.txt
```

## Обучение модели и генерация артефактов

```powershell
cd C:\Users\User\source\repos\praktik
pp12_ml\.venv\Scripts\python.exe pp12_ml\scripts\run_notebook.py --save-executed
```

После успешного запуска должны появиться:

```text
pp12_ml/artifacts/best_model.joblib
pp12_ml/artifacts/tfidf_vectorizer.joblib
pp12_ml/artifacts/label_encoder.joblib
```

`--save-executed` дополнительно сохраняет локальную выполненную копию notebook-а в `pp12_ml/notebooks/pp12_task_classifier_executed.ipynb`. Этот файл игнорируется Git и нужен только для просмотра outputs и подготовки скриншотов.

## Локальный запуск сервиса

```powershell
cd C:\Users\User\source\repos\praktik\pp12_ml\service
..\.venv\Scripts\python.exe -m uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
```

Swagger UI будет доступен по адресу:

```text
http://127.0.0.1:8000/docs
```

## Настройки окружения

- `PP12_DATASET_PATH` - путь к CSV-датасету.
- `PP12_ARTIFACTS_DIR` - папка с `joblib`-артефактами.
- `PP12_MAX_SIMILAR_EXAMPLES` - число похожих примеров для prompt.
- `LMSTUDIO_BASE_URL` - базовый URL LM Studio OpenAI-compatible API. По умолчанию `http://127.0.0.1:1234`.
- `LMSTUDIO_MODEL` - имя локальной модели. По умолчанию `vikhr-qwen-2.5-1.5b-instruct`.
- `LMSTUDIO_API_TOKEN` - токен, если endpoint его требует.
- `LMSTUDIO_TIMEOUT_SECONDS` - таймаут запроса к LM Studio.

Клиент LM Studio сначала отправляет запрос в OpenAI-compatible endpoint `/v1/chat/completions`.
Если этот endpoint возвращает ошибку или пустой ответ, сервис автоматически пробует fallback endpoint LM Studio API `/api/v1/chat`.
Фактически использованный endpoint, режим API, статус и тип ошибки по попыткам возвращаются в `used_model_info`.

Если LM Studio недоступен, `/generate-description` вернет `generated_description: null`, но вернет `prompt_preview`, `similar_examples` и причину в `used_model_info`.

Env-переменные больше не нужно задавать вручную для обычного локального запуска. Их можно задать только для переопределения дефолтов в текущей PowerShell-сессии:

```powershell
$env:LMSTUDIO_BASE_URL = "http://127.0.0.1:1234"
$env:LMSTUDIO_MODEL = "vikhr-qwen-2.5-1.5b-instruct"
```

## Примеры запросов PowerShell

Health:

```powershell
Invoke-RestMethod -Method Get -Uri "http://127.0.0.1:8000/health"
```

Predict category:

```powershell
$body = @{ title = "Заливка ростверка" } | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8000/predict-category" -ContentType "application/json; charset=utf-8" -Body $body
```

Generate description:

```powershell
$body = @{
  title = "Монтаж водоснабжения"
  site_name = "ЖК Северный"
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:8000/generate-description" -ContentType "application/json; charset=utf-8" -Body $body
```

## Примеры запросов curl

```bash
curl http://127.0.0.1:8000/health
```

```bash
curl -X POST http://127.0.0.1:8000/predict-category \
  -H "Content-Type: application/json; charset=utf-8" \
  --data-raw '{"title":"Заливка ростверка"}'
```

```bash
curl -X POST http://127.0.0.1:8000/generate-description \
  -H "Content-Type: application/json; charset=utf-8" \
  --data-raw '{"title":"Монтаж водоснабжения","site_name":"ЖК Северный"}'
```

## Docker

Dockerfile рассчитан на build context `pp12_ml`, потому что сервису нужен доступ к `service` и `data`.

```powershell
cd C:\Users\User\source\repos\praktik\pp12_ml
docker build -f service\Dockerfile -t pp12-ml-service .
docker run --rm -p 8000:8000 -v "${PWD}\artifacts:/artifacts" pp12-ml-service
```

Если используется `cmd.exe`, переменная пути отличается:

```cmd
cd C:\Users\User\source\repos\praktik\pp12_ml
docker build -f service\Dockerfile -t pp12-ml-service .
docker run --rm -p 8000:8000 -v "%cd%\artifacts:/artifacts" pp12-ml-service
```

Папка `/artifacts` внутри контейнера должна содержать `joblib`-артефакты после обучения notebook-а.
