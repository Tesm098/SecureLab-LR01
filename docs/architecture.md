# Карта архітектури SecureLab

**Лабораторна робота №1:** «Запуск, дослідження та невелике розширення готової вебсистеми»

**Baseline:** 2-A «Трекер інцидентів»
**Робоча гілка:** `lab/1-system`
**Кінцевий тег:** `v0.1.0`

## 1. Компоненти системи

Система SecureLab складається з браузерного клієнта, серверного застосунку ASP.NET Core Web API та бази даних PostgreSQL, яка працює в контейнері Docker.

| Компонент | Розташування | Призначення |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Формує HTTP-запити та відображає відповіді |
| Presentation | `src/SecureLab.Api/Presentation/` | Реєструє endpoints, обробляє параметри та формує HTTP-відповіді |
| Application | `src/SecureLab.Api/Application/` | Реалізує логіку отримання, фільтрації та групування інцидентів |
| Data / EF Core | `src/SecureLab.Api/Data/` | Забезпечує доступ до PostgreSQL |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані про інциденти |

## 2. Дослідження маршруту запиту

Під час лабораторної роботи досліджено запит:

`GET /api/incidents/20000000-0000-0000-0000-000000000003`

Маршрут проходить через такі компоненти:

```text
Браузер
  ↓
Client/app.js → loadIncidentDetails()
  ↓
GET /api/incidents/{id}
  ↓
IncidentEndpoints.GetDetailsAsync()
  ↓
IncidentQueries.GetDetailsAsync()
  ↓
SecureLabDbContext.Incidents
  ↓
PostgreSQL → таблиця incidents
  ↓
IncidentDetailsResponse
  ↓
JSON-відповідь API
  ↓
Client/app.js → renderIncidentDetails()
  ↓
DOM → textContent
```

У DevTools Network зафіксовано відповідь `200 OK` з типом вмісту `application/json`.

Також перевірено запит до неіснуючого інциденту з ID `aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa`. Сервер повернув `404 Not Found` у форматі `application/problem+json`.

У PostgreSQL перевірено існування запису інциденту за його ідентифікатором.

## 3. Реалізоване розширення severity-summary

У межах лабораторної роботи реалізовано endpoint:

`GET /api/incidents/severity-summary`

**Призначення:** підрахунок кількості інцидентів за рівнями небезпеки.

### Маршрут нової функції

```text
Кнопка «Показати підсумок»
  ↓
Client/index.html
  ↓
Client/app.js → loadSeveritySummary()
  ↓
GET /api/incidents/severity-summary
  ↓
IncidentEndpoints.GetSeveritySummaryAsync()
  ↓
IncidentQueries.GetSeveritySummaryAsync()
  ↓
SecureLabDbContext.Incidents
  ↓
PostgreSQL → таблиця incidents
  ↓
IncidentSeveritySummaryResponse
  ↓
JSON
  ↓
Client/app.js → textContent
  ↓
Відображення результату на сторінці
```

### Змінені файли

- `Presentation/Contracts/IncidentResponses.cs` — додано DTO `IncidentSeveritySummaryResponse`.
- `Application/Incidents/IncidentQueries.cs` — реалізовано групування та підрахунок інцидентів.
- `Presentation/Endpoints/IncidentEndpoints.cs` — підключено новий endpoint.
- `Client/index.html` — додано кнопку та область відображення результату.
- `Client/app.js` — реалізовано виклик API та безпечне відображення.
- `tests/http/incidents.http` — оновлено сценарій T-06.

### Контракт API

- Метод: `GET`.
- Тіло запиту: відсутнє.
- Успішна відповідь: `200 OK`.
- Тип відповіді: `application/json`.
- Поля елемента відповіді: `severity`, `count`.

Використовується політика **лише наявних груп**. Категорії, для яких у базі немає інцидентів, не повертаються. Якщо таблиця порожня, результатом є порожній масив `[]`.

Порядок результатів визначається числовим порядком значень `IncidentSeverity`: від Low до Critical.

Фактично отримано:

```json
[
  {"severity": "Low", "count": 1},
  {"severity": "Medium", "count": 1},
  {"severity": "High", "count": 1}
]
```

Категорія Critical відсутня, оскільки інцидентів цього рівня немає.

Для реалізації використано `AsNoTracking()`, `GroupBy()`, `Count()` та `ToListAsync()`.

## 4. Межі довіри

| Межа довіри | Потенційна загроза | Захист |
|---|---|---|
| Користувач → Browser client | Користувач може змінювати введені дані | Елементи форми допомагають формувати правильні запити, але не замінюють серверну перевірку |
| Browser client → API | HTTP-параметри можна змінити поза інтерфейсом | Перевірка `status` у GetListAsync; обмеження `:guid` для маршруту деталей |
| API → PostgreSQL | Некоректні параметри запиту, небажана обробка даних | EF Core, LINQ, параметризовані запити та явна проєкція |
| PostgreSQL → API | Розкриття зайвих або внутрішніх полів | Response DTO обмежує дані, які повертає API |
| API → DOM | Виконання небезпечного HTML або JavaScript | `textContent`, `createElement()` та `document.createTextNode()` |

Під час перевірки деталей інциденту фрагмент `<script>` відображався як звичайний текст, а не виконувався браузером.

Нова функція severity-summary також виводить результат через `textContent`, не використовуючи `innerHTML`.

## 5. Конфігураційні входи

- `global.json` — визначає версію .NET SDK. Для виконання роботи налаштовано стабільну SDK сімейства .NET 10 з `rollForward: latestFeature`.
- `src/SecureLab.Api/appsettings.json` — основні налаштування застосунку.
- `src/SecureLab.Api/appsettings.Development.json` — локальна конфігурація середовища розробки.
- `infra/compose.yaml` — конфігурація PostgreSQL у Docker Compose.
- `infra/.env.example` — приклад налаштування локального порту PostgreSQL.
- `ConnectionStrings__SecureLab` — змінна середовища для перевизначення рядка підключення до бази даних.

Локальний порт PostgreSQL — `54329`, API — `5080`.

Реальні паролі, токени та приватні рядки підключення не включаються до документа.

## 6. Відновлення початкових даних

Для відновлення навчального стану використовується команда:

```powershell
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
```

Команда очищає навчальні таблиці та повторно заповнює їх початковими seed-даними.

Після відновлення підтверджено наявність трьох інцидентів на головній сторінці.

Команду дозволено використовувати лише для локальної навчальної бази даних SecureLab.

## 7. Результати перевірки

У процесі роботи отримано такі результати:

| Перевірка | Фактичний результат |
|---|---|
| PostgreSQL | Healthy |
| GET /health | 200 OK, status: ready |
| GET /api/incidents | 200 OK |
| GET /api/incidents/{id} | 200 OK |
| GET /api/incidents/{невідомий id} | 404 Not Found |
| Severity-summary до реалізації | 501 Not Implemented |
| Severity-summary після реалізації | 200 OK |
| Безпечне відображення в DOM | Успішно |
| Компіляція Release | Build succeeded |
| Автоматизовані тести | 4 успішно, 0 помилок |

## 8. Висновок

У лабораторній роботі досліджено архітектуру SecureLab та маршрут HTTP-запиту від браузера до PostgreSQL і назад.

Реалізовано нову функцію підрахунку інцидентів за рівнями небезпеки. Функція доступна через API та браузерний інтерфейс.

Перевірено коректність HTTP-відповідей, безпечне відображення даних, компіляцію проєкту та проходження чотирьох автоматизованих тестів.

Отримані результати підтверджують виконання основного програмного завдання лабораторної роботи №1.