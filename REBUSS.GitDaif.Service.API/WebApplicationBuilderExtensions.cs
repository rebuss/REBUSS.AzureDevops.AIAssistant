using GitDaif.ServiceAPI.Agents;
using GitDaif.ServiceAPI;
using REBUSS.GitDaif.Service.API.Agents;
using REBUSS.GitDaif.Service.API.Properties;
using Serilog;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using REBUSS.GitDaif.Service.API.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace REBUSS.GitDaif.Service.API
{
    public static class WebApplicationBuilderExtensions
    {
        public static WebApplicationBuilder SetupLogging(this WebApplicationBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();
            return builder;
        }

        public static WebApplicationBuilder SetupConfiguration(this WebApplicationBuilder builder)
        {
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables();
            builder.Services.Configure<AppSettings>(builder.Configuration);
            builder.Services.Configure<CopilotSettings>(builder.Configuration.GetSection(ConfigConsts.MicrosoftCopilot));
            builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection(ConfigConsts.OpenAI));
            
            return builder;
        }

        public static WebApplicationBuilder SetupServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddControllers();
            builder.Services.AddHostedService<DiffFileCleanerBackgroundService>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var logger = provider.GetRequiredService<ILogger<DiffFileCleanerBackgroundService>>();
                var appSettings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
                return new DiffFileCleanerBackgroundService(appSettings.DiffFilesDirectory, logger);
            });
            AppSettings appSettings = builder.Configuration.Get<AppSettings>();
            switch (appSettings.AIAgent)
            {
                case ConfigConsts.MicrosoftCopilot:
                    builder.Services.AddScoped<InterfaceAI, BrowserCopilotForEnterprise>();
                    break;
                case ConfigConsts.OpenAI:
                    var settings = builder.Configuration.GetSection(ConfigConsts.OpenAI).Get<OpenAISettings>();
                    var kernel = GetKernel(settings);
                    builder.Services.AddSingleton(kernel);
                    builder.Services.AddScoped<InterfaceAI, AzureOpenAI>();
                    break;
                default:
                    break;
            }

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddScoped<GitService>();
            return builder;
        }

        public static Kernel GetKernel(OpenAISettings settings)
        {
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddLogging().AddSerilog();
            kernelBuilder.AddAzureOpenAIChatCompletion(settings.Model, settings.Endpoint, settings.Key);
            
            return kernelBuilder.Build();
        }
    }
}
