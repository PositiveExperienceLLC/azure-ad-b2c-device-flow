using System.IO;
using Ltwlf.Azure.B2C;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, builder) =>
    {
        builder
            .AddJsonFile(Path.Combine(context.HostingEnvironment.ContentRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(context.HostingEnvironment.ContentRootPath, $"appsettings.{context.HostingEnvironment.EnvironmentName}.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<ConfigOptions>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("Config").Bind(settings);
            });

        services.AddSingleton<PageFactory>();

        services.AddHttpClient();

        services.AddMemoryCache();
        services.AddSingleton<IAuthorizationStateStore, MemoryAuthorizationStateStore>();
    })
    .Build();

host.Run();
