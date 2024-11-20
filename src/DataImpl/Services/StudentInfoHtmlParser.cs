using Data.Responses;
using Data.Services;
using HtmlAgilityPack;
using System.Globalization;

namespace DataImpl.Services;

internal sealed class StudentInfoHtmlParser : IHtmlParser<StudentInfo>
{
    public async Task<IEnumerable<StudentInfo>> ParseAsync(string html, CancellationToken cancellationToken = default)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        HtmlNodeCollection? nodes = document.DocumentNode.SelectNodes("//div[@class='result-item']");
        if (nodes is null || nodes.Count == 0)
        {
            return [];
        }

        IEnumerable<Task<StudentInfo>> tasks = nodes.Select(node => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string registrationId = node.SelectSingleNode(".//p[@class='actual-matricule']")?.InnerText.Trim().ToUpper() ?? "N/A";
            string studentName = node.SelectSingleNode(".//p[@class='subtitle']")?.InnerText.Trim().ToUpper() ?? "N/A";
            string? dateOfBirth = node.SelectSingleNode(".//p[@class='student-year']")?.InnerText.Trim().ToUpper();
            string schoolName = node.SelectSingleNode(".//p[@class='title']")?.InnerText.Trim().ToUpper() ?? "N/A";
            string studentClass = node.SelectSingleNode(".//p[@class='student-class']")?.InnerText.Trim().ToUpper() ?? "N/A";
            Gender gender = node.SelectSingleNode(".//div[@class='gender']/p")?.InnerText.Trim().ToUpper() switch
            {
                "M" => Gender.Male,
                "F" => Gender.Female,
                _ => Gender.Unspecified,
            };

            // Acceptable date formats
            string[] dateFormats = ["yyyy-MM-dd", "MM/dd/yyyy"];

            DateOnly? dob = dateOfBirth is not null
                && DateOnly.TryParseExact(dateOfBirth, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;

            return new StudentInfo(
                registrationId,
                studentName,
                dob,
                gender,
                schoolName,
                studentClass);

        }, cancellationToken));

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
