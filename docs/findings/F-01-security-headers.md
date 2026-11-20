
# F-01. Відсутність захисних HTTP-заголовків у відповідях 500

**Статус:** Fixed
**Актив:** безпечна обробка HTTP-відповідей та захист браузерного клієнта.
**Компонент:** src/SecureLab.Api/Program.cs, ASP.NET Core middleware.
**Пріоритет:** P3.

## Передумови і межі огляду

- Review-start commit: `93907a2` (v0.5.0).
- Робоча гілка: `lab/6-hardening-review`.
- ОС: Windows.
- SDK: .NET 10.0.403.
- API: http://localhost:5080.
- База даних: локальна PostgreSQL у Docker Compose.
- Профіль перевірки: Production, режим broken.
- Навчальний користувач: Alice.
- Клієнт: VS Code REST Client.
- Маршрут: POST /api/auth/login.
- Команда відновлення навчальних даних у Development:
  `dotnet run --project src/SecureLab.Api -- --reset-database`.

Перевірка проводилася виключно на власному локальному навчальному стенді. Реальний зовнішній Production deployment не досліджувався.

## Відтворення

1. Зупинити API.
2. Запустити контрольований Production-профіль:

   `powershell -NoProfile -ExecutionPolicy Bypass -File ..\lr06-resources\commands\Production.ps1 -Mode broken`

3. Відкрити файл `..\lr06-resources\commands\unexpected-error.http`.
4. Виконати підготовлений POST-запит до `/api/auth/login` із синтетичними обліковими даними.
5. Перевірити статус, тіло та заголовки HTTP-відповіді.

Очікуваний результат: 500 Internal Server Error,
application/problem+json, безпечне повідомлення,
traceId та захисні HTTP-заголовки.

Фактичний результат до виправлення:

- HTTP 500 Internal Server Error.
- Content-Type: application/problem+json.
- traceId присутній.
- Внутрішній stack trace клієнту не повертається.
- X-Content-Type-Options відсутній.
- Referrer-Policy відсутній.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально.

Відсутність заголовків на error responses створює
непослідовність застосування браузерних захистів.

X-Content-Type-Options зменшує ризик MIME sniffing,
а Referrer-Policy обмежує передачу інформації
через Referer.

Сценарій можливий у разі необробленої серверної
помилки, зокрема недоступності бази даних.

Прямої експлуатації або витоку даних під час
перевірки не виявлено.

Пріоритет P3 обрано через обмежений підтверджений
вплив і необхідність виникнення серверної помилки.

## Першопричина

Middleware встановлював захисні заголовки після
UseExceptionHandler() та до завершення обробки
запиту.

Під час формування відповіді на необроблений
виняток ExceptionHandler очищував попередні
заголовки HTTP-відповіді.

У результаті остаточна відповідь 500
не містила необхідних заголовків.

## Виправлення

У Program.cs додано middleware перед
UseExceptionHandler(), який використовує
Response.OnStarting().

Безпосередньо перед надсиланням відповіді
встановлюються заголовки:

- X-Content-Type-Options: nosniff.
- Referrer-Policy: no-referrer.

Це забезпечує їх наявність також на відповідях,
сформованих обробником винятків.

Fixed commit: `7d24206`.

Commit message:
Fix security headers on exception responses.

## Retest

Повторно запущено Production-профіль у режимі
broken після виправлення Program.cs.

Виконано той самий запит із файлу
unexpected-error.http.

Фактичний результат:

- HTTP 500 Internal Server Error.
- Content-Type: application/problem+json.
- X-Content-Type-Options: nosniff.
- Referrer-Policy: no-referrer.
- traceId присутній.
- Stack trace та connection string у відповіді
  відсутні.

Результат: PASS.

## Positive regression

Після відновлення Development-профілю виконано
повторні перевірки.

| Сценарій | Результат | Захисні заголовки |
|---|---|---|
| GET /health | 200 OK | Присутні |
| GET власного інциденту | 200 OK | Присутні |
| GET із некоректним status | 400 Bad Request | Присутні |
| GET неіснуючого інциденту | 404 Not Found | Присутні |

Вхід Alice: 204 No Content.

Команда автоматичного тестування:

`dotnet test SecureLab.sln -c Release`

Безпосередньо після виправлення: 11/11 тестів
успішно, 0 помилок.

Після реалізації CSRF-захисту: 12/12 тестів
успішно, 0 помилок.

Результат: PASS.

## Залишковий ризик

Захисні HTTP-заголовки не замінюють
автентифікацію, авторизацію, CSRF-захист
та безпечну роботу з DOM.

Перевірка обмежена локальним HTTP-стендом.

Реальний HTTPS deployment, TLS termination
та HSTS на зовнішньому сервері не перевірялися.

Окремий автоматизований тест заголовків
на відповіді 500 не створювався.
Відповідний сценарій перевірено вручну.

## Доказ

Початковий TraceId:

`97bc4721d765600358109c3f83a77a17`

Збіг TraceId HTTP-відповіді та серверного журналу
підтверджено до виправлення.

Спостереження після виправлення:
відповідь 500 від 19.11.2026 містить
обидва захисні заголовки.

Перевірка змін:

`git diff 93907a2 7d24206 -- src/SecureLab.Api/Program.cs`

Перевірка звичайної відповіді:

`curl.exe -i http://localhost:5080/health`

Автоматичне тестування:

`dotnet test SecureLab.sln -c Release`

Докази не містять реальних паролів, cookie,
CSRF-токенів або секретів конфігурації.
