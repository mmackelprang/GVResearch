namespace GvResearch.Shared.Models;

public sealed record GvAccount(
    IReadOnlyList<GvPhoneNumber> PhoneNumbers,
    IReadOnlyList<GvDevice> Devices,
    GvSettings Settings);

public sealed record GvPhoneNumber(string Number, PhoneNumberType Type, bool IsPrimary);
public sealed record GvDevice(string DeviceId, string Name, DeviceType Type);
public sealed record GvSettings(bool DoNotDisturb, Uri? VoicemailGreetingUrl);
