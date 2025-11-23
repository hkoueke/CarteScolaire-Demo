using System.Net;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Services;
using FluentAssertions;
using NSubstitute;

namespace CarteScolaire.Tests.Unit;

public class StudentInfoServiceTests
{
    private readonly IHtmlParser<StudentInfoResponse> _htmlParser;
    private readonly StudentInfoService _sut;

    // This field allows us to swap behavior per test without re-creating the Service
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handlerOverride;

    //default request object
    private readonly StudentInfoQuery _query = new("1234", "DUPONT");

    public StudentInfoServiceTests()
    {
        // Default behavior: throw to ensure tests explicitly set up their expectations
        _handlerOverride = (_, _) => throw new InvalidOperationException("Handler not configured for this test.");

        // Wire up the client to use our mutable delegate
        var handler = new TestHttpMessageHandler((req, token) => _handlerOverride(req, token));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.cartescolaire.test/")
        };

        _htmlParser = Substitute.For<IHtmlParser<StudentInfoResponse>>();
        _sut = new StudentInfoService(httpClient, _htmlParser);
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenBaseAddressIsNull()
    {
        // Arrange
        var clientNoBase = new HttpClient();
        var sut = new StudentInfoService(clientNoBase, _htmlParser);

        // Act
        var result = await sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("missing Base Address");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldBuildCorrectUri_AndReturnParsedResult_WhenHttpCallSucceeds()
    {
        // Arrange
        var expectedResponse = new StudentInfoCollection([
                new StudentInfoResponse{ Class = "C1", Name = "DUPONT", Gender = Gender.Male, RegistrationId = "R134", SchoolName = "S1"},
                new StudentInfoResponse{ Class = "C1", Name = "DUPONT 2", Gender = Gender.Male, RegistrationId = "R234", SchoolName = "S1"}]
        );

        // Setup Parser
        _htmlParser
            .ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyCollection<StudentInfoResponse>>.Success(expectedResponse));

        // Setup HTTP: Capture the request to assert on it later
        HttpRequestMessage? capturedRequest = null;
        _handlerOverride = (req, _) =>
        {
            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Mock Content</html>")
            });
        };

        // Act
        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        // Assert
        // 1. Validate Result
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expectedResponse);

        // 2. Validate Request
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Get);
        capturedRequest.RequestUri!.Query.Should().Contain("student_name=DUPONT");
        capturedRequest.RequestUri!.Query.Should().Contain("school_code=1234");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenHttpResponseIsNotSuccess()
    {
        // Arrange
        _handlerOverride = (_, _) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            ReasonPhrase = "Server on fire"
        });

        // Act
        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to retrieve data");
        result.Error.Should().Contain("Server on fire");
    }

    [Fact]
    public async Task GetStudentInfoAsync_ShouldReturnFailure_WhenExceptionIsThrown()
    {
        // Arrange : Simulate network failure
        _handlerOverride = (_, _) => throw new HttpRequestException("Network error");

        // Act
        var result = await _sut.GetStudentInfoAsync(_query, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Network error");
    }
}