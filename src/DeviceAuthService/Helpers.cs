using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Ltwlf.Azure.B2C
{
    public static class Helpers
    {
        public static string GetTokenEndpoint(ConfigOptions config)
        {
            return $"https://{config.Tenant}.b2clogin.com/{config.Tenant}.onmicrosoft.com/{config.SignInPolicy}/oauth2/v2.0/token";
        }

        // Serialize with Newtonsoft so the [JsonProperty] snake_case names the clients expect are kept
        // (the isolated worker's default formatter is System.Text.Json, which ignores them)
        public static IActionResult Json(object value, int statusCode = 200)
        {
            return new ContentResult
            {
                Content = JsonConvert.SerializeObject(value),
                ContentType = "application/json; charset=utf-8",
                StatusCode = statusCode
            };
        }
    }
}
