using System.Text;
using CVAPI.Repos;
using CVAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);

// Tilføj logging
builder.Logging.AddConsole();

// Tilføj tjenester til containeren
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Hent connection string
var connectionString = builder.Configuration.GetConnectionString("CosmosDB");

// Tilføj CosmosDB klienten som en service
builder.Services.AddSingleton<CosmosClient>(sp => new CosmosClient(connectionString));

// Registrer repositorier og services
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ExperienceRepository>();
builder.Services.AddScoped<CompetenciesRepository>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<OneTimeLinkService>();

// Configure SMTP client
builder.Services.Configure<SmtpClient>(options => {
    var emailConfig = builder.Configuration.GetSection("Email");
    options.Host = emailConfig["SmtpServer"];
    options.Port = int.Parse(emailConfig["Port"]);
    options.Credentials = new NetworkCredential(
        emailConfig["Username"],
        emailConfig["Password"]
    );
    options.EnableSsl = bool.Parse(emailConfig["EnableSsl"]);
});

// Tilføj controllers
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// Konfigurer JWT Authentication
ConfigureJwtAuthentication(builder);

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Adjust timeout as needed
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Add middleware to extract JWT from Authorization header for page requests
app.Use(async (context, next) =>
{
    // For page requests (not API), check if there's an Authorization header
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            // Token is already in Authorization header - no need to modify
            Console.WriteLine($"[DEBUG] Page request with Authorization header: {context.Request.Path}");
        }
    }
    await next();
});

// Add before app.UseHttpsRedirection();
app.UseCors(builder =>
{
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader()
           .WithExposedHeaders("Authorization");
});

// Konfigurer HTTP-request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Ensure static files are served properly in all environments
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // Add cache control headers for static files
        context.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseSession(); // Add this before app.UseAuthorization()
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Health check endpoint for operational tests
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
}));

app.Run();

// Metode til at konfigurere JWT Authentication
static void ConfigureJwtAuthentication(WebApplicationBuilder builder)
{
    var jwtKey = builder.Configuration["JwtSettings:SecretKey"];
    var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];

    if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32 || string.IsNullOrEmpty(jwtIssuer))
    {
        throw new InvalidOperationException("JWT secrets are missing or invalid.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero // Reduce clock skew to zero for testing
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    Console.WriteLine($"[Debug] Full Auth Header: {authHeader}");
                    
                    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    {
                        Console.WriteLine("[Debug] No valid Authorization header found");
                        return Task.CompletedTask;
                    }

                    var token = authHeader.Substring("Bearer ".Length);
                    context.Token = token;
                    
                    Console.WriteLine($"[Debug] Extracted token: {token.Substring(0, Math.Min(token.Length, 20))}...");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine("[Debug] Token successfully validated");
                    Console.WriteLine(
                        $"[Debug] Claims: {string.Join(", ", context.Principal.Claims.Select(c => $"{c.Type}: {c.Value}"))}"
                    );
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine(
                        $"[Debug] Authentication failed: {context.Exception.GetType().Name}"
                    );
                    Console.WriteLine($"[Debug] Error details: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine("[Debug] Challenge issued");
                    Console.WriteLine(
                        $"[Debug] Error: {context.Error}, Description: {context.ErrorDescription}"
                    );
                    return Task.CompletedTask;
                },
            };
        });
}
