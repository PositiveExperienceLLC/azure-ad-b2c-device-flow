using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ltwlf.Azure.B2C
{
    public class SignInRedirect
    {
        private readonly IAuthorizationStateStore _store;
        private readonly ILogger<SignInRedirect> _log;
        private readonly PageFactory _pageFactory;
        private readonly ConfigOptions _config;

        public SignInRedirect(IAuthorizationStateStore store, IOptions<ConfigOptions> options, PageFactory pageFactory, ILogger<SignInRedirect> log)
        {
            _store = store;
            _log = log;
            _pageFactory = pageFactory;
            _config = options.Value;
        }

        [Function("user_code_get")]
        public IActionResult Get(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req)
        {
            return _pageFactory.GetPageResult(PageFactory.PageType.UserCode);
        }

        [Function("user_code_post")]
        public async Task<IActionResult> Post(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest req)
        {
            var useAjax = req.Headers.TryGetValue("x-use-ajax", out var useAjaxHeader) &&
                          String.Equals(useAjaxHeader, "true", StringComparison.OrdinalIgnoreCase);

            try
            {
                var userCode = (await req.ReadFormAsync())["user_code"].ToString();

                var authState = _store.GetByUserCode(userCode)
                    ?? throw new InvalidOperationException("Unknown or expired user code");

                var tenant = _config.Tenant;
                var signInFlow = _config.SignInPolicy;
                var appId = _config.AppId;
                var redirectUri = HttpUtility.UrlEncode(_config.RedirectUri);
                var scope = authState.Scope ?? "openid";
                string codeChallenge;
                using (var sha256 = SHA256.Create())
                {
                    var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(authState.CodeVerifier));
                    codeChallenge = Base64UrlTextEncoder.Encode(challengeBytes);
                }

                var url = $"https://{_config.Tenant}.b2clogin.com/{_config.Tenant}.onmicrosoft.com/{signInFlow}/oauth2/v2.0/authorize?client_Id={appId}&redirect_uri={redirectUri}&scope={scope}&state={authState.UserCode}&nonce=defaultNonce&response_type=code&prompt=login&response_mode=form_post&code_challenge={codeChallenge}&code_challenge_method=S256";

                return useAjax
                    ? (IActionResult) new ContentResult {Content = url, ContentType = "text/plain"}
                    : new RedirectResult(url);
            }
            catch (Exception e)
            {
                _log.LogInformation(e.Message);

                if (useAjax)
                {
                    return new BadRequestResult();
                }

                return _pageFactory.GetPageResult(PageFactory.PageType.Error);
            }
        }
    }
}
