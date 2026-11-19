using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetList_ReturnsSeededIncidents()
    {
        var incidents = await _client.GetFromJsonAsync<List<IncidentListItemResponse>>(
            "/api/incidents");

        Assert.NotNull(incidents);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.AliceIncidentId);
        Assert.Contains(incidents, incident => incident.Id == DbSeeder.BobIncidentId);
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        var password = Environment.GetEnvironmentVariable(
            "SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Входимо як Alice.
        using var client = await factory.LoginAsync("alice", password);

        // Запитуємо неіснуючий інцидент.
        using var response = await client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetDetails_DoesNotExposeInternalOwnerFields()
    {
        var password = Environment.GetEnvironmentVariable(
            "SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Автентифікуємося як власник інциденту.
        using var client = await factory.LoginAsync("alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.False(
            document.RootElement.TryGetProperty("ownerUserId", out _));

        Assert.False(
            document.RootElement.TryGetProperty("email", out _));

        Assert.Equal(
            "Аліса Коваль",
            document.RootElement
                .GetProperty("ownerDisplayName").GetString());
    }


    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        var script = await _client.GetStringAsync("/app.js");

        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
        Assert.Contains("textContent", script, StringComparison.Ordinal);
    }

    // ЛР №4, A-02: власник читає свій інцидент.
    [Fact]
    public async Task A02_Owner_CanReadOwnIncident()
    {
        var password = Environment.GetEnvironmentVariable(
            "SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        using var client = await factory.LoginAsync(
            "alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            DbSeeder.AliceIncidentId,
            document.RootElement.GetProperty("id").GetGuid());
    }

    // ЛР №4, A-03: Reporter не читає чужий інцидент.
    [Fact]
    public async Task A03_NonOwner_CannotReadForeignIncident()
    {
        var password = Environment.GetEnvironmentVariable(
            "SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Alice намагається прочитати інцидент Bob.
        using var client = await factory.LoginAsync(
            "alice", password);

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        // У відповіді не повинно бути чужих даних.
        Assert.DoesNotContain(
            DbSeeder.BobIncidentId.ToString(), body);

        Assert.DoesNotContain(
            "Боб Мельник", body);
    }

}
