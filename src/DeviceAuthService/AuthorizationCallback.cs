using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ltwlf.Azure.B2C
{
    public class AuthorizationCallback
    {
        private readonly IAuthorizationStateStore _store;
        private readonly ILogger<AuthorizationCallback> _log;
        private readonly HttpClient _client;
        private readonly PageFactory _pageFactory;
        private readonly ConfigOptions _config;

        public AuthorizationCallback(IAuthorizationStateStore store, IHttpClientFactory httpClientFactory, IOptions<ConfigOptions> options, PageFactory pageFactory, ILogger<AuthorizationCallback> log)
        {
            _store = store;
            _log = log;
            _client = httpClientFactory.CreateClient();
            _pageFactory = pageFactory;
            _config = options.Value;
        }

        [Function("authorization_callback")]
        public async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "authorization_callback")]
            HttpRequest req)
        {
            _log.LogInformation("authorization_callback function processed a request.");

            if(req.Query.ContainsKey("error"))
            {
                return _pageFactory.GetPageResult(PageFactory.PageType.Error);
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            String code = HttpUtility.ParseQueryString(requestBody).Get("code");
            String userCode = HttpUtility.ParseQueryString(requestBody).Get("state");
                        
            var authState = _store.GetByUserCode(userCode);
            if (authState == null)
            {
                _log.LogWarning("Device authentication request expired");
                return _pageFactory.GetPageResult(PageFactory.PageType.Error);
            }

            var scope = authState.Scope ?? "openid";

            var tokenResponse = await _client.PostAsync(Helpers.GetTokenEndpoint(_config),
                new StringContent(
                        $"grant_type=authorization_code&client_id={_config.AppId}&client_secret={_config.AppSecret}&scope={scope}&code={code}&code_verifier={authState.CodeVerifier}")
                    {Headers = {ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")}});

            if (tokenResponse.IsSuccessStatusCode == false)
            {
                var errorContent = await tokenResponse.Content.ReadAsStringAsync();
                return new ContentResult() {Content = errorContent};
            }

            var jwt = JObject.Parse(await tokenResponse.Content.ReadAsStringAsync());

            authState.AccessToken = jwt.Value<string>("access_token");
            authState.RefreshToken = jwt.Value<string>("refresh_token");
            authState.ExpiresIn = jwt.Value<int>("expires_in");
            authState.TokenType = jwt.Value<string>("token_type");

            _store.Set(authState, TimeSpan.FromSeconds(_config.ExpiresIn));

            return new RedirectResult("../signinSuccess.html");
        }
    }
}
