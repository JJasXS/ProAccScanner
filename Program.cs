using System;
using FirebirdWeb.Helpers;

using Microsoft.AspNetCore.Authentication.Cookies; // ✅ Cookie auth
using Microsoft.AspNetCore.Http;                   // ✅ SameSiteMode

var builder = WebApplication.CreateBuilder(args);

// ----------------------------
// Services
// ----------------------------
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// ✅ REQUIRED: Session needs a cache store
builder.Services.AddDistributedMemoryCache();

// ✅ Cookie Authentication (stay logged in)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";

        options.ExpireTimeSpan = TimeSpan.FromDays(30);  // ✅ stay login 30 days
        options.SlidingExpiration = true;                // ✅ extend when active

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // ✅ If your site ALWAYS uses HTTPS, uncomment:
        // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// ✅ Session (keep for OTP / your existing flow)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

    // ✅ If your site ALWAYS uses HTTPS, uncomment:
    // options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Helpers
builder.Services.AddSingleton<EmailHelper>();
builder.Services.AddSingleton<DbHelper>();

var app = builder.Build();

// ----------------------------
// Pipeline
// ----------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ IMPORTANT ORDER:
app.UseSession();          // keep for OTP
app.UseAuthentication();   // ✅ MUST be before authorization
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
