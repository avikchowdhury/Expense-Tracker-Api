using ExpenseTracker.Api.Services;
using Microsoft.Extensions.FileProviders;

namespace ExpenseTracker.Api.Extensions
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseExpenseTrackerPlatform(this WebApplication app)
        {
            var storagePaths = app.Services.GetRequiredService<FileStoragePaths>();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "ExpenseTracker API v1");
                options.RoutePrefix = string.Empty;
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

            return app;
        }
    }
}
