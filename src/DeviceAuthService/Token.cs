using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace Ltwlf.Azure.B2C
{
    public class Token
    {
        private class TokenResponse
        {
            [JsonProperty("access_token")] public string AccessToken { get; set; }
            [JsonProperty("token_type")] public string TokenType { get; set; }
            [JsonProperty("expires_in")] public int ExpiresIn { get; set; }
            [JsonProperty("refresh_token")] public string RefreshToken { get; set; }
            [JsonProperty("scope")] public string Scope { get; set; }
            [JsonProperty("resource")] public string Resource { get; set; }
        }

        private readonly IAuthorizationStateStore _store;
        private readonly HttpClient _client;
        private readonly ConfigOptions _config;

        public Token(IAuthorizationStateStore store, IHttpClientFactory httpClientFactory, IOptions<ConfigOptions> options)
        {
            _store = store;
            _client = httpClientFactory.CreateClient();
            _config = options.Value;
        }

        [Function("token")]
        public async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "oauth/token")]
            HttpRequest req)
        {
            var deviceCode = req.Query["device_code"].SingleOrDefault();
            var grantType = req.Query["grant_type"].SingleOrDefault();
            if(grantType == null)
            {
                grantType = req.Form["grant_type"];
            }    
            var clientId = req.Query["client_id"].SingleOrDefault();

            if (grantType == null)
                return new BadRequestObjectResult("grant_type are mandatory");

            /*
            if (!grantType.Equals("urn:ietf:params:oauth:grant-type:device_code", StringComparison.OrdinalIgnoreCase) || !grantType.Equals("refresh_token", StringComparison.OrdinalIgnoreCase) )
                return new BadRequestObjectResult("gran_type must be \"urn:ietf:params:oauth:grant-type:device_code\"");
            */
            if (grantType.Equals("refresh_token", StringComparison.OrdinalIgnoreCase))
            {
                var scope = req.Form["scope"].SingleOrDefault() ?? "openid";
                var refreshToken = req.Form["refresh_token"];
                
                var b2CTokenResponse = await _client.PostAsync(Helpers.GetTokenEndpoint(_config),
                    new StringContent(
                            $"grant_type=refresh_token&refresh_token={refreshToken}&client_id={_config.AppId}&client_secret={_config.AppSecret}&scope={scope}&")
                        {Headers = {ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")}});

                if (b2CTokenResponse.IsSuccessStatusCode)
                {
                    var jwt = JObject.Parse(await b2CTokenResponse.Content.ReadAsStringAsync());

                    var accessTokenResponse = new TokenResponse
                    {
                        AccessToken = jwt.Value<string>("access_token"),
                        RefreshToken = jwt.Value<string>("refresh_token"),
                        ExpiresIn = jwt.Value<int>("expires_in"),
                        TokenType = jwt.Value<string>("token_type"),
                        Scope = jwt.Value<string>("scope"),
                        Resource = jwt.Value<string>("resource")
                    };

                    return Helpers.Json(accessTokenResponse);
                }
                else
                {
                    return Helpers.Json(new { Message = b2CTokenResponse.ReasonPhrase }, 400);
                }
            }
            
            var authState = _store.GetByDeviceCode(deviceCode);
            if (authState == null) return new BadRequestObjectResult("expired_token");

            if (authState.AccessToken == null) return new BadRequestObjectResult("authorization_pending");

            var tokenResponse = new TokenResponse
            {
                AccessToken = authState.AccessToken,
                RefreshToken = authState.RefreshToken,
                ExpiresIn = authState.ExpiresIn,
                TokenType = authState.TokenType,
                Scope =  authState.Scope
            };

            return Helpers.Json(tokenResponse);
        }
    }
}
