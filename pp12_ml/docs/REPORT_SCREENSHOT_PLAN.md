# Report screenshot plan

Скриншоты для отчета лучше делать после полного запуска notebook, сервиса и Docker-команд.

## Notebook

1. `pp12_task_classifier.ipynb`: первые строки датасета через `head`.
2. Блок первичного анализа: `shape`, `info`, пропуски, дубликаты.
3. Графики:
   - распределение категорий;
   - длина названий задач;
   - длина эталонных описаний.
4. Таблица сравнения моделей с `accuracy`, `precision_macro`, `recall_macro`, `f1_macro`.
5. Финальная оценка лучшей модели на test.
6. Матрица ошибок.
7. Примеры предсказаний.
8. Ячейка сохранения артефактов `best_model.joblib`, `tfidf_vectorizer.joblib`, `label_encoder.joblib`.

## API

1. Swagger UI по адресу `http://127.0.0.1:8000/docs`.
2. `GET /health` с `artifacts_loaded: true`.
3. Успешный `/predict-category` для `Заливка ростверка` с категорией `бетонные`.
4. Успешный `/predict-category` для `Прокладка кабеля` с категорией `инженерные`.
5. Успешный `/generate-description` без LM Studio:
   - `predicted_category`;
   - `similar_examples`;
   - `prompt_preview`;
   - `generated_description: null`;
   - причина в `used_model_info`.
6. Успешный `/generate-description` с LM Studio, если локальная модель запущена:
   - непустой `generated_description`;
   - `llm_configured: true`;
   - имя модели в `used_model_info`.

## Docker

1. Команда сборки:

```powershell
docker build -f service\Dockerfile -t pp12-ml-service .
```

2. Успешное завершение Docker build.
3. Команда запуска:

```powershell
docker run --rm -p 8000:8000 -v "${PWD}\artifacts:/artifacts" pp12-ml-service
```

4. Логи контейнера с запуском Uvicorn.
5. Проверка `GET /health` против контейнера.

## Примечание

В текущей среде Codex Docker CLI не найден, поэтому Docker build/run нужно снять вручную на машине, где установлен Docker Desktop или совместимый Docker Engine.
