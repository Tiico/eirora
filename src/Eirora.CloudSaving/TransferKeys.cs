using System;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Eirora.CloudSaving
{
    /// <summary>
    /// A one-time X25519 key pair for one key transfer (ADR-0008, TASK-023b): the new phone makes one, shows its public
    /// key in the ticket, and opens the sealed contribution key with the private one. It is never stored.
    /// </summary>
    public sealed class TransferKeys
    {
        public const int PublicKeyLength = 32;

        private readonly X25519PrivateKeyParameters _private;

        private TransferKeys(X25519PrivateKeyParameters privateKey)
        {
            _private = privateKey;
            PublicKey = privateKey.GeneratePublicKey().GetEncoded();
        }

        /// <summary>The public key, 32 bytes, as the ticket carries it.</summary>
        public byte[] PublicKey { get; }

        public static TransferKeys Generate() => new(new X25519PrivateKeyParameters(new SecureRandom()));

        /// <summary>A pair from a known private key, for tests.</summary>
        internal static TransferKeys FromPrivateKey(byte[] privateKey) => new(new X25519PrivateKeyParameters(privateKey));

        /// <summary>The shared secret with <paramref name="otherPublicKey"/>, or null when the key is not a valid point.</summary>
        internal byte[]? SharedSecretWith(byte[] otherPublicKey)
        {
            if (otherPublicKey is null || otherPublicKey.Length != PublicKeyLength)
            {
                return null;
            }

            var secret = new byte[X25519PrivateKeyParameters.SecretSize];
            try
            {
                _private.GenerateSecret(new X25519PublicKeyParameters(otherPublicKey), secret, 0);
            }
            catch (InvalidOperationException)
            {
                return null; // A low-order point: the agreement would be the same for everyone.
            }

            foreach (byte b in secret)
            {
                if (b != 0)
                {
                    return secret;
                }
            }

            return null;
        }
    }
}
