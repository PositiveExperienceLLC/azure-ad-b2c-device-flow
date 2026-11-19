_Work in progress_...

# Device Flow Authentication

## Example local config

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Config:AppId": "",
    "Config:AppSecret": "",
    "Config:Tenant": "myb2c",
    "Config:RedirectUri": "http://localhost:7071/api/authorization_callback",
    "Config:SignInPolicy": "B2C_1A_signup_signin",
    "Config:VerificationUri": "http://localhost:7071",
    // optional: configure custom html pages (e.g. load from a blob storage)
    "Config:UserCodePage": "http://localhost:8080/usercode.html",
    "Config:SuccessPage": "http://localhost:8080/success.html",
    "Config:ErrorPage": "http://localhost:8080/error.html"
  }
}
```


## Start on device

```
POST https://login.clir.io/api/oauth/device_authorization
Accept: */*
Content-Type: application/x-www-form-urlencoded
Cache-Control: no-cache

scope=https://cliriob2c.onmicrosoft.com/6ee20164-b269-441c-83a7-ff44f27f0d62/access_as_user
```

Example Response:

```
{
    "device_code": "2co08-hzi7PoJ0ZRuOx_e57BZXurHPKZdmRimxgw1hf73PdCex-cyQ_zni2mEgHH0g2ighOPlVKk0B2KV7MHoQ",
    "user_code": "OpYbIS",
    "verification_uri": "https://login.clir.io",
    "expires_in": 360
}

```

## Navigate to website

https://login.clir.io/

Enter User Code and login

### Poll for token on device

```
POST https://login.clir.io/api/oauth/token
Accept: */*
Content-Type: application/x-www-form-urlencoded
Cache-Control: no-cache

grant_type=urn:ietf:params:oauth:grant-type:device_code&device_code=2co08-hzi7PoJ0ZRuOx_e57BZXurHPKZdmRimxgw1hf73PdCex-cyQ_zni2mEgHH0g2ighOPlVKk0B2KV7MHoQ
```

Pending example response:

```
HTTP/1.1 400 Bad Request
Content-Length: 33
Content-Type: application/json; charset=utf-8
Set-Cookie: ARRAffinity=dd716a6def04e48f4e433f7740cecb7f8a4f1c77d318c5480b769fc5157ad936;Path=/;HttpOnly;Domain=service-device-auth-flow.azurewebsites.net
Date: Wed, 30 Sep 2020 12:37:11 GMT

{
  "value": "authorization_pending"
}
```

Expired token

```
HTTP/1.1 400 Bad Request
Content-Length: 13
Content-Type: text/plain; charset=utf-8
Set-Cookie: ARRAffinity=dd716a6def04e48f4e433f7740cecb7f8a4f1c77d318c5480b769fc5157ad936;Path=/;HttpOnly;Domain=service-device-auth-flow.azurewebsites.net
Date: Wed, 30 Sep 2020 12:44:33 GMT

expired_token
```

Token

```
HTTP/1.1 200 OK
Content-Type: text/plain; charset=utf-8
Vary: Accept-Encoding
Set-Cookie: ARRAffinity=dd716a6def04e48f4e433f7740cecb7f8a4f1c77d318c5480b769fc5157ad936;Path=/;HttpOnly;Domain=service-device-auth-flow.azurewebsites.net
Date: Wed, 30 Sep 2020 12:46:31 GMT

{
    "access_Token":"eyJraWQiOiJ2VE96SmhwS3dIeD....",
    "token_type":null,
    "expires_in":0,
    "refresh_token":null,
    "scope":nul
}

```

## Example custom user code page

```html

<html>
<body>
<div>
    <form id="form" method="post">
        <label for="user_code">User Code</label>
        <input name="user_code" autofocus="autofocus"/>
        <button type="submit">Send</button>
    </form>
</div>
</body>
</html>
```

Instead of submitting the form directly you can also use JavaScript to submit.

```javascript
const form = document.getElementById("form");
window
    .fetch("", {
        method: "post",
        "Content-Type": "application/x-www-form-urlencoded",
        body: new FormData(form),
        headers: {
            "x-use-ajax": true,
        },
    })
    .then((resp) => {
        if (resp.ok) {
            resp
                .text()
                .then((redirectUrl) => (window.location.href = redirectUrl));
        } else {
            alert("error")
        }
    })
````

The success and error pages are just static HTML pages.

## Production hosting

Device-code sign-in is used only by the Quest build. The `prd` function app (`das-prd-scirmdebbac4u`) runs on the WorkspaceService App Service plan (`wss-prd`: B1, one instance, Always On), alongside wss, tok, col, viw and web. It adds no hosting cost of its own.

**Pending sign-ins are held in memory** (`MemoryAuthorizationStateStore`), not in Redis. A device code lives for 6 minutes. The device-code and user-code lookups happen in the same process, which is safe only because the plan runs a single instance that never idles out. Things to know:

- A restart or deploy of das loses any sign-ins in progress. The user requests a new code on the Quest.
- Don't scale the `wss-prd` plan out to more than one instance, or move das to a Consumption or multi-instance plan, without first switching `IAuthorizationStateStore` to shared storage (for example Azure Table Storage).

**`login.clir.io`** is the `deviceCodeWeb` Static Web App. It serves the code-entry page, which posts to `/api/user_code_post`. The app must be on the **Standard** plan with das linked as its backend ("APIs" in the portal), so that `/api/*` reaches das. The Free plan can't link a backend.

Until November 2026 das ran on its own S1 plan, with a classic Basic Redis cache (`sitescan-cache-prd`) for pending sign-ins. Both were deleted, and classic Redis caches can no longer be created. das also moved from the in-process .NET 6 model, whose support ended on November 10, 2026, to the .NET 8 isolated worker.

The [November 2026 hosting changes](https://github.com/PositiveExperienceLLC/Shared/blob/development/docs/hosting-changes-2026-11.md) doc covers why this changed and how it fits with the other services.
