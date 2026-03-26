# Server-Side Fixes Required: Checkout/Checkin Endpoints

## Overview

The `POST /api/v1/documents/{documentId}/checkout` and `POST /api/v1/documents/{documentId}/checkin` endpoints in SpwsRestApi have bugs that cause 500 errors when called from the Merge Utility.

**Project:** `E:\codeprojects\easyfile-platform\src\SpwsRestApi`

---

## Bug 1: Checkout returns 500 — User identity not resolved

**File:** `DataAccess/DocumentDataAccess.cs` — `CheckOutDocument` method (~line 901)

**Problem:** The checkout method gets the current user from:
```csharp
string user = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
```

When called via API Key + Bearer token (OAuth2 client credentials), `Identity.Name` may be null or cause an exception depending on how the claims are mapped. The SQL then fails because it tries to insert a null or improperly resolved user into the `CHECKED_OUT` column.

**Fix:** Use the `X-Logged-In-User` header as a fallback:
```csharp
string user = _httpContextAccessor.HttpContext?.User?.Identity?.Name
    ?? _httpContextAccessor.HttpContext?.Request.Headers["X-Logged-In-User"].FirstOrDefault()
    ?? "System";
```

---

## Bug 2: CheckIn uses wrong column — `ID` vs `DOC_ID`

**File:** `DataAccess/DocumentDataAccess.cs` — `CheckInDocument` method (~line 986)

**Problem:** CheckIn calls `GetDocumentInfo(documentId)`, but `GetDocumentInfo` queries by the auto-increment `ID` column:
```csharp
string query = $"SELECT * FROM DOC_ID1 WHERE ID = {id}";
```

The `documentId` parameter passed in is actually a `DOC_ID` (business document ID), not the auto-increment `ID`. These are different values — for example, DOC_ID `39293` has table ID `30992`.

**Result:** `GetDocumentInfo` doesn't find the document, throws `FileNotFoundException`, and the controller returns a 500.

**Fix:** Change the query to use `DOC_ID`:
```csharp
string query = $"SELECT * FROM DOC_ID1 WHERE DOC_ID = {id} AND IS_ACTIVE_VERSION = 'Y'";
```

Or create a separate method that queries by DOC_ID for use in checkout/checkin flows.

---

## Bug 3: IIS rejects POST with no body (HTTP 411)

**Symptom:** Checkout returns `411 Length Required` when called without a request body.

**Cause:** IIS requires a `Content-Length` header on POST requests. When the client sends a POST with no body and no `Content-Length: 0` header, IIS rejects it before it reaches the controller.

**Fix (client-side, already applied):** Send an empty body with the POST request. This is handled in the Merge Utility's `DocumentService.CheckoutAsync`.

**Fix (server-side alternative):** Add `[AllowEmptyBody]` or configure IIS to allow zero-length POSTs.

---

## How to Verify Fixes

### 1. Test Checkout via curl
```bash
TOKEN=$(curl -s -X POST "https://easyfiledev1.caso.com/spwsrestauth/api/v1/OAuth2/token" \
  -H "Content-Type: text/plain" \
  -d "grant_type=client_credentials&client_id=YOUR_CLIENT_ID&client_secret=YOUR_SECRET&profile_key=easyfileaz" \
  | python -c "import sys,json; print(json.load(sys.stdin)['access_token'])")

curl -s -X POST "https://easyfiledev1.caso.com/spwsrest/api/v1/documents/39293/checkout" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-API-Key: YOUR_API_KEY" \
  -H "X-Logged-In-User: Arimal" \
  -H "X-Profile-Key: easyfileaz" \
  -H "Content-Length: 0"
```

**Expected:** `{"success":true,"message":"Document checked out successfully","data":{...}}`

### 2. Test Undo Checkout
```bash
curl -s -X POST "https://easyfiledev1.caso.com/spwsrest/api/v1/documents/39293/undo-checkout" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-API-Key: YOUR_API_KEY" \
  -H "X-Logged-In-User: Arimal" \
  -H "X-Profile-Key: easyfileaz" \
  -H "Content-Length: 0"
```

### 3. Test Checkin
```bash
curl -s -X POST "https://easyfiledev1.caso.com/spwsrest/api/v1/documents/39293/checkin" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-API-Key: YOUR_API_KEY" \
  -H "X-Logged-In-User: Arimal" \
  -H "X-Profile-Key: easyfileaz" \
  -F "File=@test.pdf" \
  -F "Comment=Test checkin" \
  -F "CreateNewVersion=true" \
  -F "UserId=Arimal"
```

### 4. Run Merge Utility end-to-end
```bash
dotnet run --project src/MergeUtility.Console
```

Should show: `[Success] 1500-PW 5225-02_LF002.pdf → DocId: 39293`

---

## Files to Modify

| File | Method | Issue |
|------|--------|-------|
| `DataAccess/DocumentDataAccess.cs` | `CheckOutDocument` | User identity resolution (Bug 1) |
| `DataAccess/DocumentDataAccess.cs` | `GetDocumentInfo` | `WHERE ID` should be `WHERE DOC_ID` (Bug 2) |
| `DataAccess/DocumentDataAccess.cs` | `CheckInDocument` | Calls `GetDocumentInfo` with DOC_ID (Bug 2) |

---

## Interim Workaround

Until these fixes are deployed, the Merge Utility can fall back to the `PUT /documents/{versionId}/replace` endpoint, which works correctly but does not lock the document or create versioned history.
