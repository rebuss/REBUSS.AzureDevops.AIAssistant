using GitDaif.ServiceAPI;
using Microsoft.TeamFoundation.TestManagement.WebApi;
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
// Register DiffFileCleanerService with the diffFilesDirectory from configuration
builder.Services.AddScoped<DiffFileCleanerService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<DiffFileCleanerService>>();
    var diffFilesDirectory = configuration[ConfigConsts.DiffFilesDirectory];
    return new DiffFileCleanerService(diffFilesDirectory, logger);
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
