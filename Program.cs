using ExpenseTracker.Api.Configuration;
using ExpenseTracker.Api.Extensions;

var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(webRootPath);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = webRootPath
});

builder.Services.AddExpenseTrackerPlatform(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExpenseTrackerPlatform();

app.Run();
