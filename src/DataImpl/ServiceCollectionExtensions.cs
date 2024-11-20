using Data.Responses;
using Data.Services;
using DataImpl.Handlers;
using DataImpl.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace DataImpl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterImplementations(this IServiceCollection services)
    {
        RegisterDependencies(services);
        ConfigureResilientHttpClients(services);
        ConfigureDistributedMemoryCaching(services);
        return services;
    }

    private static void RegisterDependencies(IServiceCollection services)
    {
        services.AddTransient<IHtmlParser<StudentInfo>, StudentInfoHtmlParser>();
        services.AddScoped<TokenAppendingHandler>();
    }

    private static void ConfigureResilientHttpClients(IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(config =>
        {
            config.AddStandardResilienceHandler();
            config.ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://cartescolaire.cm");
                client.DefaultRequestHeaders.Add("Accept", "text/html");
            });
        });

        services
            .AddHttpClient<IStudentInfoService, StudentInfoService>(client =>
            {
                client.BaseAddress = new Uri(client.BaseAddress!, "/get-matricule");
            })
            .AddHttpMessageHandler<TokenAppendingHandler>();

        services.AddHttpClient<ITokenFetcher<string>, StringTokenFetcher>(
            client => client.BaseAddress = new Uri(client.BaseAddress!, "/minesec"));

        services.Configure<HttpStandardResilienceOptions>(configure =>
        {
            configure.Retry.MaxRetryAttempts = 3;
            configure.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            configure.Retry.UseJitter = true;
            configure.CircuitBreaker.FailureRatio = 0.2;
        });
    }

    private static void ConfigureDistributedMemoryCaching(IServiceCollection services)
    {
        services.AddDistributedMemoryCache(options => options.ExpirationScanFrequency = TimeSpan.FromMinutes(1));
    }
}
