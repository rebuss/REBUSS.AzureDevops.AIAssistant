using REBUSS.GitDaif.Service.API;

var builder = WebApplication.CreateBuilder(args)
                            .SetupLogging()
                            .SetupConfiguration()
                            .SetupServices();

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