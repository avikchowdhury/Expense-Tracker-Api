using ExpenseTracker.Api.Configuration;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ExpenseTracker.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExpenseTrackerPlatform(
            this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(options =>
            {
                options.OperationFilter<FileUploadOperationFilter>();
            });

            services.AddDbContext<ExpenseTrackerDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                    "Server=(localdb)\\mssqllocaldb;Database=ExpenseTrackerDb;Trusted_Connection=True;MultipleActiveResultSets=true";
                options.UseSqlServer(connectionString);
            });
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpContextAccessor();

            services.Configure<FileStorageOptions>(configuration.GetSection("Storage"));
            var storageOptions = configuration.GetSection("Storage").Get<FileStorageOptions>() ?? new FileStorageOptions();
            var storageRootPath = Path.IsPathRooted(storageOptions.RootPath)
                ? storageOptions.RootPath
                : Path.GetFullPath(Path.Combine(environment.ContentRootPath, storageOptions.RootPath));

            var storagePaths = new FileStoragePaths
            {
                RootPath = storageRootPath,
                AvatarsPath = Path.Combine(storageRootPath, storageOptions.AvatarsFolder),
                ReceiptsPath = Path.Combine(storageRootPath, storageOptions.ReceiptsFolder),
                NotificationPreviewsPath = Path.Combine(storageRootPath, "notification-previews")
            };

            Directory.CreateDirectory(storagePaths.RootPath);
            Directory.CreateDirectory(storagePaths.AvatarsPath);
            Directory.CreateDirectory(storagePaths.ReceiptsPath);
            Directory.CreateDirectory(storagePaths.NotificationPreviewsPath);

            services.AddSingleton(storagePaths);

            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            var jwtConfig = configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
            var key = Encoding.UTF8.GetBytes(jwtConfig.Secret);

            services.AddAuthentication(options =>
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
            services.AddAuthorization();

            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAdminUserDeletionService, AdminUserDeletionService>();
            services.AddScoped<IUserRoleService, UserRoleService>();
            services.AddScoped<IBudgetHealthService, BudgetHealthService>();
            services.AddScoped<IBudgetAdvisorService, BudgetAdvisorService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IReceiptService, ReceiptService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<INotificationDigestService, NotificationDigestService>();
            services.AddHostedService<NotificationDigestBackgroundService>();
            services.AddHttpClient<IAIService, AIService>();
            services.AddMemoryCache();

            services.AddCors(options =>
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

            return services;
        }
    }
}
