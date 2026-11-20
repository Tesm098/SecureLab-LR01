
# Security review SecureLab, ЛР 6

Автор: Кліванський Володимир, КБ-42.
Спільний сценарій: 2-A.
Рівень: достатній («Задовільно»).
Дата виконання: 19–20.11.2026.

Review-start:
93907a2bd8aa200f47bb5eb8b7529f4c8ce9acc7.

Початковий тег: v0.5.0.
Робоча гілка: lab/6-hardening-review.
SDK: .NET 10.0.403.
ОС: Windows.
Браузер: Brave.
Інструменти: VS Code, PowerShell, REST Client,
DevTools, Docker Compose, Git, xUnit.

Хеш прийнятого merge commit, результат фінального
прогону та тег v1.0.0 додаються під час експорту PDF.

## 1. Executive summary

Проведено огляд безпеки навчального застосунку
SecureLab на основі результатів ЛР 1–5.

Перевірено конфігурацію, обробку серверних помилок,
захисні HTTP-заголовки, автентифікацію, авторизацію,
SQL Injection, XSS та CSRF.

Виявлено й виправлено відсутність захисних
HTTP-заголовків у відповідях 500. Реалізовано
antiforgery validation для створення інцидентів
і виходу із системи.

Після реалізації змін успішно пройдено
12 автоматичних тестів із 12.

Залишкові ризики: відсутність обмеження спроб
входу, неперевірений зовнішній HTTPS deployment,
ризики публічних маршрутів автентифікації.

## 2. Scope та exclusions

До меж огляду входять:

- ASP.NET Core API.
- Браузерний клієнт SecureLab.
- PostgreSQL у Docker Compose.
- ASP.NET Core Identity.
- EF Core.
- Поточна відстежувана конфігурація.
- HTTP-відповіді та діагностичні повідомлення.
- Автоматичні й ручні регресійні перевірки.

Історію Git переглянуто для таких файлів:

- infra/compose.yaml;
- src/SecureLab.Api/appsettings.Development.json;
- src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs.

Перевірялися коміти, що змінювали зазначені файли.
Повний аудит вмісту всієї історії Git не проводився.

Виключення:

- зовнішній Production deployment;
- Redis;
- повний аудит залежностей;
- повний аудит журналів;
- навантажувальне тестування;
- додаткові вимоги доброго та відмінного рівнів.

## 3. System overview і trust boundaries

Архітектура SecureLab:

Browser → ASP.NET Core API → EF Core → PostgreSQL.

Автентифікація реалізована через ASP.NET Core
Identity Cookie Authentication.

Основні межі довіри:

- browser → API;
- API → PostgreSQL;
- repository → build;
- environment → configuration.

Детальна модель загроз, активи, суб'єкти,
entry points та схема меж довіри:

[Модель загроз](threat-model.md).

## 4. Methodology та test environment

Огляд розпочато з тегу v0.5.0,
commit 93907a2.

Команди підготовки середовища:

    dotnet restore SecureLab.sln --locked-mode
    dotnet build SecureLab.sln -c Release --no-restore
    docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait

Відновлення локальних навчальних даних:

    dotnet run --project src/SecureLab.Api -- --reset-database

Запуск Development:

    dotnet run --project src/SecureLab.Api

API origin: http://localhost:5080.

PostgreSQL працює локально в Docker Compose.

Для перевірки Production використовувалися
надані методичкою скрипти Production.ps1
у режимах normal та broken.

Перевірка доступності:

    curl.exe -i http://localhost:5080/health

Результат для нормального стану:
200 OK, {"status":"ready"}.

Початкове автоматичне тестування:
11 успішних тестів, 0 помилок.

Після реалізації змін:
12 успішних тестів, 0 помилок.

Перевірений commit реалізації: 59c8d2f.

Остаточний прогін на прийнятому merge commit
буде зафіксований під час експорту звіту.

## 5. Findings summary

| ID | Актив | Вплив | Ймовірність | Пріоритет | Статус | Retest |
|---|---|---|---|---|---|---|
| F-01 | Захисні властивості HTTP-відповідей | Непослідовний захист браузера | Можлива за наявності серверної помилки | P3 | Fixed | Повторний Production/broken HTTP 500 |

Пріоритет визначено якісно, без вигаданого
CVSS score.

Виявлену прогалину виправлено та перевірено.
Додаткові finding cards для вищих рівнів
не оформлювалися.

## 6. Detailed findings

### F-01. Відсутність захисних заголовків на 500

До виправлення відповідь 500 не містила
X-Content-Type-Options та Referrer-Policy.

Причина: встановлення заголовків до завершення
обробки відповіді ExceptionHandler middleware.

Виправлення: реєстрація Response.OnStarting()
у middleware перед UseExceptionHandler().

Fixed commit: 7d24206.

Повторний контрольований HTTP 500 містить:

- X-Content-Type-Options: nosniff;
- Referrer-Policy: no-referrer;
- application/problem+json;
- traceId.

Positive regression:
відповіді 200, 400 і 404 продовжують
працювати з потрібними заголовками.

Детальна картка:

[F-01](findings/F-01-security-headers.md).

## 7. Regression results

Результати практичних перевірок достатнього рівня
на робочому стані ЛР 6.

| ID | Property | Передумови | Негативний сценарій | Позитивний сценарій | Фактичний результат |
|---|---|---|---|---|---|
| S-02 | SQL Injection | Локальна БД, параметризований пошук | Контрольований SQLi-рядок | Пошук створеного запису за назвою | 200, порожній масив для SQLi; звичайний пошук працює |
| T-04 | Безпечний пошук | Пошуковий endpoint ЛР 2 | Рядок без збігів | Пошук навчального інциденту | Очікувана поведінка |
| A-02 | Авторизація власника | Сеанс Alice | — | Читання власного інциденту | 200 OK |
| A-03 | Захист від IDOR | Сеанс Alice | Читання інциденту Bob | Читання власного інциденту | 404 для чужого, 200 для власного |
| A-05 | Власник ресурсу | Сеанс Bob і CSRF-токен | Підміна ownerUserId | Створення ресурсу | 201, власник Bob |
| Authentication | Identity lifecycle | Навчальний користувач | Анонімне читання профілю та інциденту | Вхід, профіль, вихід | 401; 204, 200, 204 |
| REG-XSS-01 | Безпечний DOM | Сеанс Alice | Навчальний XSS-маркер | Складний текст із Unicode та HTML-like рядком | Маркер не виконується, звичайний текст збережено |
| REG-CSRF-01 | Antiforgery | Автентифікований cookie-сеанс | POST без request token | POST із валідним токеном | 400 без створення запису; 201 із записом у БД |

### Докази регресії

SQLi:

    curl.exe -i "http://localhost:5080/api/incidents/search?q=zz-no-match"

    curl.exe -i "http://localhost:5080/api/incidents/search?q=zz-no-match%27%20OR%20TRUE%20--%20"

Обидві відповіді: 200 OK, [].

Authentication:

    curl.exe -i http://localhost:5080/api/me

Анонімний запит: 401 Unauthorized.

Авторизація:

- AliceIncidentId: 20000000-0000-0000-0000-000000000001.
- BobIncidentId: 20000000-0000-0000-0000-000000000002.
- Alice читає власний запис: 200.
- Alice читає чужий запис: 404.

XSS:

На сторінці browser-lab.html створено інцидент
із маркером із lab05-marker.txt.

Маркер відображається як звичайний текст.
Атрибут data-lab05-marker відсутній (null).

Окремо перевірено апострофи, лапки, українські
символи, Unicode, знаки порівняння та HTML-like
рядок. Дані відображаються коректно.

Під час перевірки виявлено JavaScript TypeError
через використання event.currentTarget після await.
Помилку виправлено збереженням посилання на форму
до асинхронної операції. Повторний тест успішний.

### Автоматизований REG-CSRF-01

Файл:
tests/SecureLab.Api.Tests/CsrfRegressionTests.cs.

Тест:
REG_CSRF_01_RejectsWithoutToken_AndAcceptsValidToken.

Команда:

    dotnet test tests/SecureLab.Api.Tests -c Release --filter "FullyQualifiedName~REG_CSRF_01"

Фактичний результат: 1/1 PASS.

Тест перевіряє:

1. Вхід Alice через реальний cookie flow.
2. Початкову відсутність тестового запису.
3. POST без CSRF-токена.
4. Відповідь 400 із повідомленням CSRF-помилки.
5. Незмінність кількості відповідних записів у БД.
6. Отримання валідного antiforgery token.
7. POST із токеном: 201 Created.
8. Наявність запису в PostgreSQL.
9. Правильного власника створеного інциденту.

### Перевірка logout

Вхід Alice: 204.
Читання профілю: 200.
Logout із валідним токеном: 204.
Читання профілю після виходу: 401.

Негативна перевірка:

- profileBefore: 200;
- logoutWithoutToken: 400;
- profileAfter: 200.

Відхилений logout не завершує сеанс.

### Загальний результат тестування

    dotnet test SecureLab.sln -c Release

Результат останнього робочого прогону:

- total: 12;
- succeeded: 12;
- failed: 0;
- skipped: 0.

Додатковий regression pack доброго рівня:
не входить до обраного рівня.

Остаточний прогін на merge commit ще не виконано.

## 8. Hardening controls

| Control | Ризик | Фактична перевірка | Межа |
|---|---|---|---|
| Parameterized LINQ / EF Core | SQL Injection | S-02, 200 і [] | Перевірений пошуковий сценарій |
| Server validation | Некоректні дані | 400 для status=Unknown | Локальний API |
| Identity Cookie Authentication | Неавтентифікований доступ | 401 без сеансу, 204 login | Локальний cookie profile |
| Resource authorization | IDOR | A-02, A-03, A-05 | Перевірені навчальні ресурси |
| textContent / safe DOM | Stored XSS | REG-XSS-01 | Основне поле Description |
| Antiforgery | CSRF | REG-CSRF-01, logout retest | Cookie-authenticated mutation routes |
| Security headers | MIME sniffing, referrer disclosure | F-01, 200/400/404/500 | Перевірені HTTP-відповіді |
| Problem Details | Розкриття внутрішніх деталей | Production/broken 500 | Контрольована локальна помилка |
| Development-only CORS | Небажаний cross-origin доступ | Наявна exact-origin policy | Повний REG-CORS-01 не повторювався |

### Інвентаризація змінювальних маршрутів

| ID | Method + route | Джерело / розбіжність | Зміна стану | Cookie authentication | Browser-accessible | Antiforgery validation | Regression ID |
|---|---|---|---|---|---|---|---|
| SC-01 | POST /api/incidents | ЛР 2–5, browser-lab.js | Створення інциденту | Так | Так | explicit | REG-CSRF-01, A-05 |
| SC-02 | POST /api/auth/logout | auth-lab.js, раніше 501 | Завершення сеансу | Так | Так | explicit | Ручний logout retest |
| SC-03 | POST /api/auth/login | ЛР 3, auth-lab.js | Створення сеансу | Ні | Так | absent | Login 204 |
| SC-04 | POST /api/auth/register | ЛР 3, API route | Створення акаунта | Ні | Так | absent | Реалізація перевірена за кодом |

Пошук маршрутів:

    git grep -n -E '\.Map(Post|Put|Patch|Delete)\(' -- src/SecureLab.Api

Перевірка підключення фільтра:

    git grep -n 'AddEndpointFilter<Lab06AntiforgeryFilter>' -- src/SecureLab.Api

Спільний antiforgery filter застосовується
до POST /api/incidents та POST /api/auth/logout.

Валідація відбувається до зміни даних або
виконання SignOutAsync().

Login та register не довіряють наявній
authentication cookie для виконання операції.
Ризики login-CSRF та зловживання реєстрацією
залишаються предметом окремого аналізу.

### Cookie profile

Authentication cookie:

- SameSite: Lax (за кодом конфігурації).
- SecurePolicy: SameAsRequest у Development.
- SecurePolicy: Always у Production.

Antiforgery cookie у локальному Development:

- HttpOnly: true;
- SameSite: Strict;
- Secure: false;
- Session cookie.

Назва request header:
X-SecureLab-CSRF.

Значення cookie й токенів не записуються
до звіту.

### Security headers

X-Content-Type-Options: nosniff.
Referrer-Policy: no-referrer.

Перевірено через curl, DevTools та REST Client.

Development:
200, 400, 404 — заголовки присутні.

Production/broken:
500 — після виправлення заголовки присутні.

CSP: N/A, раніше не реалізована.

### HTTPS/HSTS

launchSettings.json містить локальний HTTP-профіль
http://localhost:5080.

UseHttpsRedirection(), UseHsts() та
UseForwardedHeaders() у Program.cs не виявлено.

Локальний HTTP не підтверджує безпечність
реального HTTPS deployment.

TLS termination і зовнішні HSTS responses
не тестувалися.

### Rate limiting

За результатами пошуку в коді:

- AddRateLimiter не виявлено;
- UseRateLimiter не виявлено;
- RequireRateLimiting не виявлено.

Виклик PasswordSignInAsync використовує
lockoutOnFailure: false.

Login Rate Limiter відсутній.

Це залишкова прогалина захисту від масових
спроб входу. Новий limiter не додавався,
оскільки це не є обов'язковим для обраного рівня.

Quota, 429 та recovery не тестувалися.

## 9. Dependency and secret audit

Перевірено поточні відстежувані файли
конфігурації:

- infra/.env.example;
- infra/compose.yaml;
- appsettings.json;
- appsettings.Development.json.

Команда пошуку:

    git grep -n -I -i -E 'password|passwd|secret|api.?key' -- .

Початковий пошук повернув 77 рядків збігів.

Категорії виявлених secret-like збігів:

| Категорія | Приклад місця | Класифікація |
|---|---|---|
| Навчальні credentials | README.md, локальна конфігурація PostgreSQL | Відкриті демонстраційні значення для локального стенду, не Production secrets |
| Назви змінних | SeedUsers__Password | Назва параметра, не його значення |
| Поля Identity | PasswordHash, password | Назви полів моделі та DTO |
| Документація | Попередні звіти | Опис механізмів захисту |
| Правила захисту Git | .gitignore | Назви типів секретних файлів |

Окремо перевірено git check-ignore для infra/.env
та appsettings.Local.json.

Обидва шляхи ігноруються Git.

Реальний пароль навчального користувача
передається через змінну середовища.
Його значення не виводилося під час перевірки
наявності змінної.

Наведена класифікація описує категорії збігів.
Повний систематичний аудит усіх tracked files
не заявляється.

У разі реального витоку простого видалення
секрету з поточного файла недостатньо:
необхідно відкликати або замінити секрет
та оцінити його присутність в історії Git.

Restore у locked mode та збірку виконано успішно.

Повний dependency audit, advisory decision table,
систематичний log/audit review:
не входить до обраного рівня.

## 10. Residual risks

| Ризик | Стан | Наступна дія |
|---|---|---|
| Масові спроби входу | Login limiter відсутній | Запровадити обмеження в наступному hardening циклі |
| Login-CSRF | Окремо не тестувався | Оцінити та реалізувати відповідні controls |
| Зловживання публічною реєстрацією | Не тестувалося | Оцінити abuse protection |
| HTTPS та HSTS | Зовнішній deployment не перевірено | Перевірити реальну TLS-конфігурацію перед розгортанням |
| CSP | Відсутня | Розглянути впровадження окремо |
| Залежності | Повний аудит поза рівнем | Провести dependency audit |
| Історія Git | Перевірено лише окремі шляхи | Розширити огляд за потреби |

Відповідальний за наступні дії:
розробник SecureLab.

Дані ризики не позначено як PASS.

## 11. Conclusion

Проведено локальний security review SecureLab.

Виправлено прогалину захисних HTTP-заголовків
на відповідях 500.

Реалізовано спільний antiforgery flow:
видача токена, передача request header,
серверна валідація та захищений logout.

Перевірено SQLi, authentication, authorization,
IDOR, XSS та CSRF.

На робочому стані всі 12 автоматичних тестів
пройшли успішно.

Результат фінального прогону та хеш
прийнятого merge commit додаються під час
експорту PDF після фактичної перевірки.

## 12. Evidence appendix

Основні документи:

- docs/threat-model.md;
- docs/findings/F-01-security-headers.md.

Основні коміти:

- 93907a2 — початковий review-start.
- 7d24206 — виправлення security headers.
- 59c8d2f — CSRF protection and regression tests.

### Контрольована Production-помилка

Файл перевірки:
../lr06-resources/commands/unexpected-error.http.

Маршрут:
POST /api/auth/login.

Результат: 500 Internal Server Error.

Content-Type: application/problem+json.

Початковий TraceId:
97bc4721d765600358109c3f83a77a17.

Ідентифікатор зіставлено із серверним журналом.

Відповідь клієнту не містила stack trace,
SQL, connection string або внутрішніх шляхів.

### Production normal

GET /health: 200 OK.

GET /openapi/v1.json: 404 Not Found.

GET /scalar/v1: 200 text/html.
Тіло відповіді містить HTML основного клієнта,
а не інтерактивну документацію Scalar.
Це результат SPA fallback.

Спроба --reset-database у Production:
відхилена винятком із повідомленням,
що reset дозволено лише в Development.

### Перевірки Git

    git diff --check
    git diff --cached --check
    git status --short

На етапах створення комітів помилок
перевірки diff не виявлено.

Фінальні Git hashes і результати прийнятого
merge commit додаються при експорті PDF.

## Декларація й журнал ШІ

Використовував генеративний ШІ: ChatGPT.

ШІ використовувався для пояснення методичних
рекомендацій, підготовки фрагментів коду,
аналізу помилок та структурування документації.

Код було інтегровано у власний SecureLab,
перевірено збіркою, ручними HTTP-запитами,
браузерними сценаріями й автоматичними тестами.

| Дата | Сервіс | Завдання/фрагмент | Авторська адаптація | Перевірка й результат |
|---|---|---|---|---|
| 19.11.2026 | ChatGPT | Аналіз Production errors | Застосовано на власному стенді | 500, traceId |
| 19.11.2026 | ChatGPT | Виправлення middleware | Зміни Program.cs | Retest 500, 11/11 тестів |
| 19.11.2026 | ChatGPT | Antiforgery flow | Інтеграція у власні API та клієнт | Ручні перевірки CSRF |
| 19–20.11.2026 | ChatGPT | Регресійний тест | Використано наявну тестову фабрику | 12/12 тестів |
| 20.11.2026 | ChatGPT | Документація | Заповнення за фактичними результатами | Перевірка відповідності методичці |

Власна інтерпретація:
автентифікація встановлює особу, авторизація
визначає права, antiforgery захищає змінювальні
cookie-authenticated операції, а безпечний DOM
не дозволяє інтерпретувати недовірений опис як код.

Результати непроведених перевірок не заявлено.
