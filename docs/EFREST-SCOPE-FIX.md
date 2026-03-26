# Fix: OAuth2 Scope Enforcement Blocking API Clients

## Problem

The `/efrest` deployment enforces OAuth2 scopes on controller endpoints, but the OAuth2 token endpoint (`/spwsrestauth`) issues JWTs with **no `scope` claim at all** for `client_credentials` grants. The JWT payload only contains:

```json
{ "ProfileKey": "easyfileaz", "exp": 1774401662 }
```

`CustomAuthorizeAttribute.ValidateScopes` calls `user.FindAll("scope")` which returns an empty list. Since there are no scopes to match against the required scopes (`cabinets:read`, `documents:write`, etc.), every request returns **403 Insufficient permissions**.

## Root Cause

1. The OAuth2 token endpoint does **not** include a `scope` claim in the JWT
2. `ValidateScopes` finds zero scopes on the token → no match against required scopes → 403
3. The current "api" wildcard fix doesn't help because there's no `"api"` claim in the token to match

## Fix

**File:** `E:\codeprojects\easyfile-platform\src\SpwsRestApi\Attributes\CustomAuthorizeAttribute.cs`

In the `ValidateScopes` method (~line 571), add a fallback after extracting user scopes. If the token was issued by the trusted auth server but has no scope claims, treat it as having `"api"` scope:

```csharp
private bool ValidateScopes(
    AuthorizationFilterContext context,
    ClaimsPrincipal user,
    ILogger logger,
    out IActionResult result)
{
    result = null;

    // Get user scopes from JWT token
    var userScopes = user.FindAll("scope")
        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    // *** ADD THIS BLOCK ***
    // Tokens from the trusted OAuth2 endpoint may not include a scope claim.
    // Treat authenticated tokens with no scopes as having implicit "api" scope.
    if (userScopes.Count == 0 && user.Identity?.IsAuthenticated == true)
    {
        logger.LogDebug("Token has no scope claims — granting implicit 'api' scope");
        userScopes.Add("api");
    }

    // ... rest of method unchanged
```

Then the existing "api" wildcard check (already deployed) will match and grant access.

**Note:** Both changes are required together:
1. The implicit `"api"` scope fallback (this fix)
2. The `"api"` wildcard acceptance in the scope matching (already deployed in 20:14 build)

## Alternative Fix (simpler)

If you prefer not to add an implicit scope, skip scope validation entirely when the token has no scope claims:

```csharp
// If token has no scope claims, skip scope validation
// (token was authenticated via JWT validation, which is sufficient)
if (userScopes.Count == 0)
{
    logger.LogDebug("Token has no scope claims — skipping scope validation");
    return true;
}
```

This is simpler but less explicit. The implicit `"api"` approach above is preferred because it still goes through the normal scope matching logic.

## Verification

```bash
# Get a token (note: no scope claim in JWT)
TOKEN=$(curl -s -X POST "https://easyfiledev1.caso.com/spwsrestauth/api/v1/OAuth2/token" \
  -H "Content-Type: text/plain" \
  -d "grant_type=client_credentials&client_id=EasyFile-a884bc3d-0e6f-42b9-917f-34c5b968b62e&client_secret=YOUR_SECRET&profile_key=easyfileaz" \
  | python -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

# Verify token has no scope claim:
echo "$TOKEN" | cut -d. -f2 | python -c "
import sys,base64,json
p=sys.stdin.read().strip()
p+='='*(4-len(p)%4)
print(json.dumps(json.loads(base64.urlsafe_b64decode(p)),indent=2))"

# Should return 200 with cabinet list after fix:
curl -s "https://easyfiledev1.caso.com/efrest/api/v1/cabinets?userId=Arimal" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-API-Key: YOUR_API_KEY" \
  -H "X-Profile-Key: easyfileaz"
```

## Long-term Fix

Update the OAuth2 token endpoint (`/spwsrestauth`) to include a `scope` claim in the JWT. For `client_credentials` grants, this should be based on the client's configured permissions. Example JWT payload:

```json
{
  "ProfileKey": "easyfileaz",
  "scope": "api cabinets:read cabinets:write documents:read documents:write",
  "exp": 1774401662
}
```

This would allow fine-grained per-client scope control without needing the implicit fallback.
