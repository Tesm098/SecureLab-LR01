
# SecureLab — трекер інцидентів (ЛР 1–6)

Навчальний проєкт із дисципліни «Прикладні технології
програмування в інформаційній безпеці».

Автор: Кліванський Володимир, група КБ-42.
Спільний сценарій: 2-A.
Рівень виконання ЛР 6: достатній («Задовільно»).

Проєкт створено на основі SecureLab starter і послідовно
розвинено під час виконання лабораторних робіт №1–6.

SecureLab є трирівневим монолітним застосунком,
який використовує ASP.NET Core, Entity Framework Core,
PostgreSQL та Vanilla JavaScript.

## 1. Передумови

Для роботи із застосунком необхідні:

- .NET SDK 10 (фактично перевірено 10.0.403);
- Docker із підтримкою Docker Compose;
- Git;
- Visual Studio Code або інший редактор коду;
- сучасний веббраузер;
- вільні локальні порти 5080 і 54329;
- локально налаштована змінна SeedUsers__Password.

Вимоги до SDK визначаються файлом global.json.

Перевірка середовища:

```powershell
dotnet --version
docker --version
docker compose version
git --version
```

Реальні паролі та секрети необхідно передавати
через локальні змінні середовища поза Git.

## 2. Архітектура застосунку

Основний потік даних:

```text
Browser
   |
   | HTTP / JSON
   v
ASP.NET Core API
   |
   | Entity Framework Core
   v
PostgreSQL
```

Браузерний клієнт надсилає HTTP-запити до API.

Сервер перевіряє вхідні дані, автентифікацію,
авторизацію та CSRF-токени для захищених
змінювальних операцій.

Взаємодія з PostgreSQL здійснюється через EF Core.

Результати повертаються клієнту у форматі JSON
та відображаються через безпечні DOM sinks.

## 3. Підготовка середовища

У кореневому каталозі SecureLab виконайте:

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
```

Відновлення локальних інструментів:

```powershell
dotnet tool restore
```

Відновлення залежностей:

```powershell
dotnet restore SecureLab.sln --locked-mode
```

Збірка проєкту:

```powershell
dotnet build SecureLab.sln -c Release --no-restore
```

Перед запуском необхідно налаштувати змінну
SeedUsers__Password зі значенням навчального пароля.

Перевірка наявності змінної без розкриття значення:

```powershell
if ($env:SeedUsers__Password) {
    "Password configured"
} else {
    "Password missing"
}
```

## 4. Запуск застосунку

Запуск у Development:

```powershell
dotnet run --project src/SecureLab.Api
```

Під час першого Development-запуску застосунок
використовує наявні міграції EF Core та додає
початкові навчальні дані.

Основні адреси:

- Browser client: http://localhost:5080/
- Health check: http://localhost:5080/health
- Навчальний вхід: http://localhost:5080/auth-lab.html
- Картка інциденту: http://localhost:5080/browser-lab.html
- Scalar: http://localhost:5080/scalar/v1
- OpenAPI JSON: http://localhost:5080/openapi/v1.json

Scalar та OpenAPI доступні лише в Development.

Перевірка працездатності API:

```powershell
curl.exe -i http://localhost:5080/health
```

Очікувана відповідь:

```json
{"status":"ready"}
```

Для зупинення API використовуйте Ctrl+C.

Для зупинення контейнера PostgreSQL:

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml down
```

## 5. Відомий початковий стан

Навчальна база даних містить фіксованих користувачів:

- Alice;
- Bob;
- Morgan (Analyst);
- Admin.

Також передбачені навчальні інциденти,
коментарі та історія зміни статусів.

GUID початкових інцидентів Alice та Bob:

```text
Alice: 20000000-0000-0000-0000-000000000001
Bob:   20000000-0000-0000-0000-000000000002
```

Початкові навчальні ідентифікатори використовуються
у ручних HTTP-сценаріях та автоматичних тестах.

Локальні демонстраційні credentials PostgreSQL
призначені виключно для навчального стенду.

Їх заборонено повторно використовувати для
реального Production-середовища.

Реальний connection string необхідно передавати
поза Git через ConnectionStrings__SecureLab.

### Відновлення навчальних даних

Перед reset необхідно зупинити API.

```powershell
dotnet run --project src/SecureLab.Api -- --reset-database
```

Команда очищує відомі навчальні таблиці
та відновлює початковий seed.

Reset дозволено лише у відповідно налаштованому
Development-середовищі.

У Production виконання reset заборонено.

## 6. Виконані лабораторні роботи

### ЛР 1. Архітектура та базова функціональність

Реалізовано:

- отримання списку інцидентів;
- отримання деталей інциденту;
- фільтрацію за статусом;
- підсумок за рівнем небезпеки;
- взаємодію браузера, API та PostgreSQL;
- відтворювану збірку і запуск.

Маршрут підсумку:

```text
GET /api/incidents/severity-summary
```

### ЛР 2. Серверна валідація та SQL Injection

Реалізовано:

- контракт створення інциденту;
- серверну перевірку вхідних даних;
- обмеження довжини полів;
- перевірку severity та дати;
- параметризований пошук через EF Core;
- обмежений перелік допустимих параметрів сортування.

Основні маршрути:

```text
POST /api/incidents
GET /api/incidents/search
```

Контрольований SQL Injection сценарій
не змінює логіку SQL-запиту.

### ЛР 3. Автентифікація

Реалізовано ASP.NET Core Identity
Cookie Authentication.

Основні маршрути:

```text
POST /api/auth/register
POST /api/auth/login
GET /api/me
GET /api/auth/session-check
```

Облікові дані перевіряються стандартними
механізмами ASP.NET Core Identity.

Паролі, хеші та внутрішні поля Identity
не повертаються через DTO профілю.

Анонімний запит до захищеного ресурсу
повертає 401 Unauthorized.

### ЛР 4. Авторизація та IDOR

Реалізовано серверну перевірку прав доступу
до окремих інцидентів.

Власник може отримати власний інцидент.

Спроба Alice отримати деталі інциденту Bob
повертає 404 Not Found.

Власник нового інциденту визначається
на сервері за автентифікованим principal.

Передане клієнтом поле ownerUserId
не використовується для визначення власника.

### ЛР 5. Безпека браузера та XSS

Реалізовано:

- безпечне відображення Incident.Description;
- захист від stored XSS;
- використання безпечних DOM sinks;
- Development-only CORS allowlist;
- регресійні браузерні перевірки.

Дозволений CORS origin:

```text
http://localhost:4173
```

Контрольний недозволений origin:

```text
http://localhost:4174
```

CORS-політика не замінює автентифікацію,
авторизацію або CSRF-захист.

### ЛР 6. Hardening та Security Review

Проведено підсумковий огляд безпеки SecureLab.

Виконано:

- аналіз моделі загроз;
- огляд конфігурації та секретів;
- перевірку Development і Production;
- контрольоване відтворення серверної помилки;
- виправлення захисних HTTP-заголовків;
- реалізацію antiforgery validation;
- реалізацію захищеного logout;
- адаптацію попередніх тестів;
- створення автоматичного REG-CSRF-01;
- повторну перевірку SQLi, Authentication,
  Authorization, IDOR, XSS та CSRF;
- оформлення підсумкової документації.

## 7. CSRF-захист

Для захисту змінювальних маршрутів використовується
стандартний ASP.NET Core Antiforgery.

Видача токена:

```text
GET /api/auth/csrf-token
```

Маршрут доступний автентифікованому користувачу.

Назва HTTP-заголовка:

```text
X-SecureLab-CSRF
```

Захищені змінювальні маршрути:

```text
POST /api/incidents
POST /api/auth/logout
```

Запит без валідної пари antiforgery cookie
та request token відхиляється.

Неавтентифікований запит повертає 401.

Автентифікований запит без валідного CSRF-токена
повертає 400 Bad Request.

Перевірка токена відбувається до створення
інциденту або виконання SignOutAsync().

Основні файли реалізації:

```text
src/SecureLab.Api/Program.cs
src/SecureLab.Api/Scaffolding/Lab06AntiforgeryFilter.cs
src/SecureLab.Api/Client/csrf-helper.js
src/SecureLab.Api/Client/browser-lab.js
src/SecureLab.Api/Client/auth-lab.js
```

Клієнтський helper зберігає токен лише
в оперативній пам'яті поточної сторінки.

Після успішного входу або виходу кеш токена
на сторінці автентифікації очищається.

Значення cookie, токенів і паролів
не повинні потрапляти в Git або звіти.

## 8. Захисні HTTP-заголовки

У застосунку використовуються:

```text
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

Заголовки встановлюються через Response.OnStarting()
у middleware перед UseExceptionHandler().

Це дозволяє зберігати їх також на відповідях,
сформованих під час обробки необроблених винятків.

Фактично перевірено відповіді:

- 200 OK;
- 400 Bad Request;
- 401 Unauthorized;
- 404 Not Found;
- 500 Internal Server Error.

Для обробки помилок використовується Problem Details.

Під час контрольованої Production-помилки
внутрішній stack trace не повертається клієнту.

## 9. Автоматичне тестування

Перед запуском тестів необхідно зупинити API.
PostgreSQL має залишатися запущеним.

Повна збірка:

```powershell
dotnet build SecureLab.sln -c Release
```

Запуск усіх тестів:

```powershell
dotnet test SecureLab.sln -c Release
```

Останній перевірений результат робочого
стану ЛР 6:

```text
Total: 12
Succeeded: 12
Failed: 0
Skipped: 0
```

Окремий регресійний тест CSRF:

```powershell
dotnet test tests/SecureLab.Api.Tests -c Release --filter "FullyQualifiedName~REG_CSRF_01"
```

Тест REG-CSRF-01 перевіряє:

1. Автентифікацію Alice.
2. Початкову відсутність тестового інциденту.
3. Відмову POST без CSRF-токена.
4. Незмінність даних після відмови.
5. Отримання валідного токена.
6. Успішне створення інциденту.
7. Фактичну наявність запису в PostgreSQL.
8. Правильного власника інциденту.

Результат фінального прогону на прийнятому
merge commit фіксується в експортованому
Security Review.

## 10. Ручні HTTP-перевірки

Перевірка API:

```powershell
curl.exe -i http://localhost:5080/health
```

Перевірка анонімного доступу:

```powershell
curl.exe -i http://localhost:5080/api/me
```

Очікується 401 Unauthorized.

Перевірка пошуку без збігів:

```powershell
curl.exe -i "http://localhost:5080/api/incidents/search?q=zz-no-match"
```

Очікується 200 OK та порожній масив.

Додаткові HTTP-сценарії розташовані в:

```text
tests/http/
```

Браузерні перевірки виконуються через
auth-lab.html та browser-lab.html.

## 11. Документація лабораторних робіт

Документація проєкту розташована в каталозі docs.

### Попередні лабораторні роботи

- [Архітектура проєкту](docs/architecture.md)
- [Звіт ЛР 2](docs/report-lr02.md)
- [Звіт ЛР 3](docs/report-lr03.md)
- [Звіт ЛР 4](docs/report-lab-04.md)
- [Матриця доступу](docs/access-matrix.md)
- [Звіт ЛР 5](docs/report-lab-05.md)
- [Browser Security Evidence](docs/browser-security-evidence.md)
- [Source-to-sink map](docs/source-to-sink.md)

### Лабораторна робота №6

- [Security Review](docs/security-review.md)
- [Модель загроз](docs/threat-model.md)
- [Finding F-01 — Security Headers](docs/findings/F-01-security-headers.md)

Підсумковий звіт ЛР 6 містить фактичні
результати перевірок, межі огляду,
регресійні сценарії та залишкові ризики.

## 12. Структура проєкту

```text
SecureLab/
  src/
    SecureLab.Api/
      Application/
      Client/
      Data/
      Presentation/
      Scaffolding/
      Properties/
      Program.cs

  tests/
    SecureLab.Api.Tests/
    http/

  infra/
    compose.yaml
    .env.example

  docs/
    architecture.md
    access-matrix.md
    browser-security-evidence.md
    source-to-sink.md
    security-review.md
    threat-model.md

    findings/
      F-01-security-headers.md

  demonstrations/
    cors-probe/

  SecureLab.sln
  global.json
  README.md
```

Каталоги відображають відповідальність компонентів
одного серверного застосунку.

Проєкт не використовує мікросервісну архітектуру.

## 13. Версіонування Git

Проєкт розвивався послідовно через окремі
гілки лабораторних робіт.

| Версія | Лабораторна робота |
|---|---|
| v0.1.0 | ЛР 1 — базова функціональність |
| v0.2.0 | ЛР 2 — validation та SQLi |
| v0.3.0 | ЛР 3 — authentication |
| v0.4.0 | ЛР 4 — authorization |
| v0.5.0 | ЛР 5 — browser security |
| v1.0.0 | ЛР 6 — фінальний перевірений стан |

Робоча гілка ЛР 6:

```text
lab/6-hardening-review
```

Початковий commit огляду:

```text
93907a2bd8aa200f47bb5eb8b7529f4c8ce9acc7
```

Основні коміти ЛР 6:

- 7d24206 — Fix security headers on exception responses.
- 59c8d2f — Implement CSRF protection and regression tests.

Тег v1.0.0 створюється після злиття
lab/6-hardening-review з main та успішного
фінального тестування прийнятого merge commit.

## 14. Типові проблеми запуску

| Ознака | Рішення |
|---|---|
| PostgreSQL connection refused | Перевірити Docker Compose, порт 54329 та healthcheck |
| SDK не знайдено | Перевірити dotnet --version та global.json |
| Порт 5080 зайнятий | Зупинити попередній екземпляр API |
| Login не працює | Перевірити SeedUsers__Password та початкові навчальні дані |
| API повертає 401 | Виконати автентифікацію |
| POST повертає 400 через CSRF | Отримати валідний токен поточного сеансу |
| Logout повертає 400 | Перевірити підключення csrf-helper.js |
| JavaScript не оновився | Виконати Ctrl+F5 |
| Reset у Production відхилено | Використовувати лише дозволений Development-профіль |
| Тести не проходять | Зупинити API, перевірити PostgreSQL та конфігурацію |

## 15. Межі безпеки та залишкові ризики

SecureLab є локальним навчальним застосунком.

Виконані перевірки не підтверджують повну
відсутність усіх можливих вразливостей.

Для обраного рівня виконання залишаються:

- відсутність Login Rate Limiter;
- відсутність CSP;
- неперевірений зовнішній HTTPS deployment;
- неперевірена реальна конфігурація TLS/HSTS;
- необхідність окремого аналізу login-CSRF;
- необхідність окремого аналізу зловживання
  публічною реєстрацією;
- відсутність повного аудиту залежностей;
- обмежений огляд історії Git.

Додаткові перевірки доброго та відмінного
рівнів не заявляються як виконані.

## 16. Правила безпечної роботи

Усі контрольовані перевірки проводяться
виключно на власному локальному стенді.

Перед створенням commit необхідно перевірити:

```powershell
git status
git diff --check
git diff --staged
```

До Git не повинні потрапляти:

- .env із реальними секретами;
- реальні паролі;
- authentication cookies;
- CSRF-токени;
- API keys;
- приватні ключі;
- локальні connection strings із секретами;
- дампи бази даних;
- необроблені журнали;
- приватні конфігураційні файли.

Навчальну історію з уразливими станами
не слід публікувати у відкритому доступі.

Проєкт призначений виключно для навчання
та дослідження механізмів інформаційної безпеки.
