using System.Globalization;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;


using IHost host = Host
    .CreateDefaultBuilder(args)
    .UseDefaultServiceProvider((_, configure) =>
    {
        configure.ValidateScopes = true;
        configure.ValidateOnBuild = true;
    })
    .UseSerilog((_, config) =>
    {
        config.Enrich.FromLogContext()
              .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
              .MinimumLevel.Information();
    })
    .ConfigureServices((_, services) =>
    {
        services.RegisterImplementations();
    })
    .Build();

using CancellationTokenSource tokenSource = new();
Console.CancelKeyPress += OnCancelKeyPressed;
Console.WriteLine("Press Ctrl+C to cancel the operation.");

try
{
    IStudentInfoService studentService = host.Services.GetRequiredService<IStudentInfoService>();

    SearchQuery query = new()
    {
        SchoolId = "OU50130I12",
        Name = "nsangou",
        Gender = Gender.Female,
        DateOfBirth = new DateOnly(2014, 8, 1),
        DatePrecision = DatePrecision.Year,
        Fuzziness = 0.7f,
        MaxResults = 500
    };

    Result<IReadOnlyCollection<StudentInfoResponse>> result = await studentService
        .GetStudentInfoAsync(query, tokenSource.Token)
        .ConfigureAwait(false);

    result.Match(
        onSuccess: items =>
        {
            Console.WriteLine($"Matches found : {items.Count:N0}");
            foreach (StudentInfoResponse item in items)
            {
                Console.WriteLine(item);
            }
        },
        onFailure: Console.WriteLine
    );
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled by the user.");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
    throw;
}
finally
{
    Console.CancelKeyPress -= OnCancelKeyPressed;

    if (!Console.IsInputRedirected)
    {
        Console.WriteLine("Appuyez sur n'importe quelle touche pour quitter...");
        Console.ReadKey(intercept: true); // intercept: true avoids echoing the key to the console
    }
}

return;

void OnCancelKeyPressed(object? _, ConsoleCancelEventArgs eventArgs)
{
    // Prevent the OS from immediately terminating the process
    eventArgs.Cancel = true;
    Console.WriteLine("[Cancellation Requested: Ctrl+C pressed]");

    // Guard against calling Cancel() on an already-cancelled or disposed source
    if (!tokenSource.IsCancellationRequested)
    {
        tokenSource.Cancel();
    }
}