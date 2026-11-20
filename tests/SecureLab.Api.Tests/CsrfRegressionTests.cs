
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using Xunit;

namespace SecureLab.Api.Tests;

public sealed class CsrfRegressionTests(
    SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task REG_CSRF_01_RejectsWithoutToken_AndAcceptsValidToken()
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Один автентифікований cookie-сеанс Alice.
        using var client =
            await factory.LoginAsync("alice", password);

        var title = "REG-CSRF-01-" + Guid.NewGuid().ToString("N");

        var request = new
        {
            title,
            description = "CSRF regression test",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        };

        // Перевіряємо фактичну кількість записів у БД.
        async Task<int> CountIncidentsAsync()
        {
            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            return await db.Incidents
                .AsNoTracking()
                .CountAsync(i => i.Title == title);
        }

        // 1. Початковий стан: запису немає.
        Assert.Equal(0, await CountIncidentsAsync());

        // 2. POST без CSRF-токена.
        using var rejected = await client.PostAsJsonAsync(
            "/api/incidents", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            rejected.StatusCode);

        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers.ContentType?.MediaType);

        // Перевіряємо саме CSRF-відмову, а не помилку DTO.
        using var rejectedBody = JsonDocument.Parse(
            await rejected.Content.ReadAsStringAsync());

        Assert.Equal(
            "CSRF token is missing or invalid.",
            rejectedBody.RootElement
                .GetProperty("title").GetString());

        // 3. Після відхиленого POST стан не змінився.
        Assert.Equal(0, await CountIncidentsAsync());

        // 4. Отримуємо токен тієї самої cookie-сесії.
        await client.UseAntiforgeryTokenAsync();

        // 5. Повторюємо POST з валідним токеном.
        using var accepted = await client.PostAsJsonAsync(
            "/api/incidents", request);

        Assert.Equal(
            HttpStatusCode.Created,
            accepted.StatusCode);

        using var acceptedBody = JsonDocument.Parse(
            await accepted.Content.ReadAsStringAsync());

        var incidentId = acceptedBody.RootElement
            .GetProperty("id").GetGuid();

        // 6. Перевіряємо створений запис у базі даних.
        using var verifyScope = factory.Services.CreateScope();

        var verifyDb = verifyScope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        var stored = await verifyDb.Incidents
            .AsNoTracking()
            .SingleAsync(i => i.Id == incidentId);

        Assert.Equal(title, stored.Title);
        Assert.Equal(DbSeeder.AliceId, stored.OwnerUserId);

        Assert.Equal(
            1,
            await CountIncidentsAsync());
    }
}
