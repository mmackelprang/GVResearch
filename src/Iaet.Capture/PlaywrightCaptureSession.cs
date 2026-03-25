using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class PlaywrightCaptureSession : ICaptureSession
{
    private readonly string _targetApplication;
    private readonly string _profile;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private CdpNetworkListener? _listener;

    public Guid SessionId { get; } = Guid.NewGuid();
    public string TargetApplication => _targetApplication;
    public bool IsRecording { get; private set; }

    public PlaywrightCaptureSession(string targetApplication, string profile)
    {
        _targetApplication = targetApplication;
        _profile = profile;
    }

    public async Task StartAsync(string url, CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = [$"--profile-directory={_profile}"]
        }).ConfigureAwait(false);

        _page = await _browser.NewPageAsync().ConfigureAwait(false);
        _listener = new CdpNetworkListener(SessionId);
        _listener.Attach(_page);
        IsRecording = true;

        await _page.GotoAsync(url).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        IsRecording = false;
        if (_browser is not null) await _browser.CloseAsync().ConfigureAwait(false);
        _playwright?.Dispose();
    }

    public async IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_listener is null) yield break;
        foreach (var request in _listener.DrainCaptured())
        {
            yield return request;
        }
        await Task.CompletedTask.ConfigureAwait(false); // satisfy async requirement
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRecording) await StopAsync().ConfigureAwait(false);
    }
}
