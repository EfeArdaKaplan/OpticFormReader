using System.Threading.RateLimiting;
using business.Services;
using data.Storage;
using WebApi.Middleware;
using QuestPDF.Infrastructure;
using core.interfaces;
using core.Interfaces;
using data.Context;
using data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

// Rate Limiting (API Security Best Practices: Brute-force & DDoS Koruması)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 1) Genel API Hız Sınırı (Dakikada 120 İstek)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 120,
                QueueLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    // 2) Auth Uç Noktaları İçin Sıkı Brute-Force Koruması (Dakikada 15 İstek)
    options.AddPolicy("AuthStrict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"auth_{httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon"}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 15,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("https://omr-frontend-pink.vercel.app", "http://localhost:5500", "http://127.0.0.1:5500", "http://localhost:5144")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// Repository ve Service DI kayıtları
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExamTemplateRepository, ExamTemplateRepository>();
builder.Services.AddScoped<IExamTemplateService, ExamTemplateService>();
builder.Services.AddScoped<FormGeneratorService>();
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IAnswerKeyRepository, AnswerKeyRepository>();
builder.Services.AddScoped<IAnswerKeyService, AnswerKeyService>();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
builder.Services.AddScoped<IUploadBatchRepository, UploadBatchRepository>();
builder.Services.AddScoped<IStudentSheetRepository, StudentSheetRepository>();
builder.Services.AddScoped<ISheetService, SheetService>();
builder.Services.AddSingleton<IOmrEngine, FastOmrEngine>();
builder.Services.AddScoped<IEvaluationNotificationService, WebApi.Services.EvaluationNotificationService>();
builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<IExportService, ExportService>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == "CHANGE_ME" || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key yapılandırılmamış veya 32 byte'tan kısa. user-secrets ya da Jwt__Key ortam değişkeni ile ayarlayın.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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

    // SignalR WebSocket bağlantılarında query string üzerinden JWT token okuma desteği
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/evaluation"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Login'den aldığın token'ı yapıştır (Bearer yazmana gerek yok)"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Otomatik Veritabanı Migration ve Yalnızca Hash'lenmemiş Kayıtların Dönüştürülmesi
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        for (int i = 0; i < 5; i++)
        {
            try
            {
                db.Database.Migrate();

                // Sadece veritabanında henüz '$h256$' ile başlamayan eski düz metin kayıt varsa dönüştür
                int unhashedUsers = db.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"Users\" WHERE \"Email\" NOT LIKE '$h256$%'")
                    .FirstOrDefault();

                int unhashedSheets = db.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"StudentSheets\" WHERE \"ImageKey\" <> '' AND \"ImageKey\" NOT LIKE '$h256$%'")
                    .FirstOrDefault();

                if (unhashedUsers > 0 || unhashedSheets > 0)
                {
                    foreach (var u in db.Users.ToList())
                    {
                        db.Entry(u).Property(x => x.FullName).IsModified = true;
                        db.Entry(u).Property(x => x.Email).IsModified = true;
                    }
                    foreach (var s in db.StudentSheets.ToList())
                    {
                        db.Entry(s).Property(x => x.StudentNumber).IsModified = true;
                        db.Entry(s).Property(x => x.FirstName).IsModified = true;
                        db.Entry(s).Property(x => x.LastName).IsModified = true;
                        db.Entry(s).Property(x => x.TcNo).IsModified = true;
                        db.Entry(s).Property(x => x.ImageKey).IsModified = true;
                    }
                    foreach (var b in db.UploadBatches.ToList())
                    {
                        db.Entry(b).Property(x => x.OriginalFileName).IsModified = true;
                        db.Entry(b).Property(x => x.FileKey).IsModified = true;
                    }
                    foreach (var t in db.ExamTemplates.ToList())
                    {
                        db.Entry(t).Property(x => x.InstitutionName).IsModified = true;
                    }
                    db.SaveChanges();
                }
                break;
            }
            catch when (i < 4)
            {
                Thread.Sleep(2000);
            }
        }
    }
    catch (Exception ex)
    {
        services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbMigration")
            .LogError(ex, "Veritabanı migration işlemi başarısız oldu.");
    }
}

// OWASP Security Headers (api-security-best-practices & security-and-hardening)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
    context.Response.Headers.Append("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UsePerformanceLogging();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("Frontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.MapHub<WebApi.Hubs.EvaluationHub>("/hubs/evaluation");

app.Run();
