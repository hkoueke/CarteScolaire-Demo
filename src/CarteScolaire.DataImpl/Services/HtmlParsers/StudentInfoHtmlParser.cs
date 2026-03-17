using System.Diagnostics;
using System.Globalization;
using AngleSharp;
using AngleSharp.Dom;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarteScolaire.DataImpl.Services.HtmlParsers;

#pragma warning disable CA1031

internal sealed class StudentInfoHtmlParser(
    IBrowsingContext browsingContext,
    IOptions<SelectorOptions> options,
    ILogger<StudentInfoHtmlParser> logger) : IHtmlParser<StudentInfoResponse>
{
    private readonly SelectorOptions _selectorOptions = options.Value;
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy"];

    public async Task<Result<StudentInfoCollection>> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            using IDocument document = await browsingContext
                .OpenAsync(r => r.Content(stream), cancellationToken)
                .ConfigureAwait(false);

            IHtmlCollection<IElement> elements = document.QuerySelectorAll(_selectorOptions.ResultSelector);

            Result<StudentInfoCollection> result = elements.Length is 0
                ? Result<StudentInfoCollection>.Failure("No matching items found")
                : elements.Select(ParseElement).ToArray();

            return result
                .OnSuccess(items => logger.LogDebug("{Count} HTML element(s) parsed successfully in {@Time}", items.Count, sw.Elapsed))
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

    /// <summary>Safely queries a selector and returns trimmed text, or null if absent/empty.</summary>
    private static string? GetCleanText(IElement node, string selector)
    {
        string? text = node.QuerySelector(selector)?.TextContent.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private StudentInfoResponse ParseElement(IElement node)
    {
        string registrationId = GetCleanText(node, _selectorOptions.RegistrationIdSelector) ?? "N/A";
        string studentName = GetCleanText(node, _selectorOptions.NameSelector) ?? "N/A";
        string schoolName = GetCleanText(node, _selectorOptions.SchoolNameSelector) ?? "N/A";
        string studentClass = GetCleanText(node, _selectorOptions.GradeSelector) ?? "N/A";
        string? dateOfBirthText = GetCleanText(node, _selectorOptions.DateOfBirthSelector);
        string? genderText = GetCleanText(node, _selectorOptions.GenderSelector);

        Gender gender = GenderExtensions.ToGender(genderText);

        // Use InvariantCulture for culture-neutral format strings.
        DateOnly? dob = dateOfBirthText is not null
            && DateOnly.TryParseExact(
                   dateOfBirthText,
                   DateFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out DateOnly parsed)
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