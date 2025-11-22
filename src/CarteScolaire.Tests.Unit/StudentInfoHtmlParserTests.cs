using System.Text;
using AngleSharp;
using CarteScolaire.Data.Responses;
using CarteScolaire.DataImpl.Services.HtmlParsers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CarteScolaire.Tests.Unit;

public class StudentInfoHtmlParserTests : IDisposable
{
    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly ILogger<StudentInfoHtmlParser> _logger = Substitute.For<ILogger<StudentInfoHtmlParser>>();

    private readonly SelectorOptions _options = new()
    {
        ResultSelector = ".student-row",
        RegistrationIdSelector = ".reg-id",
        NameSelector = ".name",
        SchoolNameSelector = ".school",
        GradeSelector = ".class",
        DateOfBirthSelector = ".dob",
        GenderSelector = ".gender"
    };

    public void Dispose()
    {
        _browsingContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private StudentInfoHtmlParser CreateParser() => new(_browsingContext, Options.Create(_options), _logger);

    private static MemoryStream HtmlStream(string html) => new(Encoding.UTF8.GetBytes(html));

    [Fact]
    public async Task ParseAsync_ShouldReturnSuccess_WhenAllDataIsPresent_AndMultipleRows()
    {
        // Arrange
        const string html =
            """
                <div class="student-row">
                    <span class="reg-id">2024001</span>
                    <span class="name">Jean Dupont</span>
                    <span class="school">Collège Victor Hugo</span>
                    <span class="class">6ème A</span>
                    <span class="dob">2012-03-15</span>
                    <span class="gender">M</span>
                </div>
                <div class="student-row">
                    <span class="reg-id">2024002</span>
                    <span class="name">Marie Curie</span>
                    <span class="school">Lycée Marie Curie</span>
                    <span class="class">Terminale S</span>
                    <span class="dob">03/22/2011</span>
                    <span class="gender">f</span>
                </div>
            """;

        var parser = CreateParser();

        // Act
        var result = await parser.ParseAsync(HtmlStream(html), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var collection = result.Value.ToList();
        collection.Should().HaveCount(2);

        var jean = collection[0];
        jean.RegistrationId.Should().Be("2024001");
        jean.Name.Should().Be("Jean Dupont");
        jean.SchoolName.Should().Be("Collège Victor Hugo");
        jean.Class.Should().Be("6ème A");
        jean.DateOfBirth.Should().Be(new DateOnly(2012, 3, 15));
        jean.Gender.Should().Be(Gender.Male);

        var marie = collection[1];
        marie.RegistrationId.Should().Be("2024002");
        marie.Name.Should().Be("Marie Curie");
        marie.Gender.Should().Be(Gender.Female);
        marie.DateOfBirth.Should().Be(new DateOnly(2011, 3, 22));
    }

    [Fact]
    public async Task ParseAsync_ShouldUseFallbackValues_WhenSomeFieldsAreMissing()
    {
        const string html = 
            """
                <div class="student-row">
                    <span class="reg-id">999</span>
                    <span class="name">Alice</span>
                    <!-- school, class, dob, gender missing -->
                </div>
            """;

        var parser = CreateParser();
        var result = await parser.ParseAsync(HtmlStream(html), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var student = result.Value.Single();

        student.RegistrationId.Should().Be("999");
        student.Name.Should().Be("Alice");
        student.SchoolName.Should().Be("N/A");
        student.Class.Should().Be("N/A");
        student.DateOfBirth.Should().BeNull();
        student.Gender.Should().Be(Gender.Unspecified);
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnFailure_WhenNoRowsMatchSelector()
    {
        const string html = "<div>No students here</div>";
        var parser = CreateParser();

        var result = await parser.ParseAsync(HtmlStream(html), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No matching items found");
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnFailure_WhenHtmlIsInvalid_AndLogException()
    {
        // Malformed HTML stream (truncated)
        var invalidBytes = "<html><body><div class=\"student-row\">"u8.ToArray()[..^5];
        var stream = new MemoryStream(invalidBytes);
        var parser = CreateParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().StartWith("No matching items found");

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ParseAsync_ShouldParseGender_CaseInsensitively_AndHandleUnexpectedValues()
    {
        const string html = 
            """
                <div class="student-row"><span class="gender">M</span></div>
                <div class="student-row"><span class="gender">f</span></div>
                <div class="student-row"><span class="gender">male</span></div>
                <div class="student-row"><span class="gender"></span></div>
            """;

        var parser = CreateParser();
        var result = await parser.ParseAsync(HtmlStream(html), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var genders = result.Value.Select(x => x.Gender).ToArray();

        genders.Should().BeEquivalentTo(
        [
            Gender.Male,
            Gender.Female,
            Gender.Unspecified,
            Gender.Unspecified
        ]);
    }

    [Fact]
    public async Task ParseAsync_ShouldSupportMultipleDateFormats_IncludingCurrentCulture()
    {
        const string html = 
            """
                <div class="student-row"><span class="dob">2023-12-01</span></div>
                <div class="student-row"><span class="dob">12/25/2022</span></div>
            """;

        var parser = CreateParser();
        var result = await parser.ParseAsync(HtmlStream(html), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var dates = result.Value.Select(x => x.DateOfBirth).ToArray();

        dates.Should().BeEquivalentTo(new DateOnly?[]
        {
            new DateOnly(2023, 12, 1),
            new DateOnly(2022, 12, 25)
        });
    }

    [Fact]
    public async Task ParseAsync_ShouldLogDebugOnSuccess_AndErrorOnFailure()
    {
        // Success case
        const string html = """<div class="student-row"><span class="name">Test</span></div>""";
        var parser = CreateParser();
        await parser.ParseAsync(HtmlStream(html), TestContext.Current.CancellationToken);

        _logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("1 HTML elements parsed successfully")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        _logger.ClearReceivedCalls();

        // Failure case
        const string badHtml = "<invalid>";
        await parser.ParseAsync(HtmlStream(badHtml), TestContext.Current.CancellationToken);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}