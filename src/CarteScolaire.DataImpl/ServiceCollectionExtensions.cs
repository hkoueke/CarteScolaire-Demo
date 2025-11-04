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
                options.TokenSelector = @"input[type='hidden'][name='_token']";
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
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.CircuitBreaker.FailureRatio = 0.2;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.SamplingDuration = 2 * options.AttemptTimeout.Timeout; //AttemptTimeout x 2 at least
                // Best Practice: Set TotalRequestTimeout > AttemptTimeout × (MaxRetryAttempts + 1)
                options.TotalRequestTimeout.Timeout = options.AttemptTimeout.Timeout * (options.Retry.MaxRetryAttempts + 1.5); //+0.5 for buffer
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
