using System.Diagnostics;
using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarteScolaire.DataImpl.Services.HtmlParsers;

internal sealed class StudentInfoHtmlParser(
    IBrowsingContext browsingContext,
    IOptions<SelectorOptions> options,
    ILogger<StudentInfoHtmlParser> logger) : IHtmlParser<StudentInfoResponse>
{
    private readonly SelectorOptions _selectorOptions = options.Value;

    public async Task<IReadOnlyCollection<StudentInfoResponse>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream, nameof(stream));

        using IDocument document = await browsingContext.OpenAsync(r => r.Content(stream), cancellationToken);
        var elements = document.QuerySelectorAll(_selectorOptions.ResultSelector).ToArray();

        if (elements.Length == 0) return [];

        var sw = Stopwatch.StartNew();

        try
        {
            return elements
                .AsParallel().WithCancellation(cancellationToken)
                .WithDegreeOfParallelism(Environment.ProcessorCount)
                .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                .Select(ParseElement)
                .ToList();
        }
        finally
        {
            sw.Stop();
            logger.LogDebug("HTML elements parsed successfully in {@Time}", sw.Elapsed);
        }
    }

    private StudentInfoResponse ParseElement(IElement node)
    {
        string GetCleanText(string selector, string defaultValue = "N/A")
            => node.QuerySelector(selector)?.TextContent.Trim().ToUpperInvariant() ?? defaultValue;

        string registrationId = GetCleanText(_selectorOptions.RegistrationIdSelector);
        string studentName = GetCleanText(_selectorOptions.NameSelector);
        string? dateOfBirth = GetCleanText(_selectorOptions.DateOfBirthSelector);
        string schoolName = GetCleanText(_selectorOptions.SchoolNameSelector);
        string studentClass = GetCleanText(_selectorOptions.GradeSelector);

        Gender gender = GetCleanText(_selectorOptions.GenderSelector) switch
        {
            "M" => Gender.Male,
            "F" => Gender.Female,
            _ => Gender.Unspecified
        };

        string[] dateFormats = ["yyyy-MM-dd", "MM/dd/yyyy"];

        DateOnly? dob = dateOfBirth is not null
            && DateOnly.TryParseExact(dateOfBirth, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

        return new StudentInfoResponse
        {
            RegistrationId = registrationId,
            Class = studentClass,
            Name = studentName,
            SchoolName = schoolName,
            DateOfBirth = dob,
            Gender = gender,
        };
    }
}
