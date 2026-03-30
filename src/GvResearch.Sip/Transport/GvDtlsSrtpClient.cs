using Org.BouncyCastle.Tls;
using SIPSorcery.Net.SharpSRTP.DTLSSRTP;

namespace GvResearch.Sip.Transport;

/// <summary>
/// Custom DTLS-SRTP client that offers ECDSA cipher suites.
///
/// Google Voice's DTLS server uses ECDSA certificates and requires
/// TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256. SIPSorcery's built-in
/// DtlsSrtpClient only offers RSA cipher suites, causing handshake_failure(40).
///
/// This client overrides GetSupportedCipherSuites to add ECDSA support
/// while keeping RSA as fallback.
/// </summary>
public sealed class GvDtlsSrtpClient : DtlsSrtpClient
{
    public GvDtlsSrtpClient()
        : base(null, null, SignatureAlgorithm.ecdsa, HashAlgorithm.sha256, null)
    {
    }

    protected override int[] GetSupportedCipherSuites()
    {
        // Google requires ECDSA cipher suites — include them first
        return
        [
            CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
            CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        ];
    }
}
