using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var host = Host
    .CreateDefaultBuilder(args)
    .UseSerilog((_, config) =>
    {
        config.Enrich.FromLogContext()
              .WriteTo.Console()
              .MinimumLevel.Information();
    })
    .ConfigureServices((_, services) =>
    {
        services.RegisterImplementations();
    })
    .Build();

using CancellationTokenSource tokenSource = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    // Prevent the application from immediately terminating
    eventArgs.Cancel = true;

    Console.WriteLine("[Cancellation Requested: Ctrl+C pressed]");
    // ReSharper disable once AccessToDisposedClosure
    tokenSource.Cancel();
};

Console.WriteLine("Press Ctrl+C to cancel the operation.");

try
{
    var studentService = host.Services.GetRequiredService<IStudentInfoService>();

    var result =
        await studentService.GetStudentInfoAsync(new StudentInfoQuery("OU50130I12", "NSANGOU"), tokenSource.Token);

    result.Match(
        onSuccess: items =>
        {
            Console.WriteLine($"Students found matching the criteria: {items.Count}");
            foreach (var item in items) Console.WriteLine(item);
        },
        onFailure: Console.WriteLine
    );

}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
finally
{
    Console.WriteLine("Appuyez sur n'importe quelle touche pour quitter:");
    Console.ReadKey();
}
