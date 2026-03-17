using System.Net;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Services;
using FluentAssertions;
using NSubstitute;

namespace CarteScolaire.Tests.Unit;

public sealed class StudentInfoServiceTests : IDisposable
{
    private readonly IHtmlParser<StudentInfoResponse> _htmlParser;
    private readonly StudentInfoService _sut;

    // Allows per-test HTTP behavior without recreating the service under test.
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerOverride;

    private readonly SearchQuery _query = new()
    {
        SchoolId = "1234",
        Name = "DUPONT"
    };

    // promote both to instance fields and dispose them in IDisposable.Dispose(),
    // which is called by xUnit after each test method completes, guaranteeing the client
    // outlives the service under test for the duration of the test.
    private readonly TestHttpMessageHandler _handler;
    private readonly HttpClient _httpClient;

    public StudentInfoServiceTests()
    {
        _handlerOverride = (_, _) => throw new NotImplementedException("Handler not configured for this test.");

        _handler = new TestHttpMessageHandler((req, token) => _handlerOverride(req, token));
        _httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://api.cartescolaire.test/")
        };

        _htmlParser = Substitute.For<IHtmlParser<StudentInfoResponse>>();
        _sut = new StudentInfoService(_httpClient, _htmlParser);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenBaseAddressIsNull()
    {
        using HttpClient clientNoBase = new();
        StudentInfoService sut = new(clientNoBase, _htmlParser);

        var result = await sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid HTTP request: BaseAddress is not set on the HttpClient.");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldBuildCorrectUri_AndReturnParsedResult_WhenHttpCallSucceeds()
    {
        StudentInfoCollection expectedResponse = new([
            new StudentInfoResponse { Class = "C1", Name = "DUPONT",   Gender = Gender.Male, RegistrationId = "R134", SchoolName = "S1" },
            new StudentInfoResponse { Class = "C1", Name = "DUPONT 2", Gender = Gender.Male, RegistrationId = "R234", SchoolName = "S1" }
        ]);

        _htmlParser
            .ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<StudentInfoResponse>>.Success(expectedResponse));

        HttpRequestMessage? capturedRequest = null;

        _handlerOverride = (req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Mock Content</html>")
            });
        };

        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedResponse);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.Query.Should().Contain("student_name=DUPONT");
        capturedRequest.RequestUri!.Query.Should().Contain("school_code=1234");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenHttpResponseIsNotSuccess()
    {
        _handlerOverride = (_, _) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            ReasonPhrase = "Server on fire"
        });

        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve data");
        result.Error.Should().Contain("Server on fire");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenExceptionIsThrown()
    {
        _handlerOverride = (_, _) => throw new HttpRequestException("Network error");

        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Network error");
    }
}