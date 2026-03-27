using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GvResearch.Api.Tests;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

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
/// Wraps an IGvClient mock so that DI disposal does not forward to the shared substitute.
/// IGvClient : IAsyncDisposable — if DI owns disposal it would destroy the shared mock
/// between tests. This wrapper intercepts DisposeAsync and does nothing.
/// </summary>
internal sealed class NonDisposingClientWrapper : IGvClient
{
    private readonly IGvClient _inner;

    public NonDisposingClientWrapper(IGvClient inner) => _inner = inner;

    public IGvAccountClient Account => _inner.Account;
    public IGvThreadClient Threads => _inner.Threads;
    public IGvSmsClient Sms => _inner.Sms;
    public IGvCallClient Calls => _inner.Calls;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class GvApiWebAppFactory : WebApplicationFactory<Program>
{
    public IGvClient Client { get; } = Substitute.For<IGvClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGvClient));
            if (descriptor is not null) services.Remove(descriptor);

            // Register via factory delegate so DI does not take disposal ownership of
            // the shared NSubstitute mock. The wrapper's DisposeAsync is a no-op.
            services.AddSingleton<IGvClient>(_ => new NonDisposingClientWrapper(Client));
        });
    }
}

public sealed class ApiEndpointsTests : IClassFixture<GvApiWebAppFactory>
{
    private readonly GvApiWebAppFactory _factory;

    public ApiEndpointsTests(GvApiWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAccount_ReturnsOk()
    {
        var account = new GvAccount(
            [new GvPhoneNumber("+15551234567", PhoneNumberType.GoogleVoice, true)],
            [], new GvSettings(false, null));
        _factory.Client.Account.GetAsync(Arg.Any<CancellationToken>()).Returns(account);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/account", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAccount_WhenAuthFails_Returns401()
    {
        _factory.Client.Account.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GvAuthException("Unauthorized"));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/account", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListThreads_ReturnsOk()
    {
        _factory.Client.Threads.ListAsync(Arg.Any<GvThreadListOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GvThreadPage([], null, 0));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/threads", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendSms_WithValidRequest_ReturnsCreated()
    {
        _factory.Client.Sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GvSmsResult("t.+15551234567", true));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/sms", UriKind.Relative),
            new { ToNumber = "+15551234567", Message = "Hello!" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendSms_WithEmptyNumber_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/sms", UriKind.Relative),
            new { ToNumber = "", Message = "Hello!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitiateCall_ReturnsCreated()
    {
        _factory.Client.Calls.InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok("call-123"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative),
            new { ToNumber = "+15551234567" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InitiateCall_WhenRateLimited_Returns429()
    {
        _factory.Client.Calls.InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GvRateLimitException("calls/initiate"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative),
            new { ToNumber = "+15551234567" });

        response.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
