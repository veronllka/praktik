# AGENTS для `pp12_ml`

## Назначение папки

`pp12_ml` содержит изолированную реализацию PP12: датасет, notebook обучения классификатора, FastAPI-сервис, prompt builder и документацию для будущей интеграции с WPF-приложением.

## Структура

```text
pp12_ml/
  README_PP12.md
  AGENTS.md
  requirements-dev.txt
  data/
    construction_tasks_dataset.csv
    dataset_schema.md
  docs/
    API_TEST_CASES.md
    PP12_IMPLEMENTATION_PLAN.md
    INTEGRATION_NOTES.md
    REPORT_SCREENSHOT_PLAN.md
  notebooks/
    pp12_task_classifier.ipynb
  scripts/
    run_notebook.py
  service/
    Dockerfile
    README.md
    requirements.txt
    app/
      config.py
      lmstudio_client.py
      main.py
      ml_utils.py
      prompt_builder.py
      schemas.py
```

## Правила изменений

- Не менять существующий WPF-код без необходимости.
- Все новые файлы для PP12 размещать внутри `pp12_ml`, если нет крайней причины выходить за границы папки.
- Изменения должны быть минимальными, понятными и проверяемыми.
- Не перемещать текущие файлы приложения.
- Не выполнять агрессивный рефакторинг C#-сервисов LM Studio на Phase 1.
- Если нужна интеграция с WPF, сначала описать точку подключения в `docs/INTEGRATION_NOTES.md`, затем делать отдельный небольшой шаг.

## Запуск notebook

```bash
cd pp12_ml
python -m venv .venv
.venv\Scripts\python -m pip install -r requirements-dev.txt
.venv\Scripts\python scripts\run_notebook.py --save-executed
```

Notebook должен:

- загрузить `data/construction_tasks_dataset.csv`;
- обучить минимум две модели;
- выбрать лучшую по `f1_macro`;
- сохранить артефакты в `pp12_ml/artifacts`.

## Запуск сервиса

```bash
cd pp12_ml/service
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
```

Docker:

```bash
cd pp12_ml
docker build -f service/Dockerfile -t pp12-ml-service .
docker run --rm -p 8000:8000 -v "${PWD}\artifacts:/artifacts" pp12-ml-service
```

## Проверка

```bash
curl http://127.0.0.1:8000/health
curl -X POST http://127.0.0.1:8000/predict-category -H "Content-Type: application/json" -d "{\"title\":\"Заливка ростверка\"}"
```

Перед интеграцией с WPF нужно отдельно прогнать notebook, убедиться в наличии `joblib`-артефактов и вручную проверить API.
