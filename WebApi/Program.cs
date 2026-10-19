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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("https://omr-frontend-pink.vercel.app", "http://localhost:5500", "http://127.0.0.1:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // SignalR için gerekli
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
var jwtKey = builder.Configuration["Jwt:Key"]!;
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

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

// Otomatik Veritabani Migration (Docker ve yerel baslangic icin)
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
        Console.WriteLine($"DB Migration uyarisi: {ex.Message}");
    }
}

// OWASP Security Headers (api-security-best-practices & security-and-hardening)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UsePerformanceLogging();

if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.MapHub<WebApi.Hubs.EvaluationHub>("/hubs/evaluation");

app.Run();