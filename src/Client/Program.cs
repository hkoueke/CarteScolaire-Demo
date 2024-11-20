using Data.Requests;
using Data.Responses;
using Data.Services;
using DataImpl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureServices((_, services) => services.RegisterImplementations())
    .UseSerilog((_, config) =>
    {
        config.Enrich.FromLogContext()
              .WriteTo.Console();
    })
    .Build();

try
{
    var studentService = host.Services.GetRequiredService<IStudentInfoService>();

    IEnumerable<StudentInfo> results =
    await studentService.GetStudentInfoAsync(new StudentInfoRequest("OU50130I12", "MANDOU"))
                        .ConfigureAwait(false);

    foreach (var result in results)
        Console.WriteLine(result);

    Console.WriteLine("Appuyez sur n'importe quelle touche pour quitter:");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message.ToUpperInvariant());
}