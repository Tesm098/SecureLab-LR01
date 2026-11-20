using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using System.Security.Claims;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (
            RegistrationInput input,
            UserManager<ApplicationUser> users) =>
        {
            // Нормалізація лише профільних полів.
            var userName = input.UserName?.Trim() ?? "";
            var email = input.Email?.Trim() ?? "";
            var displayName = input.DisplayName?.Trim() ?? "";

            // Перевірки довжини та формату за контрактом.
            var validUserName =
                System.Text.RegularExpressions.Regex.IsMatch(
                    userName, @"\A[A-Za-z0-9._@+-]{3,256}\z");

            var validEmail =
                email.Length <= 256 &&
                System.Net.Mail.MailAddress.TryCreate(
                    email, out var parsedEmail) &&
                parsedEmail.Address.Equals(
                    email, StringComparison.OrdinalIgnoreCase);

            var validDisplayName =
                displayName.Length > 0 &&
                displayName.Length <= 120;

            var validPassword =
                !string.IsNullOrEmpty(input.Password);

            if (!validUserName || !validEmail ||
                !validDisplayName || !validPassword)
            {
                return Results.BadRequest(
                    new { message = "Registration failed" });
            }

            // Ідентифікатор та роль встановлює сервер.
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                DisplayName = displayName
            };

            // Стандартний Identity перевіряє пароль,
            // хешує його та зберігає користувача.
            var result = await users.CreateAsync(
                user, input.Password!);

            if (!result.Succeeded)
            {
                return Results.BadRequest(
                    new { message = "Registration failed" });
            }

            var role = await users.AddToRoleAsync(
                user, DbSeeder.ReporterRole);

            if (!role.Succeeded)
                throw new InvalidOperationException(
                    "Default role assignment failed.");

            // У відповіді немає пароля або полів Identity.
            var response = new RegistrationResponse(
                user.Id,
                user.UserName!,
                user.DisplayName
            );

            return Results.Created("/api/me", response);
        });

        app.MapPost("/api/auth/login", async (LoginInput input, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrEmpty(input.Password))
                return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(input.UserName, input.Password, false, false);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });
        app.MapPost("/api/auth/logout", async (
            SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .AddEndpointFilter<Lab06AntiforgeryFilter>();

        app.MapGet("/api/me", async (
            HttpContext context,
            UserManager<ApplicationUser> users) =>
        {
            // Отримуємо користувача лише з перевіреного principal.
            var user = await users.GetUserAsync(context.User);

            if (user is null)
                return Results.Unauthorized();

            // Повертаємо лише безпечні поля профілю.
            return Results.Ok(new
            {
                user.Id,
                user.UserName,
                user.DisplayName
            });
        })
        .RequireAuthorization();

        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }
}

public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);

public sealed record RegistrationResponse(
    Guid Id,
    string UserName,
    string DisplayName);
