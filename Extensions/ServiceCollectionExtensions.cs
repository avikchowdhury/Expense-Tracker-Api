using ExpenseTracker.Api.Configuration;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Services;
using ExpenseTracker.Shared.Constants;
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
                var connectionString = configuration.GetConnectionString(ApplicationText.Configuration.DefaultConnectionName);
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        $"Connection string '{ApplicationText.Configuration.DefaultConnectionName}' is missing from configuration.");
                }

                options.UseSqlServer(connectionString);
            });
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserDigestRepository, UserDigestRepository>();
            services.AddScoped<IUserRoleRepository, UserRoleRepository>();
            services.AddScoped<IAdminUserDeletionRepository, AdminUserDeletionRepository>();
            services.AddHttpContextAccessor();

            services.Configure<FileStorageOptions>(configuration.GetSection(ApplicationText.Configuration.StorageSection));
            var storageOptions = configuration.GetSection(ApplicationText.Configuration.StorageSection).Get<FileStorageOptions>() ?? new FileStorageOptions();
            var storageRootPath = Path.IsPathRooted(storageOptions.RootPath)
                ? storageOptions.RootPath
                : Path.GetFullPath(Path.Combine(environment.ContentRootPath, storageOptions.RootPath));

            var storagePaths = new FileStoragePaths
            {
                RootPath = storageRootPath,
                AvatarsPath = Path.Combine(storageRootPath, storageOptions.AvatarsFolder),
                ReceiptsPath = Path.Combine(storageRootPath, storageOptions.ReceiptsFolder),
                NotificationPreviewsPath = Path.Combine(storageRootPath, ApplicationText.Storage.NotificationPreviewFolder)
            };

            Directory.CreateDirectory(storagePaths.RootPath);
            Directory.CreateDirectory(storagePaths.AvatarsPath);
            Directory.CreateDirectory(storagePaths.ReceiptsPath);
            Directory.CreateDirectory(storagePaths.NotificationPreviewsPath);

            services.AddSingleton(storagePaths);

            services.Configure<JwtSettings>(configuration.GetSection(ApplicationText.Configuration.JwtSettingsSection));
            var jwtConfig = configuration.GetSection(ApplicationText.Configuration.JwtSettingsSection).Get<JwtSettings>() ?? new JwtSettings();
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
            services.AddScoped<IPasswordHashService, PasswordHashService>();
            services.AddScoped<IAvatarStorageService, AvatarStorageService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminUserDeletionService, AdminUserDeletionService>();
            services.AddScoped<IUserRoleService, UserRoleService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IBudgetHealthService, BudgetHealthService>();
            services.AddScoped<IBudgetAdvisorService, BudgetAdvisorService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IReceiptService, ReceiptService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddSingleton<INotificationDigestService, NotificationDigestService>();
            services.AddHostedService<NotificationDigestBackgroundService>();
            services.AddScoped<IAIInsightsService, AIInsightsService>();
            services.AddScoped<IAISpendingAnalysisService, AISpendingAnalysisService>();
            services.AddScoped<IAINotificationService, AINotificationService>();
            services.AddScoped<IAIService, AIService>();
            services.AddHttpClient<IAIModelClient, AIModelClient>();
            services.AddHttpClient<IAIExpenseTextParser, AIExpenseTextParser>();
            services.AddHttpClient<IAIReceiptVisionParser, AIReceiptVisionParser>();
            services.AddMemoryCache();

            services.AddCors(options =>
            {
                options.AddPolicy(ApplicationText.Policies.AllowLocalhost, policy =>
                {
                    policy
                        .WithOrigins(ApplicationText.Policies.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            return services;
        }
    }
}
