using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using  System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Ltwlf.Azure.B2C
{
    public class DeviceAuthorization
    {
        class AuthorizationResponse
        {
            [JsonProperty("device_code")] public string DeviceCode { get; set; }
            [JsonProperty("user_code")] public string UserCode { get; set; }
            [JsonProperty("verification_uri")] public string VerificationUri { get; set; }
            [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
        }

        private readonly IConnectionMultiplexer _muxer;

        private readonly ConfigOptions _config;

        public DeviceAuthorization(IConnectionMultiplexer muxer, IOptions<ConfigOptions> options)
        {
            _muxer = muxer;
            _config = options.Value;
        }

        [FunctionName("device_authorization")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "oauth/device_authorization")]
            HttpRequest req,
            ILogger log)
        {
            log.LogInformation("DeviceAuthorization function is processing a request.");

            if (req.ContentLength == null || !req.ContentType.Equals("application/x-www-form-urlencoded",
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException("Request content type must be application/x-www-form-urlencoded");
            }

            if (!req.Form.TryGetValue("clientId", out var clientId))
            {
                throw new ArgumentException("ClientId is missing!");
            }

            var random = new Random();
            int codeVerifierLength = random.Next(43, 128);
            var bytes = new byte[codeVerifierLength];
            var cryptoRandom = RandomNumberGenerator.Create();
            cryptoRandom.GetBytes(bytes);

            // It is recommended to use a URL-safe string as code_verifier.
            // See section 4 of RFC 7636 for more details.
            var codeVerifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var authState = new AuthorizationState()
            {
                DeviceCode = CreateSecureRandomString(),
                ClientId = clientId,
                UserCode = CreateSecureRandomString(_config.UserCodeLength),
                ExpiresIn = 300,
                VerificationUri = _config.VerificationUri,
                Scope = req.Form?["scope"],
                CodeVerifier = codeVerifier
            };

            var response = new AuthorizationResponse()
            {
                DeviceCode = authState.DeviceCode,
                UserCode = authState.UserCode,
                ExpiresIn = authState.ExpiresIn,
                VerificationUri = authState.VerificationUri
            };

            _muxer.GetDatabase().StringSet($"{authState.DeviceCode}:{authState.UserCode}",
                JsonConvert.SerializeObject(authState), new TimeSpan(0, 0, authState.ExpiresIn));

            return new OkObjectResult(response);
        }

        public static string CreateSecureRandomString(int count = 64)
        {
            var bytes = new byte[count];
            var cryptoRandom = RandomNumberGenerator.Create();
            cryptoRandom.GetBytes(bytes);

            // It is recommended to use a URL-safe string as code_verifier.
            // See section 4 of RFC 7636 for more details.
            var randomString = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return randomString;
        }
    }
}