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
              .MinimumLevel.Debug();
    })
    .ConfigureServices((_, services) =>
    {
        services.RegisterImplementations();
    })
    .Build();

try
{
    var studentService = host.Services.GetRequiredService<IStudentInfoService>();
    var results = await studentService.GetStudentInfoAsync(new StudentInfoQuery("OU50130I12", "NSANGOU"));

    Console.WriteLine();
    Console.WriteLine($"Students found matching the criteria: {results.Count}");
    foreach (var result in results) Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message.ToUpperInvariant());
}
finally
{
    Console.WriteLine("Appuyez sur n'importe quelle touche pour quitter:");
    Console.ReadKey();
}
