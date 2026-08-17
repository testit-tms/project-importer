# Анализ миграции Importer: TestIT.ApiClient → TestIT.AdaptersApi

Дата анализа: 2026-08-17  
Версии: `TestIt.ApiClient` **5.5.6** (NuGet), `TestIT.AdaptersApi` **1.1.0** (локальный проект).

## Краткий вывод

| Метрика | Значение |
|--------|----------|
| API-клиентов в Importer | **8** (`IAttachmentsApi`, `IProjectsApi`, `IProjectAttributesApi`, `IProjectSectionsApi`, `ISectionsApi`, `ICustomAttributesApi`, `IWorkItemsApi`, `IParametersApi`) |
| Покрыто AdaptersApi напрямую | **6 из 8** (~75% вызовов по количеству) |
| Полностью отсутствует в AdaptersApi | **`ICustomAttributesApi`** (глобальные атрибуты) |
| Блокер полной замены | **4 метода** работы с глобальными custom attributes + связанные модели |
| Оценка миграции без доработки OpenAPI | **~70%** функционала Importer |

**Полная замена `TestIT.ApiClient` в Importer сейчас невозможна** без расширения Adapters OpenAPI (глобальные атрибуты). Остальное — в основном механическая замена клиента, namespace моделей и имён методов.

---

## Где Importer ходит в API сегодня

Единственная точка интеграции — `ClientAdapter` + регистрация в `ServiceCollectionExtensions.RegisterApiServices`.

### Используемые вызовы (TestIT.ApiClient)

| # | ClientAdapter / сервис | ApiClient метод | REST (типичный путь ApiClient) |
|---|------------------------|-----------------|--------------------------------|
| 1 | `GetProject` | `IProjectsApi.ApiV2ProjectsSearchPostAsync` | `POST /api/v2/projects/search` |
| 2 | `CreateProject` | `IProjectsApi.CreateProjectAsync` | `POST /api/v2/projects` |
| 3 | `AddAttributesToProject` | `IProjectsApi.AddGlobalAttributesToProjectAsync` | `POST /api/v2/projects/{id}/attributes/global` |
| 4 | `GetRootSectionId` | `IProjectSectionsApi.GetSectionsByProjectIdAsync` | `GET /api/v2/projects/{id}/sections` |
| 5 | `ImportSection`, `GetSection`* | `ISectionsApi.CreateSectionAsync` | `POST /api/v2/sections` |
| 6 | `ImportSharedStep`, `ImportTestCase` | `IWorkItemsApi.ApiV2WorkItemsPostAsync` | `POST /api/v2/workItems` |
| 7 | `UploadAttachment` | `IAttachmentsApi.ApiV2AttachmentsPostAsync` | `POST /api/v2/attachments` |
| 8 | `CreateParameter` | `IParametersApi.CreateParameterAsync` | `POST /api/v2/parameters` |
| 9 | `GetParameter` | `IParametersApi.ApiV2ParametersSearchPostAsync` | `POST /api/v2/parameters/search` |
| 10 | `ImportAttribute` | `ICustomAttributesApi.ApiV2CustomAttributesGlobalPostAsync` | `POST /api/v2/customAttributes/global` |
| 11 | `GetAttribute`, `GetProjectAttributeById` | `ICustomAttributesApi.ApiV2CustomAttributesIdGetAsync` | `GET /api/v2/customAttributes/{id}` |
| 12 | `GetProjectAttributes` | `ICustomAttributesApi.ApiV2CustomAttributesSearchPostAsync` | `POST /api/v2/customAttributes/search` |
| 13 | `UpdateAttribute` | `ICustomAttributesApi.ApiV2CustomAttributesGlobalIdPutAsync` | `PUT /api/v2/customAttributes/global/{id}` |
| 14 | `GetRequiredProjectAttributesByProjectId` | `IProjectAttributesApi.SearchAttributesInProjectAsync` | `POST /api/v2/projects/{id}/attributes/search` |
| 15 | `UpdateProjectAttribute` | `IProjectAttributesApi.UpdateProjectsAttributeAsync` | `PUT /api/v2/projects/{id}/attributes` |

\* `GetSection` объявлен в `ClientAdapter`, но **не входит** в `IClientAdapter` и, судя по коду, не используется сервисами импорта.

---

## Что есть в TestIT.AdaptersApi

### API-классы (12)

| API | Базовый префикс | Используется Importer |
|-----|-----------------|----------------------|
| `ProjectsApi` | `/adapters/projects` | Да |
| `ProjectSectionsApi` | `/adapters/projects/{projectId}/sections` | Да |
| `SectionsApi` | `/adapters/sections` | Да |
| `WorkItemsApi` | `/adapters/workItems` | Да |
| `AttachmentsApi` | `/adapters/attachments` | Да |
| `ParametersApi` | `/adapters/parameters` | Да |
| `ProjectAttributesApi` | `/adapters/projects/{projectId}/attributes` | Да |
| `CustomAttributesApi` | — | **Нет такого класса** |
| `AutoTestsApi` | `/adapters/autoTests` | Нет |
| `TestRunsApi` | `/adapters/testRuns` | Нет |
| `ConfigurationsApi` | `/adapters/configurations` | Нет |
| `WorkflowsApi` | `/adapters/workflows` | Нет |
| `ProjectWorkItemsApi` | `/adapters/projects/{projectId}/workItems` | Нет |

Adapters API — отдельный контракт (`/adapters/...`), не зеркало `/api/v2/...`. На бэкенде это, как правило, тот же TMS, но другой gateway/спека для интеграций и адаптеров.

---

## Матрица: можно / нельзя / с оговорками

### ✅ Можно перевести (есть аналог)

| Importer | ApiClient | AdaptersApi | Примечание |
|----------|-----------|-------------|------------|
| `GetProject` | `ApiV2ProjectsSearchPostAsync` | `AdaptersProjectsSearchPostAsync` | Ответ: `ProjectShortModel` → `ProjectApiResult` (поле `Name` есть; проверить лишние/отсутствующие поля при маппинге) |
| `CreateProject` | `CreateProjectAsync` | `AdaptersProjectsPostAsync` | Модель `CreateProjectApiModel` есть в обоих клиентах (разные namespace) |
| `AddAttributesToProject` | `AddGlobalAttributesToProjectAsync` | `AdaptersProjectsIdAttributesGlobalPostAsync` | Сигнатура: `string projectId` → `Guid id` |
| `GetRootSectionId` | `GetSectionsByProjectIdAsync` | `AdaptersProjectsProjectIdSectionsGetAsync` | `projectId.ToString()` → `Guid projectId` |
| `ImportSection` | `CreateSectionAsync` | `AdaptersSectionsPostAsync` | `SectionPostModel`, `SectionWithStepsModel` — есть в AdaptersApi |
| `ImportSharedStep`, `ImportTestCase` | `ApiV2WorkItemsPostAsync` | `AdaptersWorkItemsPostAsync` | `CreateWorkItemApiModel` и вложенные модели (`CreateStepApiModel`, `CreateLinkApiModel`, `AssignIterationApiModel`, `LinkType`, …) присутствуют |
| `UploadAttachment` | `ApiV2AttachmentsPostAsync` | `AdaptersAttachmentsPostAsync` | `FileParameter` — свой тип в `TestIT.AdaptersApi.Client` |
| `CreateParameter` | `CreateParameterAsync` | `AdaptersParametersPostAsync` | Путь: `/api/v2/parameters` → `/adapters/parameters` |
| `GetParameter` | `ApiV2ParametersSearchPostAsync` | `AdaptersParametersSearchPostAsync` | Фильтр `ParametersFilterApiModel` есть |
| `GetRequiredProjectAttributesByProjectId` | `SearchAttributesInProjectAsync` | `AdaptersProjectsProjectIdAttributesSearchPostAsync` | Ответ: `CustomAttributeGetModel` → `CustomAttributeModel` |
| `UpdateProjectAttribute` | `UpdateProjectsAttributeAsync` | `AdaptersProjectsProjectIdAttributesPutAsync` | `CustomAttributePutModel` есть |

### ❌ Нельзя перевести (нет в AdaptersApi)

| Importer | ApiClient | Почему блокер |
|----------|-----------|---------------|
| `ImportAttribute` | `ApiV2CustomAttributesGlobalPostAsync` | Нет `CustomAttributesApi`, нет `POST .../customAttributes/global` в adapters-спеке |
| `GetAttribute` | `ApiV2CustomAttributesIdGetAsync` | Нет GET глобального атрибута по id |
| `GetProjectAttributes` | `ApiV2CustomAttributesSearchPostAsync` | Нет search по глобальным атрибутам |
| `UpdateAttribute` | `ApiV2CustomAttributesGlobalIdPutAsync` | Нет update глобального атрибута |

Эти методы критичны для импорта **кастомных атрибутов** (`AttributeService` → создание/обновление опций, привязка к проекту).

### ⚠️ Можно частично / потребует доработки кода

| Тема | Детали |
|------|--------|
| **Namespace моделей** | `TestIT.ApiClient.Model.*` → `TestIT.AdaptersApi.Model.*` — массовая замена `using`, типы совпадают по именам, но это **разные сборки** |
| **Configuration / DI** | `ApiConfigurationFactory` и `ServiceCollectionExtensions` завязаны на `TestIT.ApiClient.Client.Configuration`; нужен аналог для `TestIT.AdaptersApi.Client.Configuration` |
| **HttpClient** | Тот же `IHttpClientFactory` + PrivateToken, но `Activator.CreateInstance` для API-классов AdaptersApi |
| **Target framework** | AdaptersApi: `netstandard2.0`, Importer: `net8.0` — совместимо |
| **Ответы search projects** | Логика сравнения `project.Name == name` должна работать на `ProjectApiResult` |
| **Project attributes search** | В Importer маппинг с `CustomAttributeGetModel`; в AdaptersApi — `CustomAttributeModel` (другой набор полей: `code`, `targets`, `isSystem`, …) — проверить маппинг в `GetRequiredProjectAttributesByProjectId` |

---

## Чего не хватает в AdaptersApi для **полной** замены

### 1. API (критично)

Нужен новый контроллер/раздел OpenAPI, условно **`CustomAttributesApi`**:

| Метод | Предлагаемый adapters-путь | Зачем Importer |
|-------|------------------------------|----------------|
| `POST` create global attribute | `POST /adapters/customAttributes/global` | `ImportAttribute` |
| `GET` attribute by id | `GET /adapters/customAttributes/{id}` | `GetAttribute`, `GetProjectAttributeById` |
| `POST` search global attributes | `POST /adapters/customAttributes/search` | `GetProjectAttributes` |
| `PUT` update global attribute | `PUT /adapters/customAttributes/global/{id}` | `UpdateAttribute` (в т.ч. динамическое добавление options) |

Без этого Importer не сможет:
- создавать глобальные атрибуты при импорте;
- искать существующие глобальные атрибуты;
- обновлять options при импорте (`BaseWorkItemService.ConvertAttributeValue` → `UpdateAttribute`).

### 2. Модели (критично)

В AdaptersApi **отсутствуют** (есть только project-scoped `CustomAttributeModel` / `CustomAttributePutModel`):

| Модель ApiClient | Использование в Importer |
|------------------|--------------------------|
| `GlobalCustomAttributePostModel` | `ImportAttribute` |
| `GlobalCustomAttributeUpdateModel` | `UpdateAttribute` |
| `CustomAttributeOptionPostModel` | создание options при import attribute |
| `CustomAttributeSearchQueryModel` | фильтр `isGlobal`, `isDeleted` |
| `CustomAttributeSearchResponseModel` | `GetProjectAttributes` (есть `workItemUsage`, `testPlanUsage`) |

Желательно также явно описать в adapters-спеке модель ответа create/update global attribute (аналог `CustomAttributeModel` из v2 API).

### 3. Некритично для Importer (но есть в ApiClient / нет в Adapters)

Importer **не использует**, поэтому для импорта не блокирует:

- update/delete work items;
- bulk attachments (`AdaptersAttachmentsBulkPostAsync` в AdaptersApi уже есть, Importer не использует);
- `WorkItemsApi` search/get (AdaptersApi имеет, Importer — нет);
- AutoTests, TestRuns, Configurations, Workflows, ProjectWorkItems search.

---

## Рекомендуемый план миграции

### Фаза 1 — без изменения OpenAPI (гибрид)

1. Подключить `TestIT.AdaptersApi` как ProjectReference в `Importer.csproj`.
2. Перевести на AdaptersApi: Projects, ProjectSections, Sections, WorkItems, Attachments, Parameters, ProjectAttributes.
3. Оставить `ICustomAttributesApi` из `TestIT.ApiClient` (или вынести в отдельный «legacy» wrapper).
4. Обновить `ApiConfigurationFactory` / DI для двух `Configuration` (или общий абстрактный фабричный слой).

### Фаза 2 — полная замена

1. Добавить в adapters OpenAPI global custom attributes (4 endpoint + модели).
2. Сгенерировать/обновить `TestIT.AdaptersApi`.
3. Удалить зависимость `TestIt.ApiClient` из Importer.
4. Прогнать `ClientAdapterTests` + интеграционный импорт на стенде.

---

## Сводная таблица покрытия по `IClientAdapter`

| Метод IClientAdapter | AdaptersApi | Статус |
|----------------------|-------------|--------|
| `GetProject` | `ProjectsApi` | ✅ |
| `CreateProject` | `ProjectsApi` | ✅ |
| `ImportSection` | `SectionsApi` | ✅ |
| `ImportAttribute` | — | ❌ нет API |
| `GetAttribute` | — | ❌ нет API |
| `ImportSharedStep` | `WorkItemsApi` | ✅ |
| `ImportTestCase` | `WorkItemsApi` | ✅ |
| `GetRootSectionId` | `ProjectSectionsApi` | ✅ |
| `GetProjectAttributes` | — | ❌ нет API |
| `GetRequiredProjectAttributesByProjectId` | `ProjectAttributesApi` | ✅ |
| `GetProjectAttributeById` | — | ❌ (сейчас через global GET by id) |
| `AddAttributesToProject` | `ProjectsApi` | ✅ |
| `UpdateAttribute` | — | ❌ нет API |
| `UpdateProjectAttribute` | `ProjectAttributesApi` | ✅ |
| `UploadAttachment` | `AttachmentsApi` | ✅ |
| `CreateParameter` | `ParametersApi` | ✅ |
| `GetParameter` | `ParametersApi` | ✅ |

**Итого:** 12 методов можно перевести сейчас, **5 методов** завязаны на отсутствующий global custom attributes API.

---

## Технические заметки для разработчиков

- AdaptersApi генерируется из OpenAPI (`RootNamespace`: `TestIT.AdaptersApi`); правки в `.cs` вручную нежелательны — источник правды — спека на бэкенде.
- Имена методов в AdaptersApi: префикс `Adapters*` (например `AdaptersWorkItemsPostAsync` вместо `ApiV2WorkItemsPostAsync`).
- Авторизация: как и сейчас, `PrivateToken` в заголовке `Authorization` через `Configuration.AddApiKey`.
- `TestCaseImportErrorLogService` использует `TestIT.ApiClient.Model` — при миграции проверить, не тянет ли лишние типы ApiClient.

---

## Связанные файлы в репозитории

| Файл | Роль |
|------|------|
| `Importer/Client/Implementations/ClientAdapter.cs` | все HTTP-вызовы |
| `Importer/Client/Extensions/ServiceCollectionExtensions.cs` | регистрация 8 API-клиентов |
| `Importer/Client/Implementations/ApiConfigurationFactory.cs` | BasePath + token |
| `TestIT.AdaptersApi/` | новый generated client |
| `Importer/Importer.csproj` | `TestIt.ApiClient` 5.5.6 |
