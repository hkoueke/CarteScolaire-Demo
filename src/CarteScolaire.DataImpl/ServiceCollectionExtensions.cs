using AngleSharp;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Handlers;
using CarteScolaire.DataImpl.Services;
using CarteScolaire.DataImpl.Services.HtmlParsers;
using CarteScolaire.DataImpl.Services.TokenProviders;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace CarteScolaire.DataImpl;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection RegisterImplementations(this IServiceCollection services)
    {
        ConfigureOptions(services);
        RegisterDependencies(services);
        ConfigureResilientHttpClients(services);
        ConfigureCaching(services);
        return services;
    }

    private static void ConfigureOptions(IServiceCollection services)
    {
        services
            .AddOptionsWithValidateOnStart<TokenProviderOptions>()
            .ValidateDataAnnotations()
            .Configure(options =>
            {
                options.TokenSelector = "input[type='hidden'][name='_token']";
                options.TokenEndpointPath = "/";
            });

        services
            .AddOptionsWithValidateOnStart<SelectorOptions>()
            .ValidateDataAnnotations()
            .Configure(options =>
            {
                options.ResultSelector = "div.result-item";
                options.RegistrationIdSelector = "p.actual-matricule";
                options.NameSelector = "p.subtitle";
                options.DateOfBirthSelector = "p.student-year";
                options.SchoolNameSelector = "p.title";
                options.GradeSelector = "p.student-class";
                options.GenderSelector = "div.gender > p";
            });
    }

    private static void RegisterDependencies(IServiceCollection services)
    {
        services.AddTransient<IHtmlParser<StudentInfoResponse>, StudentInfoHtmlParser>();
        services.AddScoped<TokenAppendingHandler>();
        services.AddSingleton(_ => BrowsingContext.New(Configuration.Default.WithDefaultLoader()));
    }

    private static void ConfigureResilientHttpClients(IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(config =>
        {
            config.ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://cartescolaire.cm");
                client.DefaultRequestHeaders.Add("Accept", "text/html");
                client.Timeout = TimeSpan.FromSeconds(100);
            });

            config.AddStandardResilienceHandler(options =>
            {
                // Retry Policy: 2 Attempts (Total 3 calls: 1 initial + 2 retries)
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                // Attempt Timeout: Maximum time for a single HTTP request (20s)
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);

                // Circuit Breaker Policy: Opens if 20% of calls fail.
                options.CircuitBreaker.FailureRatio = 0.2;

                // Set the minimum number of requests (throughput) needed in the SamplingDuration 
                // before the failure ratio calculation is performed.
                options.CircuitBreaker.MinimumThroughput = 20;

                // REVISED: Set sampling duration to a meaningful monitoring period (e.g., 60s)
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);

                // Total Timeout Policy: Max time for the entire operation (including retries).
                // REVISED: Set a comfortable time, as 90s (from original calculation) may be too short
                // due to Exponential Backoff and Jitter. 150 seconds is safer.
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(150);
            });

        });

        services
            .AddHttpClient<IStudentInfoService, StudentInfoService>(client => client.BaseAddress = new Uri(client.BaseAddress!, "/get-matricule"))
            .AddHttpMessageHandler<TokenAppendingHandler>();

        services.AddHttpClient<ITokenProvider<string>, TokenProvider>(client => client.BaseAddress = new Uri(client.BaseAddress!, "/minesec"));
    }

    private static void ConfigureCaching(IServiceCollection services)
    {
        services
            .AddFusionCache()
            .TryWithAutoSetup()
            .WithOptions(options => options.DefaultEntryOptions.Duration = TimeSpan.FromMinutes(2));
    }
}
