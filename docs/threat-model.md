
# Модель загроз SecureLab, ЛР 6

Рівень виконання: достатній («Задовільно»).
Спільний сценарій: 2-A.

| Елемент | Що зафіксувати | Ваш запис |
|---|---|---|
| Версія та середовище | Commit, профіль запуску, seed/reset, SDK | Review-start: 93907a2bd8aa200f47bb5eb8b7529f4c8ce9acc7 (v0.5.0). Windows, .NET SDK 10.0.403, Development і контрольований Production-профіль. Локальні навчальні дані відновлюються через --reset-database у Development. |
| Компоненти в межах огляду | Компоненти системи | Браузерний клієнт, ASP.NET Core API, EF Core, ASP.NET Core Identity, PostgreSQL у Docker Compose, Git-репозиторій, конфігурація та діагностичні журнали. |
| Активи | Дані та ресурси | Облікові дані, authentication cookie, CSRF-токени, інциденти користувачів, права доступу, конфігурація застосунку, цілісність бази даних. |
| Суб’єкти | Учасники системи | Анонімний відвідувач, автентифіковані користувачі Alice і Bob, користувачі з привілейованими ролями, локальний розробник. |
| Entry points | Точки входу | HTTP-маршрути API, login, register, logout, створення та пошук інцидентів, браузерні форми, конфігурація, відновлення NuGet-пакетів. |
| Межі довіри | Взаємодія компонентів | browser → API; API → PostgreSQL; repository → build; environment → configuration. |
| Наявні controls | Механізми захисту | Серверна валідація, параметризовані EF Core запити, Identity Cookie Authentication, перевірка власника ресурсу, безпечний DOM через textContent, Development-only CORS, Problem Details, захисні HTTP-заголовки та antiforgery validation. |
| Виключення | Межі огляду | Реальний зовнішній Production deployment, TLS termination, Redis, повний аудит залежностей, повне сканування історії Git, навантажувальне тестування. Додаткові перевірки вищих рівнів не виконувалися. |

## Схема меж довіри

    [Користувач]
          |
          v
    [Браузерний клієнт]
          |
          | HTTP / JSON / Cookie / CSRF
          | Межа довіри 1
          v
    [ASP.NET Core API] <--- [Змінні середовища]
          |                    Межа довіри 4
          | EF Core
          | Межа довіри 2
          v
    [PostgreSQL / Docker Compose]

    [Git-репозиторій]
          |
          | Restore / Build
          | Межа довіри 3
          v
    [Застосунок SecureLab]

## Очікувані властивості на межах

| Межа | Очікувана властивість (усі рівні) | Конкретний test або перевірка |
|---|---|---|
| browser → API | Недовірені дані перевіряються на сервері. Захищені операції потребують автентифікації та авторизації. Змінювальні cookie-authenticated маршрути потребують CSRF-токена. | A-02, A-03, A-05, REG-CSRF-01, REG-XSS-01. |
| API → PostgreSQL | Запити параметризовані. Користувацькі дані не змінюють структуру SQL-запиту. Власник інциденту визначається сервером. | Контрольований SQLi-запит повернув 200 і []. Тест A-05 підтвердив правильного власника. |
| repository → build | Відтворювана збірка, контроль версій пакетів, відсутність реальних секретів у відстежуваній конфігурації. | dotnet restore SecureLab.sln --locked-mode; dotnet build SecureLab.sln -c Release; git grep; git check-ignore. |
| environment → configuration | Реальні секрети не зберігаються в Git. Development і Production мають розмежовану конфігурацію. | Перевірка змінної SeedUsers__Password без виведення значення, .gitignore, Production reset rejection, контрольована відповідь 500. |

## Посилання на реалізовані controls

- Серверна валідація та пошук:
  src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs.

- Authentication та logout:
  src/SecureLab.Api/Scaffolding/Lab03Identity.cs.

- Авторизація доступу до інцидентів:
  src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs.

- Безпечне відображення даних:
  src/SecureLab.Api/Client/browser-lab.js.

- CORS, захисні заголовки та видача CSRF-токена:
  src/SecureLab.Api/Program.cs.

- Перевірка CSRF-токена:
  src/SecureLab.Api/Scaffolding/Lab06AntiforgeryFilter.cs.

- Клієнтський CSRF-helper:
  src/SecureLab.Api/Client/csrf-helper.js.

- Автоматична регресія CSRF:
  tests/SecureLab.Api.Tests/CsrfRegressionTests.cs.

## Межі висновків

Локальний HTTP-профіль не підтверджує безпечність
транспорту в реальному Production.

CSP не була реалізована в попередньому стані.
Для обраного рівня: N/A.

Login Rate Limiter відсутній.
Це зафіксовано як залишковий hardening gap.

CSRF-захист застосовано до POST /api/incidents
та POST /api/auth/logout.

Ризики login-CSRF і зловживання публічною
реєстрацією розглядаються окремо.

За результатами поточного тестування:
12 автоматичних тестів успішно, 0 помилок.

Перевірений commit реалізації: 59c8d2f.

Детальні результати та залишкові ризики наведено
в docs/security-review.md.
