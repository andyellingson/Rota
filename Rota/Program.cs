using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Rota.Components;
using Rota.Endpoints;
using Rota.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Bind MongoDB options from configuration
builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection("MongoDb"));

// Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.HttpContextAccessor>();
builder.Services.AddScoped<IUserService, MongoUserService>();
builder.Services.AddScoped<IRemindersService, MongoRemindersService>();
builder.Services.AddScoped<IShiftsService, MongoShiftsService>();
builder.Services.AddScoped<IAbsencesService, MongoAbsencesService>();
builder.Services.AddScoped<AuthenticationStateProvider, HttpContextAuthenticationStateProvider>();

// Enable detailed circuit errors for debugging (remove or set false in production)
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Auth endpoints used by the Blazor login component
app.MapAuthEndpoints();

// Reminders endpoints used by the calendar component
app.MapRemindersEndpoints();
// Shifts endpoints used by calendar component for creating/viewing shifts
app.MapShiftsEndpoints();
// Absences endpoints used by calendar component
app.MapAbsencesEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

try
{
    // Run the application. Surround with try/catch to log fatal errors during startup or runtime.
    app.Run();
}
catch (Exception ex)
{
    // Log unexpected critical failures and rethrow so the host terminates with failure.
    app.Logger.LogCritical(ex, "Host terminated unexpectedly");
    throw;
}
