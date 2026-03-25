using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using GvResearch.Api.Models;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GvResearch.Api.Tests;

/// <summary>
/// Authentication handler that auto-authenticates every request,
/// allowing integration tests to bypass real bearer-token validation.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name used in test configuration.</summary>
    public const string SchemeName = "Test";

    /// <param name="options">Scheme options.</param>
    /// <param name="logger">Logger factory.</param>
    /// <param name="encoder">URL encoder.</param>
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Name, "test-user") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Factory that wires in the test auth handler and a mocked IGvCallService.
/// </summary>
public sealed class CallEndpointsWebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the NSubstitute mock for IGvCallService.</summary>
    public IGvCallService CallService { get; } = Substitute.For<IGvCallService>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureServices(services =>
        {
            // Replace real authentication with test scheme.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Remove any previously registered IGvCallService.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IGvCallService));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            // Register the mock.
            services.AddSingleton(CallService);
        });
    }
}

/// <summary>Integration tests for the call endpoints.</summary>
public sealed class CallEndpointsTests : IClassFixture<CallEndpointsWebAppFactory>
{
    private readonly CallEndpointsWebAppFactory _factory;

    /// <param name="factory">Shared application factory.</param>
    public CallEndpointsTests(CallEndpointsWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>GET /api/v1/calls returns 200 OK with an empty paged result.</summary>
    [Fact]
    public async Task GetCallsReturnsOkWithEmptyPagedResult()
    {
        using var client = _factory.CreateClient();

        // xUnit1030: use ConfigureAwait(true) to satisfy both CA2007 and xUnit1030.
        var response = await client.GetAsync(
            new Uri("/api/v1/calls", UriKind.Relative)).ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<CallRecord>>()
            .ConfigureAwait(true);

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.NextCursor.Should().BeNull();
    }

    /// <summary>POST /api/v1/calls with a valid body returns 201 Created.</summary>
    [Fact]
    public async Task InitiateCallWithValidRequestReturnsCreated()
    {
        using var client = _factory.CreateClient();

        var gvCallId = Guid.NewGuid().ToString("N");
        _factory.CallService
            .InitiateCallAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        var request = new InitiateCallRequest("+15550000001", "+15550000002");

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative), request).ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    /// <summary>POST /api/v1/calls with an empty ToNumber returns 400 Bad Request.</summary>
    [Fact]
    public async Task InitiateCallWithEmptyToNumberReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var request = new InitiateCallRequest("+15550000001", string.Empty);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative), request).ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>GET /api/v1/calls/{id} for an unknown ID returns 404 Not Found.</summary>
    [Fact]
    public async Task GetCallWithUnknownIdReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/calls/{Guid.NewGuid()}", UriKind.Relative))
            .ConfigureAwait(true);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
