namespace GvResearch.Softphone.Configuration;

public sealed class SoftphoneSettings
{
    public string SipServer { get; set; } = "127.0.0.1";
    public int SipPort { get; set; } = 5060;
    public string SipUsername { get; set; } = "";
    public string SipPassword { get; set; } = "";
    public string SipDomain { get; set; } = "gvresearch.local";
    public string DisplayName { get; set; } = "GV Softphone";
}
