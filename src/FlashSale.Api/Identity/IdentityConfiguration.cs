using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FlashSale.Api.Data;
using FlashSale.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FlashSale.Api.Identity;

public sealed record RuntimeMode(bool IsDemo, string DatabaseProvider, string CacheProvider);
public sealed record DemoSession(string AccessToken, DateTimeOffset ExpiresAt, bool IsDemo, string DisplayName);

public static class IdentityConfiguration
{
    public const string DemoIssuer = "flashsale-demo-local";
    public const string DemoAudience = "flashsale-demo-browser";

    public static void AddIdentity(this IServiceCollection services, IConfiguration config, bool isDemo)
    {
        var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        if (isDemo)
        {
            var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
            services.AddSingleton(key);
            authentication.AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = DemoIssuer,
                    ValidateAudience = true, ValidAudience = DemoAudience,
                    ValidateLifetime = true, RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(10)
                };
            });
        }
        else
        {
            var authority = config["Authentication:Authority"];
            var audience = config["Authentication:Audience"];
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != "https" || string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("Configure an HTTPS Authentication:Authority and Authentication:Audience. Production authentication never falls back to demo identity.");
            authentication.AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    ValidateIssuerSigningKey = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        }
        services.AddAuthorization(options => options.AddPolicy("user", policy => policy.RequireAuthenticatedUser().RequireClaim("sub").RequireClaim("iss")));
        services.AddScoped<CurrentUser>();
    }

    public static void MapIdentityEndpoints(this WebApplication app, bool isDemo)
    {
        if (isDemo)
        {
            app.MapPost("/api/demo/session", (SymmetricSecurityKey key) =>
            {
                var expires = DateTimeOffset.UtcNow.AddHours(1);
                // The fixed local identity is shared by this demo; it is not an account system.
                var token = new JwtSecurityToken(DemoIssuer, DemoAudience,
                    [new Claim("sub", "demo-user"), new Claim("name", "Local demo user"), new Claim("demo", "true")],
                    expires: expires.UtcDateTime,
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
                return Results.Ok(new DemoSession(new JwtSecurityTokenHandler().WriteToken(token), expires, true, "Local demo user"));
            });
        }
        app.MapGet("/api/me", async (ClaimsPrincipal principal, CurrentUser users, CancellationToken ct) =>
        {
            var user = await users.GetOrCreateAsync(principal, ct);
            return Results.Ok(new { user.Id, IsDemo = isDemo, DisplayName = isDemo ? "Local demo user" : "Authenticated user" });
        }).RequireAuthorization("user");
    }
}

public sealed class CurrentUser(FlashSaleDbContext db, DataMutationLock mutationLock)
{
    public async Task<AppUser> GetOrCreateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        await mutationLock.Semaphore.WaitAsync(ct);
        try
        {
            var issuer = principal.FindFirstValue("iss") ?? throw new InvalidOperationException("A validated issuer is required.");
            var subject = principal.FindFirstValue("sub") ?? throw new InvalidOperationException("A validated subject is required.");
            var user = await db.Users.SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, ct);
            if (user is not null) return user;
            user = new AppUser { Issuer = issuer, Subject = subject };
            db.Users.Add(user);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(user).State = EntityState.Detached;
                user = await db.Users.SingleOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, ct);
                if (user is null) throw;
            }
            return user;
        }
        finally { mutationLock.Semaphore.Release(); }
    }
}
