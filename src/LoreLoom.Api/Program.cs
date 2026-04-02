using System.Text;
using LoreLoom.Api.Services;
using LoreLoom.Core.Data;
using LoreLoom.Core.Engine;
using LoreLoom.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var port = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(port, out var parsedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{parsedPort}");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

var sqliteConnectionString = new SqliteConnectionStringBuilder(connectionString);

if (!string.IsNullOrWhiteSpace(sqliteConnectionString.DataSource) && !Path.IsPathRooted(sqliteConnectionString.DataSource))
{
    var dataFileName = sqliteConnectionString.DataSource;
    var appServiceHome = Environment.GetEnvironmentVariable("HOME");

    if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(appServiceHome))
    {
        var persistentDataDirectory = Path.Combine(appServiceHome, "data");
        Directory.CreateDirectory(persistentDataDirectory);

        var deployedDatabasePath = Path.Combine(builder.Environment.ContentRootPath, dataFileName);
        var persistentDatabasePath = Path.Combine(persistentDataDirectory, dataFileName);

        if (!File.Exists(persistentDatabasePath) && File.Exists(deployedDatabasePath))
        {
            File.Copy(deployedDatabasePath, persistentDatabasePath);
        }

        sqliteConnectionString.DataSource = persistentDatabasePath;
    }
    else
    {
        sqliteConnectionString.DataSource = Path.Combine(builder.Environment.ContentRootPath, dataFileName);
    }
}

builder.Services.AddDbContext<LoreLoomDbContext>(options =>
    options.UseSqlite(sqliteConnectionString.ConnectionString));

builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.AddHttpClient<ILlmService, GroqLlmService>();
builder.Services.AddScoped<TurnManager>();

// JWT
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtService>();

// Email
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<IEmailService, EmailService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LoreLoomDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
