using System;
using FirebirdWeb.Helpers;

using Microsoft.AspNetCore.Authentication.Cookies; // ✅ Cookie auth
using Microsoft.AspNetCore.Http;                   // ✅ SameSiteMode
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ✅ Enable Windows Service support
builder.Host.UseWindowsService();

// ----------------------------
// Services
// ----------------------------
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ✅ REQUIRED: Session needs a cache store
builder.Services.AddDistributedMemoryCache();

// ✅ Cookie Authentication (stay logged in)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";

        options.ExpireTimeSpan = TimeSpan.FromDays(36500);  // Effectively never expires (100 years)
        options.SlidingExpiration = false;                 // No auto-expiry, only logout

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // ✅ If your site ALWAYS uses HTTPS, uncomment:
        // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// ✅ Session (keep for OTP / your existing flow)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(36500); // Match cookie: 100 years
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // ✅ If your site ALWAYS uses HTTPS, uncomment:
    // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Helpers
builder.Services.AddSingleton<EmailHelper>();
builder.Services.AddSingleton<DbHelper>();
builder.Services.AddSingleton<ActivationState>();

// Keygen license validation
builder.Services.Configure<FirebirdWeb.Models.KeygenSettings>(
    builder.Configuration.GetSection("KeygenSettings"));
builder.Services.AddHttpClient<FirebirdWeb.Helpers.KeygenService>();

var app = builder.Build();
app.UseForwardedHeaders();

// ----------------------------
// Pipeline
// ----------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// ✅ IMPORTANT ORDER:
app.UseSession();          // keep for OTP

// Activation gate: require successful Keygen activation before entering app.
app.Use(async (context, next) =>
{
    var activationState = context.RequestServices.GetRequiredService<ActivationState>();
    var path = context.Request.Path;

    if (path.StartsWithSegments("/Activate", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var activated = string.Equals(
        context.Session.GetString("LicenseActivated"),
        "true",
        StringComparison.OrdinalIgnoreCase
    ) || activationState.IsActivated();

    if (activated)
    {
        await next();
        return;
    }

    if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"success\":false,\"message\":\"Activation required.\"}");
        return;
    }

    context.Response.Redirect("/Activate");
});

app.UseAuthentication();   // ✅ MUST be before authorization
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
