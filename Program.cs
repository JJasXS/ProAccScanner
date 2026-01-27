using FirebirdWeb; // Your project namespace if needed
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// ===============================
//  Add services to the container
// ===============================

// Razor Pages support
builder.Services.AddRazorPages();

// API Controllers support
builder.Services.AddControllers();

// Register DbHelper as a singleton for DI
builder.Services.AddSingleton<DbHelper>();

// Optional: Add HttpClient for future REST API calls
builder.Services.AddHttpClient();

var app = builder.Build();

// ===============================
//  Configure the HTTP request pipeline
// ===============================

// Developer exception page in development
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();

// Serve static files (wwwroot)
app.UseStaticFiles();

app.UseRouting();

// Authorization middleware (not needed if no auth yet)
app.UseAuthorization();

// Map Controllers (API endpoints)
app.MapControllers();

// Map Razor Pages
app.MapRazorPages();

// Optional: default route redirect to Index page
app.MapGet("/", context =>
{
    context.Response.Redirect("/Index");
    return Task.CompletedTask;
});

// Run the app
app.Run();