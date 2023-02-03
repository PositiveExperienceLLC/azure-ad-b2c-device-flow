﻿using Ltwlf.Azure.B2C;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.IO;

[assembly: WebJobsStartup(typeof(Startup))]

namespace Ltwlf.Azure.B2C
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            FunctionsHostBuilderContext context = builder.GetContext();

            builder.ConfigurationBuilder
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, "appsettings.json"), optional: true, reloadOnChange: false)
                .AddJsonFile(Path.Combine(context.ApplicationRootPath, $"appsettings.{context.EnvironmentName}.json"), optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = builder.GetContext().Configuration;

            builder.Services.AddOptions<ConfigOptions>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("Config").Bind(settings);
                });

            builder.Services.AddSingleton<PageFactory>();

            builder.Services.AddHttpClient();

            //builder.Services.Configure<HttpOptions>(options => options.RoutePrefix = string.Empty);

            var redis = ConnectionMultiplexer.Connect(config.GetValue<string>("Redis:Connection"));

            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
        }
    }
}