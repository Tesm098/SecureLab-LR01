
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using SecureLab.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync("alice", password);

        using var response =
            await client.GetAsync("/api/auth/session-check");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // A-01: анонімний користувач не може створити інцидент.
    [Fact]
    public async Task A01_AnonymousCreate_Returns401()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        var request = new
        {
            title = "Anonymous test incident",
            description = "Authentication security test",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        };

        using var response = await client.PostAsJsonAsync(
            "/api/incidents", request);

        // Сервер відхиляє запит без автентифікації.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        // API не перенаправляє на сторінку входу.
        Assert.Null(response.Headers.Location);

        // Не використовується Bearer-автентифікація.
        Assert.Empty(response.Headers.WwwAuthenticate);

        // Повертається API-помилка, а не HTML.
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    // A-02: після login повертається профіль
    // поточного автентифікованого користувача.
    [Fact]
    public async Task A02_Login_ReturnsOwnProfile()
    {
        var password =
            Environment.GetEnvironmentVariable(
                "SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Вхід Alice зі справжньою Identity cookie.
        using var client =
            await factory.LoginAsync("alice", password);

        // Запит до захищеного endpoint.
        using var response =
            await client.GetAsync("/api/me");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var profile = document.RootElement;

        // Сервер повертає саме Alice.
        Assert.Equal(
            DbSeeder.AliceId,
            profile.GetProperty("id").GetGuid());

        Assert.Equal(
            "alice",
            profile.GetProperty("userName").GetString());

        Assert.False(string.IsNullOrWhiteSpace(
            profile.GetProperty("displayName").GetString()));

        // У відповіді лише три дозволені поля.
        Assert.Equal(
            3,
            profile.EnumerateObject().Count());
    }

    // A-05: підроблений ownerUserId не змінює власника.
    [Fact]
    public async Task A05_ForgedOwner_RemainsAuthenticatedBob()
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Автентифікуємося як Bob.
        using var client =
            await factory.LoginAsync("bob", password);
        // Отримуємо CSRF-токен для автентифікованого Bob.
        await client.UseAntiforgeryTokenAsync();
        var request = new
        {
            title = "A05-" + Guid.NewGuid().ToString("N"),
            description = "Security test for incident ownership",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow,

            // Спроба підмінити власника на Alice.
            ownerUserId = DbSeeder.AliceId
        };

        using var response = await client.PostAsJsonAsync(
            "/api/incidents", request);

        // Інцидент повинен бути створений.
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        // Отримуємо ID створеного інциденту.
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var incidentId = document.RootElement
            .GetProperty("id").GetGuid();

        // Перевіряємо фактичного власника в базі даних.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        var storedIncident = await db.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == incidentId);

        Assert.NotNull(storedIncident);

        // Власник — Bob, а не підставлена Alice.
        Assert.Equal(
            DbSeeder.BobId,
            storedIncident!.OwnerUserId);

        Assert.NotEqual(
            DbSeeder.AliceId,
            storedIncident.OwnerUserId);
    }

}
