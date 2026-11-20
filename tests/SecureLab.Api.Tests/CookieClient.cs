using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public static class CookieClient
{
    public static async Task<HttpClient> LoginAsync(this SecureLabApiFactory factory, string userName, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        if (!response.IsSuccessStatusCode)
        {
            client.Dispose();
            throw new InvalidOperationException("Study login failed (credentials omitted).");
        }
        return client;
    }
        // ЛР №6. Отримання CSRF-токена для тестового cookie-сеансу.
    public static async Task UseAntiforgeryTokenAsync(
        this HttpClient client)
    {
        const string headerName = "X-SecureLab-CSRF";

        client.DefaultRequestHeaders.Remove(headerName);

        using var response =
            await client.GetAsync("/api/auth/csrf-token");

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        if (string.IsNullOrWhiteSpace(result?.Token))
            throw new InvalidOperationException(
                "CSRF token was not received.");

        client.DefaultRequestHeaders.Add(
            headerName, result.Token);
    }

    private sealed record CsrfTokenResponse(string Token);
}
