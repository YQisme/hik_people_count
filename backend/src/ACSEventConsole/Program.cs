using ACSEventConsole.Controllers;
using ACSEventConsole.Services.Hosted;
using Microsoft.AspNetCore.Mvc.Controllers;

var runtimeConfig = RuntimeConfig.LoadDefault();
var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls($"http://0.0.0.0:{runtimeConfig.WebPort}");

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        for (int i = manager.FeatureProviders.Count - 1; i >= 0; i--)
        {
            if (manager.FeatureProviders[i] is ControllerFeatureProvider)
            {
                manager.FeatureProviders.RemoveAt(i);
            }
        }

        manager.FeatureProviders.Add(new ExplicitControllersFeatureProvider());
    });
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<AcsBackgroundHostedService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ACSEventConsole API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors();
app.MapControllers();

string localIp = LocalNetworkHelper.GetLocalIPAddress();
Console.WriteLine($"Web 服务已启动: http://localhost:{runtimeConfig.WebPort}/");
Console.WriteLine($"Swagger 文档: http://localhost:{runtimeConfig.WebPort}/swagger");
if (!string.IsNullOrEmpty(localIp))
{
    Console.WriteLine($"同网段访问地址: http://{localIp}:{runtimeConfig.WebPort}/");
    Console.WriteLine($"同网段 Swagger: http://{localIp}:{runtimeConfig.WebPort}/swagger");
}

app.Run();
