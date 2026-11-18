# AI Helper Budz

Дипломный проект персонализированного AI-ассистента. Сервер перенесён на C# 14 и ASP.NET Core 10. Веб-интерфейс реализован на Vue 3 и TypeScript. Данные хранятся в PostgreSQL 16 через EF Core 10 и Npgsql.

## Запуск

Запустите Docker и выполните из корня репозитория.

```sh
docker compose up --build -d --wait
```

- Приложение — [127.0.0.1:8080](http://127.0.0.1:8080).
- Документация API — [Swagger](http://127.0.0.1:8080/swagger/index.html).
- Проверка готовности приложения и PostgreSQL — [health](http://127.0.0.1:8080/health).

Compose по умолчанию запускает демонстрационное окружение с локальным провайдером. Миграции применяются при старте, а повторный запуск не дублирует сиды. Для проверки можно зарегистрироваться или использовать тестовые аккаунты.

| Роль | Логин | Тестовый пароль |
|---|---|---|
| Пользователь | `demo-02` | `Budz-local-demo-2026` |
| Администратор | `demo-admin` | `Budz-local-demo-2026` |

Для реального OpenRouter скопируйте [.env.example](.env.example) в `.env`, задайте `AI_MODE=OpenRouter` и заполните `OPENROUTER_API_KEY`. После изменения настроек повторите запуск Compose. Оплату можно продемонстрировать кнопкой «Заполнить тестовыми данными» на странице тарифов. Сервер хранит только сумму и маску тестовой карты.

Остаток дневного бюджета виден в шапке. Лимит считается по входным и выходным токенам и обновляется в полночь UTC. Тарифы и бюджеты редактируются в разделе администратора. В верхнем меню «Персонализация» открывает готовые стили, правила для разных задач и настройки памяти. Там же можно проверить свой запрос и указать предыдущую реплику для разбора уточнения.

## Разработка и тесты

Требуются .NET SDK 10.0.401 или более новая feature-версия .NET 10, Node.js 24 и Docker для интеграционных тестов.

```sh
dotnet build AiAssistant.sln
dotnet test AiAssistant.sln --collect:"XPlat Code Coverage"
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend run dev
```

Vite слушает порт 5173 и направляет запросы в локальный API на 8081. Автоматические интеграционные тесты поднимают собственный контейнер PostgreSQL. Для просмотра EF-миграций выполните `dotnet tool restore` и `dotnet ef migrations list -p src/AiAssistant.Infrastructure -s src/AiAssistant.Api --no-connect`.

Проверка браузера запускается на работающем стеке после `npm --prefix frontend exec -- playwright install chromium`.

```sh
npm --prefix frontend run test:e2e
```

При необходимости задайте `BUDZ_PREVIEW` для другого локального адреса и `BUDZ_BROWSER=msedge` для Edge. Тест создаёт отдельный аккаунт, проверяет пользовательские сценарии и удаляет аккаунт. Снимки браузерного прогона сохраняются в игнорируемом каталоге `.local/ui-verification`.

## Как устроен проект

- `src/AiAssistant.Domain` — сущности и состояния.
- `src/AiAssistant.Application` — сценарии, CQRS-команды и запросы, интерфейсы инфраструктуры.
- `src/AiAssistant.Infrastructure` — EF Core, миграции, файловое хранилище, OpenRouter и JWT.
- `src/AiAssistant.Api` — HTTP, SignalR, документация и фоновые службы.
- `frontend` — Vue 3, TypeScript, Pinia, Vue Router, Vite и клиент SignalR.
- `tests` — xUnit и интеграционные проверки PostgreSQL.
- `docs` — [архитектура для пояснительной записки](docs/architecture.md), [матрица приёмки](docs/acceptance.md) и [снимки интерфейса](docs/screenshots/).

Начальная версия Laravel сохранена в истории Git в ревизии `7ca12b8`.

## Настройки

| Настройка | Назначение |
|---|---|
| `WEB_PORT`, `API_PORT`, `DB_PORT` | Локальные порты Compose, по умолчанию 8080, 8081 и 15432 |
| `POSTGRES_PASSWORD` | Пароль локальной базы Compose |
| `SEED_DEMO`, `DEMO_PASSWORD` | Создание и начальный пароль демонстрационных учётных записей |
| `AI_MODE`, `OPENROUTER_API_KEY` | Выбор демо-провайдера или OpenRouter и ключ провайдера |
| `ConnectionStrings__Postgres` | Строка подключения для API вне Compose |
| `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience` | Настройки JWT. В Development ключ при отсутствии создаётся в памяти. Для постоянного окружения задайте ключ в менеджере секретов |
| `Frontend__Origin` | Ожидаемый origin SPA при проксировании через отдельный адрес. По умолчанию берётся адрес текущего HTTP-запроса |
| `Files__Directory` | Корень приватного хранилища. Compose использует отдельный том |
| `Ai__Model`, `Ai__ContextBudget`, `Ai__ModelBudgets__0__Model`, `Ai__ModelBudgets__0__Tokens` | Модель по умолчанию и бюджеты ввода, общие и по моделям |
| `Ai__VisionModels__0` | Первый идентификатор модели с поддержкой изображений |
| `Diagnostics__AllowContext`, `Diagnostics__Swagger` | Просмотр контекста администратором и публикация Swagger в окружениях вне Development |

Все адреса Compose привязаны к loopback. Для развёртывания вне локального компьютера требуется HTTPS и назначение собственных секретов и источников конфигурации.
