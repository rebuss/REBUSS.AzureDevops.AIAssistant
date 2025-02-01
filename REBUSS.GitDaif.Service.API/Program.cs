using GitDaif.ServiceAPI;
using Microsoft.Extensions.Options;
using REBUSS.GitDaif.Service.API;
using REBUSS.GitDaif.Service.API.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.Configure<AppSettings>(builder.Configuration);
builder.Services.AddHostedService<DiffFileCleanerBackgroundService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<DiffFileCleanerBackgroundService>>();
    var appSettings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
    return new DiffFileCleanerBackgroundService(appSettings.DiffFilesDirectory, logger);
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<GitService>();
var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
