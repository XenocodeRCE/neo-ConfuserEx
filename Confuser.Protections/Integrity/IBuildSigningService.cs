namespace Confuser.Protections.Integrity
{
    /// <summary>
    ///     Service for signing the integrity manifest at build time.
    /// </summary>
    internal interface IBuildSigningService
    {
        /// <summary>Sign the payload and return the signature.</summary>
        byte[] Sign(byte[] payload);

        /// <summary>Get the public key for runtime verification (CSP blob).</summary>
        byte[] GetPublicKey();

        /// <summary>SHA-256 fingerprint of the public key (hex, for logging).</summary>
        string KeyFingerprint { get; }
    }
}
