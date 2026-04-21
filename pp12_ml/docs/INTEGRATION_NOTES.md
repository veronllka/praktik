# Integration Notes

## Где находится WPF-приложение

Основное WPF-приложение находится в папке `praktik`.

Ключевые признаки:

- `praktik/praktik.csproj` - WPF-проект на .NET Framework 4.7.2.
- `praktik/App.xaml` и `praktik/App.xaml.cs` - точка входа WPF.
- `praktik/Tasks` - окна и UI для задач.
- `praktik/Models` - модели, контекст данных, сервисы и фасад.
- `praktik.Tests` - тестовый проект.

## Где находится текущая логика генерации описания

Текущая генерация описания задачи через LM Studio связана с такими файлами:

- `praktik/App.config`
  - `LmStudioBaseUrl`
  - `LmStudioTaskDescriptionModel`
  - `LmStudioTaskDescriptionEnabled`
  - `LmStudioTaskDescriptionTimeoutSeconds`
  - `LmStudioApiToken`
- `praktik/Tasks/TaskWindow.xaml`
  - кнопка `btnGenerateDescription`
  - подсказка `txtAiHint`
- `praktik/Tasks/TaskWindow.xaml.cs`
  - `ConfigureAiDescriptionUi`
  - `BtnGenerateDescription_Click`
  - `HandleGenerateDescriptionAsync`
  - `BuildTaskDescriptionRequest`
  - `GetTaskDescriptionExamples`
- `praktik/Models/Patterns/WorkPlannerFacade.cs`
  - поле `LmStudioTaskDescriptionService taskDescriptionService`
  - свойство `CanGenerateTaskDescription`
  - метод `GenerateTaskDescriptionAsync`
- `praktik/Models/Patterns/Services/TaskService.cs`
  - `TaskDescriptionGenerationRequest`
  - `TaskDescriptionExample`
  - `TaskDescriptionGenerationResult`
  - `LmStudioTaskDescriptionService`
  - внутренние DTO для LM Studio:
    - `LmStudioChatRequest`
    - `LmStudioLoadModelRequest`
    - `LmStudioModelsResponse`
    - `LmStudioModelInfo`
    - `LmStudioLoadedInstance`
    - `LmStudioChatResponse`
    - `LmStudioChatOutputItem`

## Что уже умеет текущий LM Studio-сервис

Сервис в C#:

- читает настройки из `App.config`;
- проверяет включение функции через `LmStudioTaskDescriptionEnabled`;
- проверяет и загружает модель через `api/v1/models` и `api/v1/models/load`;
- отправляет prompt в `api/v1/chat`;
- строит system prompt и user prompt;
- добавляет похожие примеры из существующих задач;
- нормализует ответ LLM;
- имеет fallback-генерацию описания по профилям строительных работ.

## Точки будущей интеграции PP12 ML

На следующем этапе можно подключить новый сервис одним из вариантов.

Вариант 1 - заменить только prompt provider:

1. WPF вызывает PP12 service `/generate-description`.
2. PP12 service сам классифицирует `title`, подбирает примеры и вызывает LM Studio.
3. WPF получает готовое `generated_description` и вставляет его в `txtDescription`.

Плюсы: меньше логики в WPF, простой HTTP-контракт.

Вариант 2 - использовать только классификацию:

1. WPF вызывает PP12 service `/predict-category`.
2. WPF добавляет категорию к существующему C# prompt.
3. Текущий `LmStudioTaskDescriptionService` продолжает вызывать LM Studio.

Плюсы: меньше изменений вокруг LM Studio endpoint.
Минусы: нужно аккуратно расширять существующий C# prompt builder.

Рекомендуемый путь для Phase 3 - вариант 1, потому что новый Python-сервис уже содержит классификацию, поиск примеров и LM Studio client.

## Предлагаемый контракт для WPF

Запрос:

```json
{
  "title": "Заливка ростверка",
  "site_name": "ЖК Северный",
  "existing_description": null
}
```

Ответ:

```json
{
  "title": "Заливка ростверка",
  "predicted_category": "бетонные",
  "similar_examples": [],
  "prompt_preview": "...",
  "generated_description": "...",
  "used_model_info": {
    "classifier": "LinearSVC",
    "llm_provider": "LM Studio"
  }
}
```

## Ограничения

- Phase 1 не изменяет WPF-проект.
- Интеграцию стоит делать после обучения модели и ручной проверки API.
- Для WPF на .NET Framework 4.7.2 лучше оставить простой JSON-over-HTTP контракт без сложных зависимостей.
