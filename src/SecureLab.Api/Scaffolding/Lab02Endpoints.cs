using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
    app.MapGet("/api/incidents/search", async (
        string? q,
        string? sortBy,
        SecureLabDbContext db,
        CancellationToken ct) =>
    {
        // Дозволені способи сортування
        if (sortBy is not (null or "" or
            "createdAtUtc" or "severity" or "status"))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["sortBy"] =
                    ["Допустимі значення: createdAtUtc, severity, status."]
                });
        }

        var query = db.Incidents.AsNoTracking();

        // Безпечний пошук за Title та Description
        if (!string.IsNullOrEmpty(q))
        {
            var escaped = q
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            var pattern = $"%{escaped}%";

            query = query.Where(incident =>
                EF.Functions.ILike(incident.Title, pattern, "\\") ||
                EF.Functions.ILike(incident.Description, pattern, "\\"));
        }

        // Сортування тільки за дозволеними полями
        var ordered = sortBy switch
        {
            "severity" => query.OrderBy(incident =>
                incident.Severity == IncidentSeverity.Critical ? 0 :
                incident.Severity == IncidentSeverity.High ? 1 :
                incident.Severity == IncidentSeverity.Medium ? 2 : 3),

            "status" => query.OrderBy(incident =>
                incident.Status == IncidentStatus.New ? 0 :
                incident.Status == IncidentStatus.Triaged ? 1 :
                incident.Status == IncidentStatus.InProgress ? 2 :
                incident.Status == IncidentStatus.Resolved ? 3 : 4),

            _ => query.OrderByDescending(
                incident => incident.CreatedAtUtc)
        };

        var rows = await ordered
            .ThenBy(incident => incident.Id)
            .Take(50)
            .ToListAsync(ct);

        return Results.Ok(rows.Select(incident => new
        {
            incident.Id,
            incident.Title,
            incident.Description,
            Severity = incident.Severity.ToString(),
            Status = incident.Status.ToString(),
            incident.CreatedAtUtc
        }));
    });

        app.MapPost("/api/incidents", async (
            CreateIncidentRequest request,
            SecureLabDbContext db,
            CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            var rawTitle = request.Title ?? "";
            var rawDescription = request.Description ?? "";

            var title = rawTitle.Trim();
            var description = rawDescription.Trim();

            // Перевірка назви
            if (title.Length == 0)
                errors["title"] = ["Назва обов'язкова."];
            else if (rawTitle.Length > 160)
                errors["title"] = ["Максимум 160 символів."];

            // Перевірка опису
            if (description.Length == 0)
                errors["description"] = ["Опис обов'язковий."];
            else if (rawDescription.Length > 4000)
                errors["description"] = ["Максимум 4000 символів."];

            // Перевірка рівня небезпеки
            var severityValid =
                Enum.TryParse<IncidentSeverity>(
                    request.Severity,
                    ignoreCase: true,
                    out var severity)
                && Enum.IsDefined(severity);

            if (!severityValid)
                errors["severity"] =
                    ["Допустимі значення: Low, Medium, High, Critical."];

            // Перевірка дати
            if (request.OccurredAtUtc is null)
                errors["occurredAtUtc"] = ["Дата обов'язкова."];
            else if (request.OccurredAtUtc > now.AddMinutes(5))
                errors["occurredAtUtc"] =
                    ["Дата не може бути більш ніж на 5 хвилин у майбутньому."];

            // Відхилення некоректного запиту
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Створення інциденту
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = DbSeeder.AliceId,
                Title = title,
                Description = description,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc
            );

            return Results.Created(
                $"/api/incidents/{incident.Id}",
                response);
});

    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc);
