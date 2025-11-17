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

    public async Task<Result<StudentInfoCollection>> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using IDocument document = await browsingContext.OpenAsync(r => r.Content(stream), cancellationToken);
            var elements = document.QuerySelectorAll(_selectorOptions.ResultSelector);

            return
                (elements.Length == 0
                    ? Result<StudentInfoCollection>.Failure("No matching items found")
                    : elements.Select(ParseElement).ToArray())
                .OnSuccess(items => logger.LogDebug("{Count} HTML elements parsed successfully in {@Time}", items.Count, sw.Elapsed))
                .OnFailure(error => logger.LogError("Failed to parse HTML stream. Reason: {Reason}.", error));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse HTML stream.");
            return Result<StudentInfoCollection>.Failure($"Parsing failed: {ex.Message}");
        }
        finally
        {
            sw.Stop();
        }
    }

    /// <summary>
    /// Helper to safely get and trim text content from a node.
    /// </summary>
    private static string? GetCleanText(IElement node, string selector) => node.QuerySelector(selector)?.TextContent.Trim();

    private StudentInfoResponse ParseElement(IElement node)
    {
        var registrationId = GetCleanText(node, _selectorOptions.RegistrationIdSelector) ?? "N/A";
        var studentName = GetCleanText(node, _selectorOptions.NameSelector) ?? "N/A";
        var schoolName = GetCleanText(node, _selectorOptions.SchoolNameSelector) ?? "N/A";
        var studentClass = GetCleanText(node, _selectorOptions.GradeSelector) ?? "N/A";
        var dateOfBirthText = GetCleanText(node, _selectorOptions.DateOfBirthSelector);
        var genderText = GetCleanText(node, _selectorOptions.GenderSelector);
        var gender = genderText?.ToUpperInvariant() switch
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
