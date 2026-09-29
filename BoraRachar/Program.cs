using BoraRachar.Configuration;
using BoraRachar.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBoraRachar(builder.Configuration);
builder.Services.AddBoraRacharRateLimiting();

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 128 * 1024);

var app = builder.Build();
if (args.Length == 2 && args[0] == "--rotate-group-token")
{
    Environment.ExitCode = await GroupTokenAdmin.RotateAsync(app.Services, args[1]);
    return;
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();

app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers();
app.Run();
