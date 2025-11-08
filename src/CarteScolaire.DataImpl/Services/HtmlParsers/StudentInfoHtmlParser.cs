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

    public async Task<Result<IReadOnlyCollection<StudentInfoResponse>>> ParseAsync(
        Stream stream, CancellationToken cancellationToken)
    {
        if (stream is null)
        {
            return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure("Input stream cannot be null.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using IDocument document = await browsingContext.OpenAsync(r => r.Content(stream), cancellationToken);
            var elements = document.QuerySelectorAll(_selectorOptions.ResultSelector).ToArray();

            if (elements.Length == 0)
            {
                return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure("No matching items found");
            }

            var responses = elements.Select(ParseElement).ToList();

            sw.Stop();
            logger.LogDebug("HTML elements parsed successfully in {@Time}", sw.Elapsed);

            return responses;
        }
        catch (Exception ex)
        {
            // Catch all other exceptions and return a Failure result.
            sw.Stop();
            logger.LogError(ex, "Failed to parse HTML stream.");
            return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure($"Parsing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper to safely get and trim text content from a node.
    /// </summary>
    private static string? GetCleanText(IElement node, string selector) => node.QuerySelector(selector)?.TextContent?.Trim();

    private StudentInfoResponse ParseElement(IElement node)
    {
        string registrationId = GetCleanText(node, _selectorOptions.RegistrationIdSelector) ?? "N/A";
        string studentName = GetCleanText(node, _selectorOptions.NameSelector) ?? "N/A";
        string schoolName = GetCleanText(node, _selectorOptions.SchoolNameSelector) ?? "N/A";
        string studentClass = GetCleanText(node, _selectorOptions.GradeSelector) ?? "N/A";
        string? dateOfBirthText = GetCleanText(node, _selectorOptions.DateOfBirthSelector);
        string? genderText = GetCleanText(node, _selectorOptions.GenderSelector);
        Gender gender = genderText?.ToUpperInvariant() switch
        {
            "M" => Gender.Male,
            "F" => Gender.Female,
            _ => Gender.Unspecified
        };

        string[] dateFormats = ["yyyy-MM-dd", "MM/dd/yyyy"];

        DateOnly? dob = dateOfBirthText is not null
            && DateOnly.TryParseExact(dateOfBirthText, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
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
