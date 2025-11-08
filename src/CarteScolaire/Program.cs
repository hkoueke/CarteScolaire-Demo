using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
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

    Result<IReadOnlyCollection<StudentInfoResponse>> result =
        await studentService.GetStudentInfoAsync(new StudentInfoQuery("OU50130I12", "NSANGOU"));

    if (result.IsFailure)
        Console.WriteLine(result.Error);
    else
    {
        Console.WriteLine($"Students found matching the criteria: {result.Value.Count}");
        foreach (var item in result.Value) Console.WriteLine(item);
    }
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
