using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using praktik.Models;

namespace praktik.Models.Patterns
{
    /// <summary>
    /// Сервис для работы с задачами.
    /// Подсистема, используемая WorkPlannerFacade.
    /// </summary>
    public class TaskService
    {
        private readonly IWorkPlannerContext db;

        public TaskService(IWorkPlannerContext context)
        {
            db = context;
        }

        /// <summary>
        /// Получает задачи с фильтрацией.
        /// </summary>
        public List<Task> GetFilteredTasks(int? siteId = null, int? crewId = null, int? statusId = null)
        {
            var tasks = db.GetTasks();

            if (siteId.HasValue && siteId.Value > 0)
            {
                tasks = tasks.Where(t => t.SiteId == siteId.Value).ToList();
            }

            if (crewId.HasValue && crewId.Value > 0)
            {
                tasks = tasks.Where(t => t.CrewId == crewId.Value).ToList();
            }

            if (statusId.HasValue && statusId.Value > 0)
            {
                tasks = tasks.Where(t => t.TaskStatusId == statusId.Value).ToList();
            }

            return tasks;
        }

        /// <summary>
        /// Создает новую задачу с валидацией.
        /// </summary>
        public bool CreateTask(Task task, int userId, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                errorMessage = "Название задачи не может быть пустым";
                return false;
            }

            if (task.StartDate > task.EndDate)
            {
                errorMessage = "Дата окончания должна быть позже даты начала";
                return false;
            }

            try
            {
                task.CreatedBy = userId;
                task.CreatedAt = DateTime.Now;
                db.AddTask(task);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при создании задачи: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Обновляет статус задачи.
        /// </summary>
        public bool UpdateStatus(int taskId, int newStatusId, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                var task = db.GetTasks().FirstOrDefault(t => t.TaskId == taskId);
                if (task == null)
                {
                    errorMessage = "Задача не найдена";
                    return false;
                }

                task.TaskStatusId = newStatusId;
                task.UpdatedAt = DateTime.Now;
                db.UpdateTask(task);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при обновлении статуса: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Получает задачи, активные на указанную дату.
        /// </summary>
        public bool RecordTaskPrint(int taskId, int userId, string templateName, DateTime printedAt, out string errorMessage)
        {
            errorMessage = null;

            if (userId <= 0)
            {
                errorMessage = "Не удалось определить пользователя печати";
                return false;
            }

            if (string.IsNullOrWhiteSpace(templateName))
            {
                errorMessage = "Не указан тип шаблона печати";
                return false;
            }

            try
            {
                var taskExists = db.GetTasks().Any(t => t.TaskId == taskId);
                if (!taskExists)
                {
                    errorMessage = "Задача не найдена";
                    return false;
                }

                db.RecordTaskPrint(taskId, userId, templateName.Trim(), printedAt);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при сохранении журнала печати: {ex.Message}";
                return false;
            }
        }

        public List<TaskPrintLog> GetTaskPrintLogs(int taskId)
        {
            if (taskId <= 0)
            {
                return new List<TaskPrintLog>();
            }

            return db.GetTaskPrintLogs(taskId);
        }

        public List<Task> GetTasksByDate(DateTime date)
        {
            return db.GetTasks()
                .Where(t => t.StartDate.Date <= date.Date && t.EndDate.Date >= date.Date)
                .ToList();
        }
    }

    public class TaskDescriptionGenerationRequest
    {
        public string Title { get; set; }
        public string SiteName { get; set; }
        public List<TaskDescriptionExample> Examples { get; set; } = new List<TaskDescriptionExample>();
    }

    public class TaskDescriptionExample
    {
        public string Title { get; set; }
        public string Description { get; set; }
    }

    public class TaskDescriptionGenerationResult
    {
        public bool IsSuccess { get; private set; }
        public string Description { get; private set; }
        public string ErrorMessage { get; private set; }

        public static TaskDescriptionGenerationResult Success(string description)
        {
            return new TaskDescriptionGenerationResult
            {
                IsSuccess = true,
                Description = description
            };
        }
        
        public static TaskDescriptionGenerationResult Fail(string errorMessage)
        {
            return new TaskDescriptionGenerationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
    
    public class LmStudioTaskDescriptionService
    {
        private const string ModelsEndpoint = "api/v1/models";
        private const string LoadModelEndpoint = "api/v1/models/load";
        private const string ChatEndpoint = "api/v1/chat";
        private const int DefaultTimeoutSeconds = 45;
        private const int DefaultContextLength = 4096;
        private const int DefaultMaxOutputTokens = 240;
        private const double DefaultTemperature = 0.3;
        private const int MinTitleLength = 4;

        private static readonly Regex DateRegex = new Regex(@"\b\d{1,2}[./-]\d{1,2}([./-]\d{2,4})?\b|\b\d{4}\b|\b(январ|феврал|март|апрел|ма[йя]|июн|июл|август|сентябр|октябр|ноябр|декабр)\w*\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ExtraWhitespaceRegex = new Regex(@"\s+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly TaskDescriptionExample[] BuiltInExamples =
        {
            new TaskDescriptionExample
            {
                Title = "Заливка фундамента",
                Description = "Необходимо выполнить работы по заливке фундамента с подготовкой основания, установкой опалубки и армированием конструкции. После подачи бетонной смеси требуется выполнить выравнивание поверхности и проверить качество бетонирования перед переходом к следующему этапу."
            },
            new TaskDescriptionExample
            {
                Title = "Монтаж кровли",
                Description = "Необходимо выполнить монтаж кровли с подготовкой основания, устройством покрытия и проработкой примыканий. По завершении требуется проверить герметичность узлов, качество креплений и готовность покрытия к эксплуатации."
            },
            new TaskDescriptionExample
            {
                Title = "Кладка стен",
                Description = "Необходимо выполнить кладку стен с подготовкой рабочей зоны, нанесением раствора и последовательной укладкой материалов по разметке. В процессе работ следует контролировать перевязку, геометрию рядов и качество швов."
            },
            new TaskDescriptionExample
            {
                Title = "Монтаж опалубки",
                Description = "Необходимо выполнить монтаж опалубки с подготовкой элементов системы, сборкой щитов и выставлением конструкции по размерам. После закрепления требуется проверить геометрию, устойчивость и готовность опалубки к бетонированию."
            },
            new TaskDescriptionExample
            {
                Title = "Прокладка электропроводки",
                Description = "Необходимо выполнить прокладку электропроводки с разметкой трасс, укладкой кабельных линий и монтажом распределительных точек. По завершении требуется проверить правильность подключений, надежность крепления и готовность системы к дальнейшему монтажу оборудования."
            },
            new TaskDescriptionExample
            {
                Title = "Монтаж водоснабжения",
                Description = "Необходимо выполнить монтаж системы водоснабжения с подготовкой трасс, прокладкой трубопроводов и сборкой соединительных узлов. После монтажа следует проверить герметичность системы и соответствие выполненных работ проектным решениям."
            },
            new TaskDescriptionExample
            {
                Title = "Устройство стяжки пола",
                Description = "Необходимо выполнить устройство стяжки пола с подготовкой основания, установкой маяков и распределением раствора по заданному уровню. После выравнивания поверхности требуется проверить плоскость и качество выполненного слоя."
            },
            new TaskDescriptionExample
            {
                Title = "Гидроизоляция подвала",
                Description = "Необходимо выполнить гидроизоляционные работы с подготовкой поверхности, обработкой стыков и нанесением защитного слоя. По завершении требуется проверить сплошность покрытия и готовность конструкции к дальнейшим работам."
            }
        };

        private static readonly string[] GenericSentenceMarkers =
        {
            "в соответствии с проектом",
            "в соответствии с технологией",
            "технологией производства работ",
            "строительными нормами",
            "контроль качества",
            "требования техники безопасности",
            "требования безопасности"
        };

        private static readonly string[] WeakTitleTokens =
        {
            "работ",
            "задач",
            "монтаж",
            "устройств",
            "выполн",
            "этап",
            "чернов",
            "чистов",
            "финиш",
            "локальн",
            "капитальн",
            "общ"
        };

        private static readonly string[] OperationVerbMarkers =
        {
            "подготов",
            "смонт",
            "улож",
            "выровн",
            "выстав",
            "нанес",
            "собра",
            "закреп",
            "гермет",
            "подключ",
            "провер",
            "очист",
            "демонт",
            "замен",
            "восстанов",
            "заполн",
            "обработ",
            "вяз",
            "бетонир"
        };

        private static readonly ConstructionTaskProfile[] TaskProfiles =
        {
            new ConstructionTaskProfile
            {
                Name = "Фундамент",
                ObjectPhrase = "фундамента",
                Keywords = new[] { "фундамент", "бетонир", "заливк", "ростверк", "плита" },
                WorkPhrase = "монолитные работы по устройству фундамента",
                StagePhrases = new[]
                {
                    "подготовить основание и разбивку",
                    "смонтировать опалубку",
                    "выполнить армирование",
                    "уложить бетонную смесь",
                    "выровнять поверхность и проверить геометрию"
                },
                ResultPhrase = "подтвердить качество бетонирования и подготовить конструкцию к следующему этапу"
            },
            new ConstructionTaskProfile
            {
                Name = "Кровля",
                ObjectPhrase = "кровли",
                Keywords = new[] { "кровл", "крыша", "стропил", "водосток", "мембран" },
                WorkPhrase = "работы по устройству кровли",
                StagePhrases = new[]
                {
                    "подготовить основание и места примыкания",
                    "смонтировать несущие и доборные элементы",
                    "устроить кровельное покрытие",
                    "герметизировать узлы и стыки",
                    "проверить надежность креплений и водоотведение"
                },
                ResultPhrase = "обеспечить герметичность покрытия и готовность кровли к эксплуатации"
            },
            new ConstructionTaskProfile
            {
                Name = "Кладка",
                ObjectPhrase = "стен",
                Keywords = new[] { "кладк", "стен", "перегород", "блок", "кирпич" },
                WorkPhrase = "кладочные работы",
                StagePhrases = new[]
                {
                    "подготовить рабочую зону и раствор",
                    "выполнить укладку материала по разметке",
                    "выдержать перевязку и размеры рядов",
                    "контролировать вертикальность и уровень",
                    "проверить качество швов и примыканий"
                },
                ResultPhrase = "обеспечить правильную геометрию кладки и готовность конструкции к дальнейшим работам"
            },
            new ConstructionTaskProfile
            {
                Name = "Опалубка",
                ObjectPhrase = "опалубки",
                Keywords = new[] { "опалуб" },
                WorkPhrase = "монтаж опалубки",
                StagePhrases = new[]
                {
                    "подготовить элементы системы",
                    "собрать и выставить опалубку по размерам",
                    "закрепить конструкцию и усилить узлы",
                    "проверить геометрию и устойчивость",
                    "подготовить систему к бетонированию"
                },
                ResultPhrase = "обеспечить точную сборку и готовность опалубки к следующему этапу"
            },
            new ConstructionTaskProfile
            {
                Name = "Армирование",
                ObjectPhrase = "арматурного каркаса",
                Keywords = new[] { "армир", "арматур" },
                WorkPhrase = "работы по армированию конструкции",
                StagePhrases = new[]
                {
                    "подготовить арматурные элементы",
                    "выполнить раскладку и вязку каркаса",
                    "выдержать проектные размеры и защитный слой",
                    "закрепить закладные и соединения",
                    "проверить схему армирования перед бетонированием"
                },
                ResultPhrase = "подтвердить правильность армирования и готовность конструкции к бетонированию"
            },
            new ConstructionTaskProfile
            {
                Name = "Инженерные сети",
                ObjectPhrase = "инженерных сетей",
                Keywords = new[] { "водоснабж", "водопровод", "канализац", "отоплен", "сантех", "труб" },
                WorkPhrase = "монтаж инженерных сетей",
                StagePhrases = new[]
                {
                    "подготовить трассы и места проходов",
                    "смонтировать трубопроводы и фитинги",
                    "собрать узлы подключения",
                    "закрепить линии и проверить уклоны",
                    "выполнить проверку герметичности системы"
                },
                ResultPhrase = "обеспечить работоспособность сети и готовность к пусконаладке"
            },
            new ConstructionTaskProfile
            {
                Name = "Электромонтаж",
                ObjectPhrase = "электропроводки",
                Keywords = new[] { "электр", "кабел", "провод", "освещен", "щит" },
                WorkPhrase = "электромонтажные работы",
                StagePhrases = new[]
                {
                    "разметить трассы и точки подключения",
                    "проложить кабельные линии",
                    "смонтировать распределительные и монтажные элементы",
                    "выполнить подключение цепей",
                    "проверить целостность линий и корректность схемы"
                },
                ResultPhrase = "подготовить систему к дальнейшему подключению и проверке"
            },
            new ConstructionTaskProfile
            {
                Name = "Отделка",
                ObjectPhrase = "поверхностей",
                Keywords = new[] { "стяжк", "штукатур", "шпаклев", "окраск", "плитк", "отделк" },
                WorkPhrase = "отделочные работы",
                StagePhrases = new[]
                {
                    "подготовить основание и рабочую поверхность",
                    "нанести или уложить основной материал",
                    "выровнять слой по заданным отметкам",
                    "обработать стыки и примыкания",
                    "проверить качество поверхности и готовность к следующему этапу"
                },
                ResultPhrase = "обеспечить ровную и подготовленную поверхность без дефектов"
            },
            new ConstructionTaskProfile
            {
                Name = "Гидроизоляция",
                ObjectPhrase = "конструкции",
                Keywords = new[] { "гидроизоляц", "утеплен", "изоляц", "мембран", "мастик" },
                WorkPhrase = "изоляционные работы",
                StagePhrases = new[]
                {
                    "подготовить и очистить основание",
                    "обработать швы, стыки и примыкания",
                    "нанести основной изоляционный слой",
                    "усилить проблемные участки",
                    "проверить сплошность и качество покрытия"
                },
                ResultPhrase = "обеспечить защиту конструкции и готовность поверхности к дальнейшим работам"
            }
        };

        private readonly string baseUrl;
        private readonly string modelKey;
        private readonly string apiToken;
        private readonly int timeoutSeconds;

        public LmStudioTaskDescriptionService()
        {
            baseUrl = ReadSetting("LmStudioBaseUrl", "http://127.0.0.1:1234");
            modelKey = ReadSetting("LmStudioTaskDescriptionModel", "vikhr-qwen-2.5-1.5b-instruct");
            apiToken = ReadSetting("LmStudioApiToken", string.Empty);
            timeoutSeconds = ParseInt(ReadSetting("LmStudioTaskDescriptionTimeoutSeconds", DefaultTimeoutSeconds.ToString()), DefaultTimeoutSeconds);
            IsEnabled = ParseBool(ReadSetting("LmStudioTaskDescriptionEnabled", "true"), true)
                && !string.IsNullOrWhiteSpace(baseUrl)
                && !string.IsNullOrWhiteSpace(modelKey);
        }

        public bool IsEnabled { get; }

        public async System.Threading.Tasks.Task<TaskDescriptionGenerationResult> GenerateDescriptionAsync(TaskDescriptionGenerationRequest request)
        {
            if (!IsEnabled)
            {
                return TaskDescriptionGenerationResult.Fail("Генерация описания через LM Studio отключена в настройках приложения.");
            }

            var normalizedRequest = NormalizeRequest(request);
            if (normalizedRequest == null)
            {
                return TaskDescriptionGenerationResult.Fail("Не передано название задачи для генерации описания.");
            }

            if (normalizedRequest.Title.Length < MinTitleLength || !normalizedRequest.Title.Any(char.IsLetter))
            {
                return TaskDescriptionGenerationResult.Fail("Для генерации нужно более понятное название задачи: минимум 4 символа и хотя бы одно слово по сути работ.");
            }

            try
            {
                using (var httpClient = CreateClient())
                {
                    await EnsureModelLoadedAsync(httpClient).ConfigureAwait(false);

                    var payload = new LmStudioChatRequest
                    {
                        model = modelKey,
                        input = BuildUserPrompt(normalizedRequest),
                        system_prompt = BuildSystemPrompt(),
                        temperature = DefaultTemperature,
                        max_output_tokens = DefaultMaxOutputTokens,
                        context_length = DefaultContextLength,
                        stream = false
                    };

                    var responseJson = await PostJsonAsync(httpClient, ChatEndpoint, payload).ConfigureAwait(false);
                    var response = DeserializeJson<LmStudioChatResponse>(responseJson);
                    var generatedText = NormalizeGeneratedText(ExtractGeneratedText(response), normalizedRequest);

                    if (string.IsNullOrWhiteSpace(generatedText))
                    {
                        generatedText = BuildFallbackDescription(normalizedRequest);
                    }

                    return TaskDescriptionGenerationResult.Success(generatedText);
                }
            }
            catch (HttpRequestException ex)
            {
                return TaskDescriptionGenerationResult.Fail($"Не удалось подключиться к LM Studio по адресу {baseUrl}. Проверьте, что локальный сервер запущен. {ex.Message}");
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                return TaskDescriptionGenerationResult.Fail("LM Studio не ответил вовремя. При необходимости увеличьте таймаут в App.config.");
            }
            catch (Exception ex)
            {
                return TaskDescriptionGenerationResult.Fail($"Не удалось сгенерировать описание задачи: {ex.Message}");
            }
        }

        private TaskDescriptionGenerationRequest NormalizeRequest(TaskDescriptionGenerationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Title))
            {
                return null;
            }

            return new TaskDescriptionGenerationRequest
            {
                Title = request.Title.Trim(),
                SiteName = TrimOrNull(request.SiteName),
                Examples = (request.Examples ?? new List<TaskDescriptionExample>())
                    .Where(example => example != null &&
                                      !string.IsNullOrWhiteSpace(example.Title) &&
                                      !string.IsNullOrWhiteSpace(example.Description))
                    .Take(3)
                    .Select(example => new TaskDescriptionExample
                    {
                        Title = example.Title.Trim(),
                        Description = example.Description.Trim()
                    })
                    .ToList()
            };
        }

        private HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            }

            return client;
        }

        private async System.Threading.Tasks.Task EnsureModelLoadedAsync(HttpClient httpClient)
        {
            var modelsJson = await httpClient.GetStringAsync(ModelsEndpoint).ConfigureAwait(false);
            var modelsResponse = DeserializeJson<LmStudioModelsResponse>(modelsJson);
            var matchingModel = modelsResponse?.models?.FirstOrDefault(IsConfiguredModelMatch);

            if (matchingModel == null)
            {
                throw new InvalidOperationException($"Модель '{modelKey}' не найдена в LM Studio.");
            }

            if (matchingModel.loaded_instances != null && matchingModel.loaded_instances.Count > 0)
            {
                return;
            }

            await PostJsonAsync(httpClient, LoadModelEndpoint, new LmStudioLoadModelRequest
            {
                model = modelKey,
                context_length = DefaultContextLength
            }).ConfigureAwait(false);
        }

        private bool IsConfiguredModelMatch(LmStudioModelInfo model)
        {
            if (model == null)
            {
                return false;
            }

            if (string.Equals(model.key, modelKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return model.loaded_instances != null &&
                   model.loaded_instances.Any(instance => string.Equals(instance.id, modelKey, StringComparison.OrdinalIgnoreCase));
        }

        private async System.Threading.Tasks.Task<string> PostJsonAsync<T>(HttpClient httpClient, string relativeUrl, T payload)
        {
            using (var content = new StringContent(CreateJsonSerializer().Serialize(payload), Encoding.UTF8, "application/json"))
            using (var response = await httpClient.PostAsync(relativeUrl, content).ConfigureAwait(false))
            {
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"LM Studio вернул ошибку {(int)response.StatusCode}: {responseBody}");
                }

                return responseBody;
            }
        }

        private T DeserializeJson<T>(string json)
        {
            return CreateJsonSerializer().Deserialize<T>(json ?? string.Empty);
        }

        private JavaScriptSerializer CreateJsonSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        private string BuildSystemPrompt()
        {
            return "You write compact task descriptions for a construction planning application. Always answer in Russian. Make the text depend on the exact task title, the work object and the type of construction operation. Prefer concrete construction actions over generic phrases. Use only plain text without headings, quotes or markdown. Mention the site only if it is explicitly provided. Never mention crews, priorities, dates, deadlines, budgets or quantities.";
        }

        private string BuildUserPrompt(TaskDescriptionGenerationRequest request)
        {
            var builder = new StringBuilder();
            var examples = GetPromptExamples(request);
            var profile = SelectBestProfile(request.Title);
            var specificWorkPhrase = BuildSpecificWorkPhrase(request.Title, profile);
            var operationHints = BuildOperationHints(request.Title, profile).Take(4).ToList();
            var resultHint = BuildResultHint(request.Title, profile);
            var focusTokens = GetMeaningfulTitleTokens(request.Title);

            builder.AppendLine("Generate a Russian description for a construction task card.");
            builder.AppendLine($"Task title: {request.Title}");

            if (!string.IsNullOrWhiteSpace(request.SiteName))
            {
                builder.AppendLine($"Construction site: {request.SiteName}");
            }

            if (profile != null)
            {
                builder.AppendLine($"Detected work type: {profile.Name}");
                builder.AppendLine($"Core work phrase: {specificWorkPhrase}.");
                builder.AppendLine($"Typical operations: {JoinFragments(operationHints)}.");
                builder.AppendLine($"Expected result: {resultHint}.");
            }

            if (focusTokens.Count > 0)
            {
                builder.AppendLine($"Important title words: {string.Join(", ", focusTokens)}.");
            }

            builder.AppendLine("Requirements:");
            builder.AppendLine("- 2 compact sentences.");
            builder.AppendLine("- 180 to 320 characters if possible.");
            builder.AppendLine("- Use the task title as the main source of meaning, not generic construction wording.");
            builder.AppendLine("- Mention 3 or 4 concrete operations or stages suitable for this exact type of work.");
            builder.AppendLine("- Reflect title modifiers like черновая, чистовая, ремонт, демонтаж or замена if they are present.");
            builder.AppendLine("- Show the practical result of the task in a short form.");
            builder.AppendLine("- Avoid repeating stock phrases about standards, technology, quality control or safety.");
            builder.AppendLine("- Do not mention crews, priorities, dates or deadlines.");
            builder.AppendLine("- Do not start with 'Описание задачи'.");

            foreach (var example in examples)
            {
                builder.AppendLine($"Example title: {example.Title}");
                builder.AppendLine($"Example description: {example.Description}");
            }

            builder.Append("Return only the final Russian description.");
            return builder.ToString();
        }

        private List<TaskDescriptionExample> GetPromptExamples(TaskDescriptionGenerationRequest request)
        {
            var profile = SelectBestProfile(request.Title);
            var requestExamples = (request.Examples ?? new List<TaskDescriptionExample>())
                .Where(example => example != null && !string.IsNullOrWhiteSpace(example.Title) && !string.IsNullOrWhiteSpace(example.Description))
                .Select(example => new
                {
                    Example = example,
                    Score = CalculateTitleSimilarity(request.Title, example.Title) + GetProfileExampleBonus(profile, example.Title)
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Example.Description.Length)
                .Take(2)
                .Select(item => item.Example);

            var builtInExamples = BuiltInExamples
                .Where(example => !string.IsNullOrWhiteSpace(example.Title) && !string.IsNullOrWhiteSpace(example.Description))
                .Select(example => new
                {
                    Example = example,
                    Score = CalculateTitleSimilarity(request.Title, example.Title) + GetProfileExampleBonus(profile, example.Title)
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Example.Description.Length)
                .Take(2)
                .Select(item => item.Example);

            return requestExamples
                .Concat(builtInExamples)
                .GroupBy(example => example.Title.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(3)
                .ToList();
        }

        private int CalculateTitleSimilarity(string firstTitle, string secondTitle)
        {
            var firstTokens = ExtractTitleTokens(firstTitle);
            var secondTokens = ExtractTitleTokens(secondTitle);
            return firstTokens.Intersect(secondTokens).Count() * 4;
        }

        private HashSet<string> ExtractTitleTokens(string value)
        {
            return new HashSet<string>((value ?? string.Empty)
                .Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')', '"' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => NormalizeTitleToken(token))
                .Where(token => token.Length >= 3));
        }

        private string NormalizeTitleToken(string token)
        {
            var normalized = (token ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length < 3)
            {
                return string.Empty;
            }

            var suffixes = new[]
            {
                "иями", "ями", "ами", "ого", "ему", "ому", "ыми", "ими",
                "ение", "ений", "ание", "аний", "ость", "ости", "овая", "овый",
                "ция", "ции", "ах", "ях", "ов", "ев", "ом", "ем", "ой", "ей",
                "ый", "ий", "ая", "яя", "ое", "ее", "ам", "ям", "ия", "ие",
                "а", "я", "ы", "и", "о", "е", "ь"
            };

            foreach (var suffix in suffixes.OrderByDescending(item => item.Length))
            {
                if (normalized.Length - suffix.Length < 3)
                {
                    continue;
                }

                if (normalized.EndsWith(suffix, StringComparison.Ordinal))
                {
                    normalized = normalized.Substring(0, normalized.Length - suffix.Length);
                    break;
                }
            }

            return normalized;
        }

        private string ExtractGeneratedText(LmStudioChatResponse response)
        {
            if (response?.output == null || response.output.Count == 0)
            {
                return null;
            }

            return string.Join(" ", response.output
                .Where(item => string.Equals(item.type, "message", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.type))
                .Select(item => item.content?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        private string NormalizeGeneratedText(string text, TaskDescriptionGenerationRequest request)
        {
            var cleaned = CollapseWhitespace(text);
            cleaned = RemoveMarkdownArtifacts(cleaned);
            cleaned = InsertMissingSentenceBreak(cleaned);
            cleaned = RemoveLeadingLabel(cleaned, "Описание задачи:");
            cleaned = RemoveLeadingLabel(cleaned, "Описание:");
            cleaned = RemoveLeadingLabel(cleaned, "Task description:");
            var profile = SelectBestProfile(request.Title);

            var sentences = SplitIntoSentences(cleaned)
                .Where(sentence => SentenceIsAllowed(sentence, request))
                .ToList();

            if (sentences.Count > 0 && IsHeadingLikeSentence(sentences[0]))
            {
                return string.Empty;
            }

            if (sentences.Count >= 2)
            {
                var specificSentences = sentences
                    .Where(sentence => !IsOverlyGenericSentence(sentence, profile))
                    .ToList();

                if (specificSentences.Count > 0)
                {
                    sentences = specificSentences;
                }
            }

            sentences = sentences.Take(2).ToList();
            cleaned = string.Join(" ", sentences).Trim();
            if (cleaned.Length < 80)
            {
                return string.Empty;
            }

            if (CountOperationMarkers(cleaned) < 2)
            {
                return string.Empty;
            }

            if (CountGenericMarkers(cleaned) > 2 && CountOperationMarkers(cleaned) < 3)
            {
                return string.Empty;
            }

            if (!ContainsTitleSignals(cleaned, request.Title, profile))
            {
                return string.Empty;
            }

            return cleaned;
        }

        private bool SentenceIsAllowed(string sentence, TaskDescriptionGenerationRequest request)
        {
            if (string.IsNullOrWhiteSpace(sentence))
            {
                return false;
            }

            if (DateRegex.IsMatch(sentence))
            {
                return false;
            }

            var lowered = sentence.ToLowerInvariant();
            if (lowered.Contains("бригада") || lowered.Contains("приоритет") || lowered.Contains("срок") || lowered.Contains("дедлайн"))
            {
                return false;
            }

            if (CountGenericMarkers(sentence) >= 2 && CountOperationMarkers(sentence) < 2)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.SiteName) &&
                (sentence.Contains("\"") || sentence.Contains("«") || sentence.Contains("»")))
            {
                return false;
            }

            return true;
        }

        private ConstructionTaskProfile SelectBestProfile(string title)
        {
            return TaskProfiles
                .Select(profile => new
                {
                    Profile = profile,
                    Score = CalculateProfileScore(title, profile)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Profile.StagePhrases.Length)
                .Select(item => item.Profile)
                .FirstOrDefault();
        }

        private int CalculateProfileScore(string title, ConstructionTaskProfile profile)
        {
            var normalizedTitle = (title ?? string.Empty).ToLowerInvariant();
            var tokens = ExtractTitleTokens(title);

            return profile.Keywords
                .Where(keyword =>
                    normalizedTitle.Contains(keyword) ||
                    tokens.Any(token => token.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ||
                                        keyword.StartsWith(token, StringComparison.OrdinalIgnoreCase)))
                .Sum(keyword =>
                {
                    var hasTokenMatch = tokens.Any(token =>
                        token.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ||
                        keyword.StartsWith(token, StringComparison.OrdinalIgnoreCase));

                    return keyword.Length + (hasTokenMatch ? 6 : 3);
                });
        }

        private int GetProfileExampleBonus(ConstructionTaskProfile targetProfile, string exampleTitle)
        {
            var exampleProfile = SelectBestProfile(exampleTitle);
            if (targetProfile == null || exampleProfile == null)
            {
                return 0;
            }

            return string.Equals(targetProfile.Name, exampleProfile.Name, StringComparison.OrdinalIgnoreCase) ? 12 : 0;
        }

        private string JoinFragments(IList<string> fragments)
        {
            if (fragments == null || fragments.Count == 0)
            {
                return string.Empty;
            }

            if (fragments.Count == 1)
            {
                return fragments[0];
            }

            if (fragments.Count == 2)
            {
                return fragments[0] + " и " + fragments[1];
            }

            return string.Join(", ", fragments.Take(fragments.Count - 1)) + " и " + fragments.Last();
        }

        private bool IsOverlyGenericSentence(string sentence, ConstructionTaskProfile profile)
        {
            var lowered = (sentence ?? string.Empty).ToLowerInvariant();
            var hasGenericMarker = GenericSentenceMarkers.Any(marker => lowered.Contains(marker));
            if (!hasGenericMarker)
            {
                return false;
            }

            return CountOperationMarkers(sentence) < 2 && !ContainsProfileSignals(sentence, profile);
        }

        private bool ContainsProfileSignals(string text, ConstructionTaskProfile profile)
        {
            if (string.IsNullOrWhiteSpace(text) || profile == null)
            {
                return false;
            }

            var lowered = text.ToLowerInvariant();
            if (profile.Keywords.Any(keyword => lowered.Contains(keyword)))
            {
                return true;
            }

            return profile.StagePhrases
                .Select(stage => stage.Split(' ').FirstOrDefault())
                .Where(stageMarker => !string.IsNullOrWhiteSpace(stageMarker))
                .Any(stageMarker => lowered.Contains(stageMarker.ToLowerInvariant()));
        }

        private bool ContainsTitleSignals(string text, string title, ConstructionTaskProfile profile)
        {
            var focusTokens = GetMeaningfulTitleTokens(title);
            if (focusTokens.Count == 0)
            {
                return ContainsProfileSignals(text, profile);
            }

            var lowered = (text ?? string.Empty).ToLowerInvariant();
            return focusTokens.Any(token => lowered.Contains(token)) || ContainsProfileSignals(text, profile);
        }

        private List<string> GetMeaningfulTitleTokens(string title)
        {
            return ExtractTitleTokens(title)
                .Where(token => token.Length >= 4)
                .Where(token => !WeakTitleTokens.Any(weakToken => token.StartsWith(weakToken, StringComparison.OrdinalIgnoreCase)))
                .Take(4)
                .ToList();
        }

        private int CountGenericMarkers(string text)
        {
            var lowered = (text ?? string.Empty).ToLowerInvariant();
            return GenericSentenceMarkers.Count(marker => lowered.Contains(marker));
        }

        private int CountOperationMarkers(string text)
        {
            var lowered = (text ?? string.Empty).ToLowerInvariant();
            return OperationVerbMarkers.Count(marker => lowered.Contains(marker));
        }

        private string RemoveMarkdownArtifacts(string text)
        {
            var cleaned = (text ?? string.Empty)
                .Replace("**", string.Empty)
                .Replace("__", string.Empty)
                .Replace("•", " ");

            cleaned = Regex.Replace(cleaned, @"(^|[.!?]\s*)[-•]\s*", "$1", RegexOptions.CultureInvariant);
            return CollapseWhitespace(cleaned);
        }

        private string InsertMissingSentenceBreak(string text)
        {
            return Regex.Replace(
                text ?? string.Empty,
                @"^([А-ЯЁA-Z][^.!?]{0,80}?)(\s+)(Необходимо|Требуется|Следует)\b",
                "$1. $3",
                RegexOptions.CultureInvariant);
        }

        private bool IsHeadingLikeSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
            {
                return false;
            }

            var lowered = sentence.ToLowerInvariant();
            var hasNarrativeVerb = lowered.Contains("необходимо") ||
                                   lowered.Contains("требуется") ||
                                   lowered.Contains("следует") ||
                                   lowered.Contains("выполнить") ||
                                   lowered.Contains("предусматривают");

            return !hasNarrativeVerb && CountOperationMarkers(sentence) == 0 && sentence.Length < 50;
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var builder = new StringBuilder();

            foreach (var character in text ?? string.Empty)
            {
                builder.Append(character);
                if (character == '.' || character == '!' || character == '?')
                {
                    var sentence = builder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }

                    builder.Clear();
                }
            }

            if (builder.Length > 0)
            {
                var sentence = builder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence.EndsWith(".") ? sentence : sentence + ".");
                }
            }

            return sentences;
        }

        private string BuildSpecificWorkPhrase(string title, ConstructionTaskProfile profile)
        {
            var loweredTitle = (title ?? string.Empty).ToLowerInvariant();
            var objectPhrase = ResolveObjectPhrase(loweredTitle, profile);
            string workPhrase;

            if (ContainsAny(loweredTitle, "демонтаж"))
            {
                workPhrase = "демонтаж " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "ремонт"))
            {
                workPhrase = "ремонт " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "замен"))
            {
                workPhrase = "замену " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "заливк"))
            {
                workPhrase = "заливку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "бетонир"))
            {
                workPhrase = "бетонирование " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "прокладк"))
            {
                workPhrase = "прокладку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "укладк"))
            {
                workPhrase = "укладку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "кладк"))
            {
                workPhrase = "кладку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "штукатур"))
            {
                workPhrase = "штукатурку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "шпаклев"))
            {
                workPhrase = "шпаклевку " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "окраск"))
            {
                workPhrase = "окраску " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "стяжк"))
            {
                workPhrase = "устройство стяжки пола";
            }
            else if (ContainsAny(loweredTitle, "гидроизоляц"))
            {
                workPhrase = "гидроизоляцию " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "утеплен"))
            {
                workPhrase = "утепление " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "армир"))
            {
                workPhrase = "армирование " + objectPhrase;
            }
            else if (ContainsAny(loweredTitle, "монтаж", "установк"))
            {
                workPhrase = "монтаж " + objectPhrase;
            }
            else
            {
                workPhrase = profile != null
                    ? profile.WorkPhrase
                    : $"работы по задаче «{title}»";
            }

            return ApplyModeToWorkPhrase(workPhrase, loweredTitle);
        }

        private List<string> BuildOperationHints(string title, ConstructionTaskProfile profile)
        {
            var loweredTitle = (title ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(loweredTitle, "демонтаж"))
            {
                return BuildDemolitionStages(profile, loweredTitle);
            }

            if (ContainsAny(loweredTitle, "ремонт"))
            {
                return BuildRepairStages(profile, loweredTitle);
            }

            if (ContainsAny(loweredTitle, "замен"))
            {
                return BuildReplacementStages(profile, loweredTitle);
            }

            if (ContainsAny(loweredTitle, "заливк", "бетонир"))
            {
                return new List<string>
                {
                    "подготовить основание и разбивку",
                    "смонтировать опалубку",
                    "выполнить армирование",
                    "уложить бетонную смесь с выравниванием поверхности"
                };
            }

            if (ContainsAny(loweredTitle, "штукатур"))
            {
                return new List<string>
                {
                    "подготовить основание и выставить маяки",
                    "нанести штукатурный раствор",
                    "выровнять слой по плоскости",
                    "обработать углы и примыкания"
                };
            }

            if (ContainsAny(loweredTitle, "шпаклев"))
            {
                return new List<string>
                {
                    "подготовить поверхность и швы",
                    "нанести шпаклевочный состав",
                    "выровнять плоскость под отделку",
                    "зачистить участки после высыхания"
                };
            }

            if (ContainsAny(loweredTitle, "окраск"))
            {
                return new List<string>
                {
                    "подготовить поверхность под окраску",
                    "нанести грунтовочный слой",
                    "выполнить окраску по участкам",
                    "проверить равномерность покрытия"
                };
            }

            if (ContainsAny(loweredTitle, "стяжк"))
            {
                return new List<string>
                {
                    "подготовить основание и отметки",
                    "выставить маяки",
                    "распределить раствор по площади",
                    "выровнять поверхность по уровню"
                };
            }

            if (ContainsAny(loweredTitle, "плитк"))
            {
                return new List<string>
                {
                    "подготовить основание и разметку",
                    "нанести клеевой состав",
                    "выполнить укладку плитки",
                    "затереть швы и проверить плоскость"
                };
            }

            if (ContainsAny(loweredTitle, "водоснабж"))
            {
                return new List<string>
                {
                    "подготовить трассы и точки прохода",
                    "смонтировать трубопроводы и фитинги",
                    "собрать узлы подключения",
                    "выполнить проверку герметичности линии"
                };
            }

            if (ContainsAny(loweredTitle, "канализац"))
            {
                return new List<string>
                {
                    "подготовить трассы и места выпуска",
                    "смонтировать трубы и фасонные элементы",
                    "выдержать уклоны на участках",
                    "проверить герметичность соединений"
                };
            }

            if (ContainsAny(loweredTitle, "электропровод", "кабел"))
            {
                return new List<string>
                {
                    "разметить трассы и точки подключения",
                    "проложить кабельные линии",
                    "смонтировать коробки и распределительные элементы",
                    "проверить схему и целостность цепей"
                };
            }

            return profile?.StagePhrases?.Take(4).ToList()
                ?? new List<string>
                {
                    "подготовить участок",
                    "выполнить основной этап работ",
                    "собрать или закрепить необходимые элементы",
                    "проверить результат выполнения"
                };
        }

        private string BuildResultHint(string title, ConstructionTaskProfile profile)
        {
            var loweredTitle = (title ?? string.Empty).ToLowerInvariant();

            if (ContainsAny(loweredTitle, "демонтаж"))
            {
                return "подготовить участок к последующему монтажу или восстановлению";
            }

            if (ContainsAny(loweredTitle, "ремонт"))
            {
                return "восстановить исправное состояние участка и готовность к дальнейшей эксплуатации";
            }

            if (ContainsAny(loweredTitle, "замен"))
            {
                return "обеспечить корректную работу новых элементов и готовность участка к эксплуатации";
            }

            if (ContainsAny(loweredTitle, "чернов"))
            {
                return "подготовить основание к следующему строительному этапу";
            }

            if (ContainsAny(loweredTitle, "чистов", "финиш"))
            {
                return "получить готовую поверхность или систему без существенных доработок";
            }

            return profile?.ResultPhrase ?? "зафиксировать готовность результата к следующему этапу работ";
        }

        private List<string> BuildRepairStages(ConstructionTaskProfile profile, string loweredTitle)
        {
            if (profile == null)
            {
                return new List<string>
                {
                    "осмотреть проблемные участки",
                    "демонтировать поврежденные элементы",
                    "восстановить основание или крепления",
                    "проверить результат ремонта"
                };
            }

            switch (profile.Name)
            {
                case "Кровля":
                    return new List<string>
                    {
                        "осмотреть поврежденные участки покрытия",
                        "демонтировать ослабленные элементы и примыкания",
                        "восстановить основание и уложить новый материал",
                        "проверить герметичность стыков и водоотведение"
                    };
                case "Инженерные сети":
                    return new List<string>
                    {
                        "определить проблемный участок сети",
                        "демонтировать поврежденный фрагмент",
                        "смонтировать заменяемые трубы и соединения",
                        "проверить герметичность и работоспособность линии"
                    };
                case "Электромонтаж":
                    return new List<string>
                    {
                        "выявить поврежденные линии и точки подключения",
                        "демонтировать неисправные кабели или элементы",
                        "выполнить замену и подключение новых участков",
                        "проверить схему и целостность цепей"
                    };
                case "Отделка":
                    return new List<string>
                    {
                        "очистить дефектные участки поверхности",
                        "восстановить основание и проблемные зоны",
                        "нанести новый выравнивающий или отделочный слой",
                        "проверить ровность и качество поверхности"
                    };
                default:
                    return new List<string>
                    {
                        "осмотреть проблемные участки",
                        "демонтировать поврежденные элементы",
                        "восстановить основание или узлы крепления",
                        "проверить результат ремонта"
                    };
            }
        }

        private List<string> BuildDemolitionStages(ConstructionTaskProfile profile, string loweredTitle)
        {
            var objectPhrase = ResolveObjectPhrase(loweredTitle, profile);
            return new List<string>
            {
                "подготовить участок и смежные зоны к демонтажу",
                $"демонтировать существующие элементы {objectPhrase}",
                "очистить основание и убрать отходы работ",
                "подготовить участок к последующему монтажу"
            };
        }

        private List<string> BuildReplacementStages(ConstructionTaskProfile profile, string loweredTitle)
        {
            return new List<string>
            {
                "демонтировать изношенные элементы",
                "подготовить основание и точки крепления",
                "смонтировать новые элементы на участке",
                "проверить стыки, крепления и итоговый результат"
            };
        }

        private string ResolveObjectPhrase(string loweredTitle, ConstructionTaskProfile profile)
        {
            if (ContainsAny(loweredTitle, "фундамент", "ростверк"))
            {
                return "фундамента";
            }

            if (ContainsAny(loweredTitle, "плита"))
            {
                return "плиты";
            }

            if (ContainsAny(loweredTitle, "кровл", "крыша"))
            {
                return "кровли";
            }

            if (ContainsAny(loweredTitle, "стропил"))
            {
                return "стропильной системы";
            }

            if (ContainsAny(loweredTitle, "перегород"))
            {
                return "перегородок";
            }

            if (ContainsAny(loweredTitle, "стен"))
            {
                return "стен";
            }

            if (ContainsAny(loweredTitle, "кирпич"))
            {
                return "кирпичной кладки";
            }

            if (ContainsAny(loweredTitle, "опалуб"))
            {
                return "опалубки";
            }

            if (ContainsAny(loweredTitle, "арматур"))
            {
                return "арматурного каркаса";
            }

            if (ContainsAny(loweredTitle, "водоснабж"))
            {
                return "системы водоснабжения";
            }

            if (ContainsAny(loweredTitle, "канализац"))
            {
                return "системы канализации";
            }

            if (ContainsAny(loweredTitle, "отоплен"))
            {
                return "системы отопления";
            }

            if (ContainsAny(loweredTitle, "электропровод"))
            {
                return "электропроводки";
            }

            if (ContainsAny(loweredTitle, "кабел"))
            {
                return ContainsAny(loweredTitle, "кабеля", "кабель") ? "кабеля" : "кабельных линий";
            }

            if (ContainsAny(loweredTitle, "освещен"))
            {
                return "системы освещения";
            }

            if (ContainsAny(loweredTitle, "стяжк"))
            {
                return "пола";
            }

            if (ContainsAny(loweredTitle, "штукатур"))
            {
                return ContainsAny(loweredTitle, "потол") ? "потолков" : "стен";
            }

            if (ContainsAny(loweredTitle, "шпаклев"))
            {
                return ContainsAny(loweredTitle, "потол") ? "потолков" : "стен";
            }

            if (ContainsAny(loweredTitle, "окраск"))
            {
                if (ContainsAny(loweredTitle, "фасад"))
                {
                    return "фасада";
                }

                return ContainsAny(loweredTitle, "стен") ? "стен" : "поверхностей";
            }

            if (ContainsAny(loweredTitle, "гидроизоляц"))
            {
                if (ContainsAny(loweredTitle, "подвал"))
                {
                    return "подвала";
                }

                if (ContainsAny(loweredTitle, "кровл"))
                {
                    return "кровли";
                }

                return "конструкции";
            }

            if (ContainsAny(loweredTitle, "утеплен"))
            {
                if (ContainsAny(loweredTitle, "фасад"))
                {
                    return "фасада";
                }

                if (ContainsAny(loweredTitle, "кровл"))
                {
                    return "кровли";
                }

                return "конструкции";
            }

            return profile?.ObjectPhrase ?? "конструкции";
        }

        private string ApplyModeToWorkPhrase(string workPhrase, string loweredTitle)
        {
            if (string.IsNullOrWhiteSpace(workPhrase))
            {
                return workPhrase;
            }

            if (ContainsAny(loweredTitle, "чернов"))
            {
                if (workPhrase.StartsWith("заливку ", StringComparison.OrdinalIgnoreCase) ||
                    workPhrase.StartsWith("кладку ", StringComparison.OrdinalIgnoreCase) ||
                    workPhrase.StartsWith("штукатурку ", StringComparison.OrdinalIgnoreCase) ||
                    workPhrase.StartsWith("шпаклевку ", StringComparison.OrdinalIgnoreCase))
                {
                    return "черновую " + workPhrase;
                }

                return workPhrase + " на черновом этапе";
            }

            if (ContainsAny(loweredTitle, "чистов"))
            {
                if (workPhrase.StartsWith("штукатурку ", StringComparison.OrdinalIgnoreCase) ||
                    workPhrase.StartsWith("шпаклевку ", StringComparison.OrdinalIgnoreCase) ||
                    workPhrase.StartsWith("окраску ", StringComparison.OrdinalIgnoreCase))
                {
                    return "чистовую " + workPhrase;
                }

                return workPhrase + " в чистовом исполнении";
            }

            if (ContainsAny(loweredTitle, "финиш"))
            {
                return workPhrase + " с финишной доводкой";
            }

            return workPhrase;
        }

        private bool ContainsAny(string value, params string[] fragments)
        {
            var source = value ?? string.Empty;
            return fragments != null && fragments.Any(fragment => source.Contains(fragment));
        }

        private string BuildFallbackDescription(TaskDescriptionGenerationRequest request)
        {
            var profile = SelectBestProfile(request.Title);
            var workPhrase = BuildSpecificWorkPhrase(request.Title, profile);
            var resultPhrase = BuildResultHint(request.Title, profile);
            var stages = BuildOperationHints(request.Title, profile);
            var stageText = JoinFragments(stages.Take(4).ToList());

            if (profile == null)
            {
                var siteSuffix = string.IsNullOrWhiteSpace(request.SiteName)
                    ? string.Empty
                    : $" на объекте «{request.SiteName}»";

                return $"Необходимо выполнить {workPhrase}{siteSuffix}. Следует подготовить участок, провести основной этап работ и проверить результат, чтобы {resultPhrase}.";
            }

            var variant = Math.Abs((request.Title ?? string.Empty).GetHashCode()) % 3;
            var sitePart = string.IsNullOrWhiteSpace(request.SiteName)
                ? string.Empty
                : $" на объекте «{request.SiteName}»";

            switch (variant)
            {
                case 0:
                    return $"Необходимо выполнить {workPhrase}{sitePart}. Следует {stageText}, после чего {resultPhrase}.";
                case 1:
                    return $"Для выполнения задачи требуется выполнить {workPhrase}{sitePart}. Необходимо {stageText}; по завершении нужно {resultPhrase}.";
                default:
                    return $"Работы по задаче предусматривают {workPhrase}{sitePart}. Требуется {stageText}, чтобы {resultPhrase}.";
            }
        }

        private string CollapseWhitespace(string text)
        {
            return ExtraWhitespaceRegex.Replace((text ?? string.Empty).Replace("\r", " ").Replace("\n", " "), " ").Trim();
        }

        private string RemoveLeadingLabel(string text, string label)
        {
            return !string.IsNullOrWhiteSpace(text) && text.StartsWith(label, StringComparison.OrdinalIgnoreCase)
                ? text.Substring(label.Length).Trim()
                : text;
        }

        private static string ReadSetting(string key, string fallbackValue)
        {
            return ConfigurationManager.AppSettings[key] ?? fallbackValue;
        }

        private static bool ParseBool(string value, bool fallbackValue)
        {
            return bool.TryParse(value, out var parsed) ? parsed : fallbackValue;
        }

        private static int ParseInt(string value, int fallbackValue)
        {
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallbackValue;
        }

        private static string TrimOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal class ConstructionTaskProfile
    {
        public string Name { get; set; }
        public string ObjectPhrase { get; set; }
        public string[] Keywords { get; set; }
        public string WorkPhrase { get; set; }
        public string[] StagePhrases { get; set; }
        public string ResultPhrase { get; set; }
    }

    internal class LmStudioChatRequest
    {
        public string model { get; set; }
        public string input { get; set; }
        public string system_prompt { get; set; }
        public double temperature { get; set; }
        public int max_output_tokens { get; set; }
        public int context_length { get; set; }
        public bool stream { get; set; }
    }

    internal class LmStudioLoadModelRequest
    {
        public string model { get; set; }
        public int context_length { get; set; }
    }

    internal class LmStudioModelsResponse
    {
        public List<LmStudioModelInfo> models { get; set; }
    }

    internal class LmStudioModelInfo
    {
        public string key { get; set; }
        public List<LmStudioLoadedInstance> loaded_instances { get; set; }
    }

    internal class LmStudioLoadedInstance
    {
        public string id { get; set; }
    }

    internal class LmStudioChatResponse
    {
        public List<LmStudioChatOutputItem> output { get; set; }
    }

    internal class LmStudioChatOutputItem
    {
        public string type { get; set; }
        public string content { get; set; }
    }
}
