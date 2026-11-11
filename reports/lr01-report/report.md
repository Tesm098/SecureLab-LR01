# Звіт до лабораторної роботи № 1

**Дисципліна:** «Прикладні технології програмування в інформаційній безпеці»  
**Тема:** «Запуск, дослідження та невелике розширення готової вебсистеми»  
**Студент:** Володимир Кліванський  
**Дата оформлення:** 11.11.2026  
**Рівень виконання:** достатній («Задовільно»)

## 1. Ідентифікація стану

- Початкова конфігурація: спільний baseline **2-A «Трекер інцидентів»**; індивідуального варіанта немає.
- Основна гілка: `main`.
- Робоча гілка: `lab/1-system`.
- Початковий commit: `e81491a` (створений із ZIP starter).
- Фінальний тег **після злиття й фінальних перевірок**: `v0.1.0`.
- **Фінальний commit hash:** `6ebeaade296c94409d97fe62c85edf27e11d0198`.
- **Спосіб передавання DEL-01:** `https://github.com/Tesm098/SecureLab-LR01`.

**Середовище:** Windows, Git 2.56.0, .NET SDK 10.0.403, Docker 29.9.2, Docker Compose 5.6.1; PostgreSQL 18.4 у Docker Compose. Порт API — `5080`, опублікований локальний порт PostgreSQL — `127.0.0.1:54329`. `global.json` адаптовано під стабільну .NET SDK сімейства 10 (`10.0.100`, `latestFeature`, `allowPrerelease: false`).

## 2. Змінений маршрут

Спочатку досліджено наявний шлях перегляду деталей:

```text
Натискання картки у браузері
  → Client/app.js: loadIncidentDetails()
  → GET /api/incidents/{id}
  → Presentation/Endpoints/IncidentEndpoints.cs: GetDetailsAsync()
  → Application/Incidents/IncidentQueries.cs: GetDetailsAsync()
  → Data/SecureLabDbContext.cs: dbContext.Incidents
  → PostgreSQL: таблиця incidents
  → IncidentDetailsResponse → JSON
  → Client/app.js: renderIncidentDetails()
  → DOM: textContent / document.createTextNode()
```

Під час перевірки `GET /api/incidents/20000000-0000-0000-0000-000000000003` повернув `200 OK` з `application/json`. У SQL-запиті до `incidents` підтверджено ID, назву «Перевірка журналу комп'ютерного класу», `Low`, `New`. Для ID `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa`, якого немає в seed, отримано `404 Not Found`, `application/problem+json` і повідомлення «Інцидент не знайдено». Фактичний `traceId` зафіксований у скриншоті відповіді, а не переписаний зі зразка.

Новий наскрізний маршрут:

```text
Client/index.html: кнопка «Показати підсумок»
  → Client/app.js: loadSeveritySummary()
  → GET /api/incidents/severity-summary
  → Presentation/Endpoints/IncidentEndpoints.cs: GetSeveritySummaryAsync()
  → Application/Incidents/IncidentQueries.cs: GetSeveritySummaryAsync()
  → Data/SecureLabDbContext.cs: dbContext.Incidents
  → PostgreSQL: GROUP BY severity, COUNT(*) у таблиці incidents
  → IncidentSeveritySummaryResponse → JSON
  → Client/app.js: createElement("li") + textContent
```

Межі довіри: **(1)** браузер → API: клієнт може змінити HTTP-запит; готові маршрути перевіряють `status` і формат `:guid`; **(2)** API ↔ PostgreSQL: умови запиту обробляються через EF Core і застосовується проєкція лише потрібних полів; **(3)** API → браузер / DOM: DTO обмежує поля відповіді, а `textContent` не виконує дані як HTML. Повна карта — `docs/architecture.md` у репозиторії.

## 3. Виконані зміни

1. У `Presentation/Contracts/IncidentResponses.cs` додано `IncidentSeveritySummaryResponse(string Severity, int Count)`.
2. У `Application/Incidents/IncidentQueries.cs` реалізовано асинхронне читання `dbContext.Incidents.AsNoTracking()`, `GroupBy(Severity)`, `Count()` і `ToListAsync(cancellationToken)`. Завантажуються агрегати, а не повний перелік інцидентів.
3. У `Presentation/Endpoints/IncidentEndpoints.cs` початкову заглушку `501 Not Implemented` замінено endpoint із DI та `Results.Ok(summary)`; OpenAPI metadata описує успішну відповідь.
4. У `Client/index.html` додано кнопку, текстовий статус і список підсумку.
5. У `Client/app.js` додано `loadSeveritySummary()` з `apiFetch()`, очищенням результату та безпечним створенням `li` через `createElement()` і `textContent`.
6. У `tests/http/incidents.http` оновлено коментар до наявного T-06 з `501` на `200` і задокументовано очікуваний підсумок; файл містить відтворюваний HTTP-запит.
7. У `docs/architecture.md` заповнено компоненти, маршрут, межі довіри, конфігураційні входи та спосіб seed/reset.

**Контракт T-06:** метод `GET`, без request body; `200 OK`, масив JSON-об'єктів тільки з `severity` і `count`. Обрана політика нульових груп — **лише наявні категорії**; за порожньої таблиці `[]`. Сталий порядок — за порядком значень enum `IncidentSeverity`: Low → Medium → High → Critical. На початковому seed Critical відсутня.

Очікувана та фактично спостережена відповідь:

```json
[
  { "severity": "Low", "count": 1 },
  { "severity": "Medium", "count": 1 },
  { "severity": "High", "count": 1 }
]
```

В окремій папці `lr01-report/evidence/` зберігаються скриншоти для перевірки. До репозиторію звіт і докази не комітяться.

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| CP-01 | Docker і PostgreSQL доступні | `docker compose ... ps` | PostgreSQL `healthy` | `secure-lab-postgres-1` — `healthy`, порт `54329` | [Docker](evidence/docker-healthy.png) |
| T-01 | PostgreSQL healthy, API запущено | `GET /health` | `200 OK`, `status: ready` | Отримано `{"status":"ready"}` | [Health](evidence/health-ready.png) |
| Дослідження list-flow | Відновлено seed | `GET /api/incidents?status=Triaged` | Один інцидент, `200`, JSON | Один інцидент Medium/Triaged; збігається з SQL | [Network](evidence/list-network.png), [Response](evidence/list-response.png), [PostgreSQL](evidence/postgres-triaged.png) |
| CP-02 (details) | Відновлено seed | `GET /api/incidents/20000000-0000-0000-0000-000000000003` | `200`, дані деталей | `200 OK`, `application/json`; запис підтверджено в SQL | [Network](evidence/details-200-network.png), [SQL](evidence/details-postgres.png) |
| T-04 | Невідомий коректний UUID | `GET /api/incidents/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa` | `404`, Problem Details | `404 Not Found`, `application/problem+json` | [404 і Network](evidence/details-404.png) |
| До зміни | Початковий endpoint | `GET /api/incidents/severity-summary` | `501 Not Implemented` | Отримано `501` | [Baseline 501](evidence/summary-before-501.png) |
| T-06 | Реалізований endpoint, відновлений seed | `GET /api/incidents/severity-summary` | `200`, Low/Medium/High по 1; сталий порядок | `200 OK`, усі три групи по 1; Critical відсутній | [JSON 200](evidence/summary-json-200.png) |
| T-07 | API та браузерний клієнт запущені | Натиснути «Показати підсумок» | Кнопка викликає API і безпечно показує результати | Виведено Low: 1, Medium: 1, High: 1; Network — `200 OK` | [Інтерфейс](evidence/summary-browser.png), [Network](evidence/summary-network.png) |
| T-08 (seed/reset) | Локальна навчальна база | `--reset-database`, повторний перегляд списку | Seed відновлено | Підтверджено повідомлення про повторне заповнення; у браузері знову три інциденти | [Reset](evidence/reset-seed.png) |
| Автотести | PostgreSQL доступна | `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release` | 4 baseline-тести проходять | `total: 4; succeeded: 4; failed: 0; skipped: 0`; `Build succeeded` | [Тести](evidence/tests-four-passed.png) |

**Останній зафіксований запуск тестів:** `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release`. Результат: чотири тести пройдено, жодної помилки, збірка успішна. Ці результати отримано до фінального merge та тегу; за методичкою **після merge потрібно повторити перевірки** й за потреби оновити цей рядок.

**Відтворюваний сценарій:** `tests/http/incidents.http`, запит T-06. Його підготовлено, але запуск саме з `.http`-клієнта окремо ще не підтверджено; фактичний запит перевірено в браузері та DevTools. Перед поданням потрібно виконати `.http`-запит або підтвердити його виконання і додати результат.

## 5. Security-сценарій

У цій лабораторній роботі **не передбачено навмисно вразливого стану або експлуатаційного PoC**. Проведено перевірку безпечного відображення даних із PostgreSQL та обмеження полів у DTO.

**Гіпотеза.** Якщо текст збереженого інциденту потрапить у DOM як HTML, фрагмент на кшталт `<script>` може бути інтерпретований браузером.

**Локальна перевірка.** Відкрито seed-інцидент «Перевірка журналу комп'ютерного класу», опис якого містить текст `<script>`. Браузер показав цей фрагмент буквально, як текст. У `GetDetailsAsync` для публічного контракту використовуються DTO та виключення внутрішніх коментарів (`!comment.IsInternal`). У новій функції severity-summary DOM-вузли створюються за допомогою `document.createElement`, а текст вставляється через `textContent`.

**Спостереження.** Фрагмент `<script>` на сторінці не перетворився на активну HTML-розмітку. [Доказ](evidence/dom-safe-script.png).

**Результат.** Небезпечний `innerHTML` для виводу отриманого підсумку не використовується; автоматизований регресійний тест клієнтського коду також пройшов у складі чотирьох baseline-тестів. Це перевірка конкретного маршруту, а не доказ відсутності всіх XSS-ризиків у системі.

## 6. Висновок

У роботі налаштовано локальне середовище та запущено SecureLab із PostgreSQL у Docker. Простежено HTTP-обмін від браузерного клієнта через ASP.NET Core API, Entity Framework Core та PostgreSQL і назад. Перевірено успішне отримання деталей інциденту, обробку відсутнього ресурсу через 404 Problem Details та безпечне відображення недовіреного тексту.

Реалізовано `GET /api/incidents/severity-summary`, який замість початкового `501` повертає `200 OK` і масив підсумків Low=1, Medium=1, High=1. Додано кнопку для виклику endpoint із браузера, сценарій HTTP-перевірки та карту архітектури. Зафіксовано успішне збирання проєкту і виконання чотирьох автоматизованих тестів. До остаточного подання залишаються оформлення фінального Git-стану, повторні перевірки після злиття та внесення hash/способу передавання в розділ 1.

## 7. Декларація використання генеративного ШІ

Під час лабораторної роботи використано **ChatGPT (OpenAI)** для покрокового пояснення методичних рекомендацій, допомоги з налаштуванням SDK і Docker, аналізу помилок запуску, підказок щодо C#/JavaScript, планування перевірок та оформлення звіту. Рекомендовані фрагменти коду адаптовано до наданого starter-проєкту. Результати перевірено локально через `dotnet build`, `dotnet test`, DevTools Network, HTTP-запити та PostgreSQL. Фактичні значення й скриншоти взято з власного запуску SecureLab; результати, які ще не були перевірені (фінальний hash, Git bundle і запуск `.http`-клієнтом), окремо позначено.
