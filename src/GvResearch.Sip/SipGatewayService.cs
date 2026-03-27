using GvResearch.Sip.Calls;
using GvResearch.Sip.Configuration;
using GvResearch.Sip.Registrar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using SIPSorcery.SIP;

namespace GvResearch.Sip;

/// <summary>
/// Hosted service that starts the SIP gateway and wires event handlers
/// between <see cref="SipRegistrar"/> and <see cref="SipCallController"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by the Generic Host DI container via AddHostedService<T>.")]
internal sealed class SipGatewayService : IHostedService
{
    private readonly SipRegistrar _registrar;
    private readonly SipCallController _controller;
    private readonly IOptions<SipGatewayOptions> _options;
    private readonly ILogger _logger;

    public SipGatewayService(
        SipRegistrar registrar,
        SipCallController controller,
        IOptions<SipGatewayOptions> options)
    {
        _registrar = registrar;
        _controller = controller;
        _options = options;
        _logger = Log.ForContext<SipGatewayService>();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _registrar.InviteReceived += OnInviteReceived;
        _registrar.ByeReceived += OnByeReceived;

        var opts = _options.Value;
        _logger.Information(
            "SIP gateway started on port {Port} for domain {Domain}.",
            opts.SipPort, opts.SipDomain);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _registrar.InviteReceived -= OnInviteReceived;
        _registrar.ByeReceived -= OnByeReceived;
        _logger.Information("SIP gateway stopped.");
        return Task.CompletedTask;
    }

    private void OnInviteReceived(object? sender, SipRequestEventArgs e)
    {
        var request = e.Request;
        var destination = request.URI.User;

        // Fire-and-forget: any errors are logged inside CreateOutboundCallAsync.
        _ = _controller.CreateOutboundCallAsync(
            sipCallId: request.Header.CallId,
            destinationNumber: destination);
    }

    private void OnByeReceived(object? sender, SipRequestEventArgs e)
    {
        _ = _controller.HangupAsync(e.Request.Header.CallId);
    }
}
