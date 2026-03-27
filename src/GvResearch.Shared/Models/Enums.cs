namespace GvResearch.Shared.Models;

public enum GvThreadType { All, Sms, Calls, Voicemail, Missed }
public enum GvMessageType { Sms, Voicemail, MissedCall, RecordedCall }
public enum CallStatusType { Unknown, Ringing, Active, Completed, Failed }
public enum PhoneNumberType { Mobile, Landline, GoogleVoice }
public enum DeviceType { Phone, Web, Unknown }
