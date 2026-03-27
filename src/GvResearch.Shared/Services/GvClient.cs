namespace GvResearch.Shared.Services;

public sealed class GvClient : IGvClient
{
    public GvClient(
        IGvAccountClient account,
        IGvThreadClient threads,
        IGvSmsClient sms,
        IGvCallClient calls)
    {
        Account = account;
        Threads = threads;
        Sms = sms;
        Calls = calls;
    }

    public IGvAccountClient Account { get; }
    public IGvThreadClient Threads { get; }
    public IGvSmsClient Sms { get; }
    public IGvCallClient Calls { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
