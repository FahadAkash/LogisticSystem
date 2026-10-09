using System.Security.Cryptography;
using LogisticServer.Application.DTOs.Auth;
using LogisticServer.Application.Interfaces;
using LogisticServer.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LogisticServer.Infrastructure.Security;

public class RsaKeyService : IJwtKeyService
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _privateKey;
    private readonly RsaSecurityKey _publicKey;
    private readonly JwksResponse _jwks;
    private readonly string _keyId;

    public string KeyId => _keyId;

    public RsaKeyService(JwtConfig config, ILogger<RsaKeyService> logger)
    {
        _keyId = string.IsNullOrWhiteSpace(config.KeyId) ? "logistic-auth-key-1" : config.KeyId;
        _rsa = RSA.Create(2048);

        // 1. Try loading from PEM config
        if (!string.IsNullOrWhiteSpace(config.RsaPrivateKeyPem))
        {
            logger.LogInformation("Loading RSA private key from configuration PEM.");
            _rsa.ImportFromPem(config.RsaPrivateKeyPem);
        }
        else
        {
            // 2. Try loading from disk or generate new key
            var keyDir = Path.IsPathRooted(config.KeyDirectory) 
                ? config.KeyDirectory 
                : Path.Combine(AppContext.BaseDirectory, config.KeyDirectory);
            
            var privateKeyPath = Path.Combine(keyDir, "rsa_private.pem");
            var publicKeyPath = Path.Combine(keyDir, "rsa_public.pem");

            if (File.Exists(privateKeyPath))
            {
                logger.LogInformation("Loading existing RSA key pair from {Path}", privateKeyPath);
                var pem = File.ReadAllText(privateKeyPath);
                _rsa.ImportFromPem(pem);
            }
            else
            {
                logger.LogInformation("Generating new 2048-bit RSA key pair for asymmetric JWT signing.");
                Directory.CreateDirectory(keyDir);
                var privatePem = _rsa.ExportPkcs8PrivateKeyPem();
                var publicPem = _rsa.ExportSubjectPublicKeyInfoPem();
                File.WriteAllText(privateKeyPath, privatePem);
                File.WriteAllText(publicKeyPath, publicPem);
                logger.LogInformation("RSA key pair persisted to {KeyDir}", keyDir);
            }
        }

        // Create security keys
        _privateKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };
        
        var pubParameters = _rsa.ExportParameters(false);
        var pubRsa = RSA.Create(pubParameters);
        _publicKey = new RsaSecurityKey(pubRsa) { KeyId = _keyId };

        // Precompute JWKS for RFC 7517 compliance
        var n = Base64UrlEncoder.Encode(pubParameters.Modulus!);
        var e = Base64UrlEncoder.Encode(pubParameters.Exponent!);

        var jwk = new JwkKeyDto(
            Kty: "RSA",
            Use: "sig",
            Alg: "RS256",
            Kid: _keyId,
            N: n,
            E: e
        );

        _jwks = new JwksResponse(new List<JwkKeyDto> { jwk });
        logger.LogInformation("JWKS precomputed with KeyId {KeyId}", _keyId);
    }

    public RsaSecurityKey GetPrivateKey() => _privateKey;
    public RsaSecurityKey GetPublicKey() => _publicKey;
    public JwksResponse GetJwks() => _jwks;
}
