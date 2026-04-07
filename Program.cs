using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Extensions;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.Text;

var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(webRootPath);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = webRootPath
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<ExpenseTracker.Api.Extensions.FileUploadOperationFilter>();
});

builder.Services.AddDbContext<ExpenseTrackerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=(localdb)\\mssqllocaldb;Database=ExpenseTrackerDb;Trusted_Connection=True;MultipleActiveResultSets=true";
    options.UseSqlServer(connectionString);
});

builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("Storage"));

var storageOptions = builder.Configuration.GetSection("Storage").Get<FileStorageOptions>() ?? new FileStorageOptions();
var storageRootPath = Path.IsPathRooted(storageOptions.RootPath)
    ? storageOptions.RootPath
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, storageOptions.RootPath));
var storagePaths = new FileStoragePaths
{
    RootPath = storageRootPath,
    AvatarsPath = Path.Combine(storageRootPath, storageOptions.AvatarsFolder),
    ReceiptsPath = Path.Combine(storageRootPath, storageOptions.ReceiptsFolder)
};

Directory.CreateDirectory(storagePaths.RootPath);
Directory.CreateDirectory(storagePaths.AvatarsPath);
Directory.CreateDirectory(storagePaths.ReceiptsPath);

builder.Services.AddSingleton(storagePaths);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

var jwtConfig = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
var key = Encoding.UTF8.GetBytes(jwtConfig.Secret ?? "supersecretkey1234567890");

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
        ValidIssuer = jwtConfig.Issuer,
        ValidAudience = jwtConfig.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddHttpClient<IAIService, AIService>();
builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ExpenseTracker API v1");
    options.RoutePrefix = string.Empty; // Swagger at root
});

app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePaths.AvatarsPath),
    RequestPath = "/avatars"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public class JwtSettings
{
    public string Secret { get; set; } = "supersecretkey1234567890";
    public string Issuer { get; set; } = "ExpenseTracker";
    public string Audience { get; set; } = "ExpenseTrackerUsers";
    public int ExpiryMinutes { get; set; } = 60;
}
