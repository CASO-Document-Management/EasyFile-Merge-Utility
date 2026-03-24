---
name: easyfile-rest-api
description: >
  EasyFile REST API integration skill. Use when building applications that consume the
  EasyFile REST API, making HTTP calls to EasyFile endpoints, creating API clients,
  handling authentication with OAuth2/JWT/API keys, uploading or downloading documents,
  managing cabinets, baskets, records, fields, users, lookups, or any other EasyFile
  backend operation. Triggers on tasks involving EasyFile API calls, REST integration,
  HttpClient setup, document management workflows, or consuming EasyFile services from
  external applications.
---

# EasyFile REST API Integration Skill

Use this skill when building any application that needs to call the EasyFile REST API.
This covers authentication, all available endpoints, request/response formats, headers,
and ready-to-use C# client patterns.

## Quick Start

### 1. Base URL

The API is served behind a path base of `/spwsrest`. All routes are relative to this.

| Environment | Base URL |
|-------------|----------|
| Development | `https://localhost:7043/spwsrest` |
| Staging     | `https://{host}/spwsrest` |
| Production  | `https://{host}/spwsrest` |

Swagger UI is available at `{baseUrl}/swagger` in all environments.

### 2. Authentication

Two authentication methods are supported. Both can be used together.

**JWT Bearer Token** (primary, for user-context operations):
```
Authorization: Bearer {access_token}
```

**API Key** (for service-to-service / machine-to-machine):
```
X-API-Key: {api_key}
```

#### Obtaining a Token (OAuth2 Client Credentials)

```
POST {authBaseUrl}/api/v1/OAuth2/token
Content-Type: text/plain

grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}&profile_key={profileKey}
```

Response:
```json
{
  "access_token": "eyJhbG...",
  "refresh_token": "abc123...",
  "expires_in": "2026-02-18T12:00:00Z"
}
```

#### Refreshing a Token

```
POST {authBaseUrl}/api/v1/OAuth2/token
Content-Type: text/plain

grant_type=refresh_token&refresh_token={refreshToken}&client_id={clientId}&client_secret={clientSecret}
```

### 3. Standard Response Format

**Every** authenticated endpoint returns this wrapper:

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { }
}
```

Error responses:
```json
{
  "success": false,
  "message": "Descriptive error message",
  "data": null
}
```

**Always check `success` field** — an HTTP 200 can still have `"success": false`.

### 4. Required Headers

These custom headers are used across many endpoints:

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes | `Bearer {token}` — JWT access token |
| `X-API-Key` | Alt | API key (alternative to Bearer token) |
| `X-Logged-In-User` | Often | Username for audit trail tracking |
| `X-Profile-Key` | Sometimes | Database profile key |
| `X-Calling-App` | Sometimes | Calling application identifier |
| `X-Device-Type` | Optional | Device type (desktop, mobile, etc.) |
| `X-Trail-Name` | Sometimes | Audit trail name (e.g., `NewRecord`) |
| `X-EasyFile-Base-Url` | Upload | EasyFile base URL for upload callbacks |
| `X-Other-Info` | Optional | Additional context information |

### 5. HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created (with Location header) |
| 400 | Bad Request — validation error or missing parameters |
| 401 | Unauthorized — missing or invalid authentication |
| 403 | Forbidden — insufficient permissions |
| 404 | Not Found — resource does not exist |
| 409 | Conflict — e.g., document already checked out |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

---

## Endpoint Summary

All routes below are relative to the base URL (e.g., `https://host/spwsrest`).
Detailed endpoint documentation: [references/endpoints.md](references/endpoints.md)

### Health (no auth required)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/health` | Health status with version, environment |
| GET | `/api/v1/health/version` | Application version only |

### Documents (`/api/v1/documents`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{documentId}` | Get all versions of a document |
| GET | `/{id}/info` | Detailed document metadata |
| GET | `/{id}/download` | Download file stream (supports range requests) |
| GET | `/{id}/sas-url?expiryMinutes=60` | Azure SAS URL for direct access |
| POST | `/upload` | Upload documents (multipart, 1GB limit) |
| POST | `/delete-uploaded-files` | Clean up working folder |
| POST | `/` | Add new document |
| PUT | `/{documentId}` | Update document metadata |
| PUT | `/{documentId}/replace` | Replace document file content |
| DELETE | `/{documentId}` | Delete document |
| POST | `/{documentId}/copy` | Copy document |
| POST | `/{documentId}/checkout` | Lock for editing |
| POST | `/{documentId}/checkin` | Release lock with new version |
| POST | `/{documentId}/checkin-with-annotations` | Check in with annotations |
| POST | `/{documentId}/undo-checkout` | Release lock without saving |
| POST | `/{documentId}/rollback` | Rollback to previous version |
| POST | `/{documentId}/convert-to-pdf` | Convert to PDF |
| POST | `/{documentId}/linearize` | Linearize PDF for web viewing |
| POST | `/merge` | Merge multiple documents into one |
| POST | `/{documentId}/combine` | Combine documents into existing one |

### Cabinets (`/api/v1/cabinets`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/` | List cabinets for user (`?userId=`) |
| GET | `/{cabinetName}` | Get cabinet details |
| GET | `/bylabel/{label}` | Find cabinets by label |
| POST | `/byprivileges` | Filter cabinets by user privileges |
| POST | `/` | Create cabinet |
| PUT | `/{cabinetName}` | Update cabinet |
| POST | `/{sourceCabName}/copy` | Copy cabinet |
| POST | `/{cabinetName}/activate` | Activate cabinet |
| POST | `/{cabinetName}/deactivate` | Deactivate cabinet |
| GET | `/{cabinetName}/fields` | Get field definitions |
| POST | `/{cabinetName}/permissions` | Get user/group permissions |
| GET | `/{cabinetName}/permissions/codes` | Get permission codes |
| POST | `/{cabinetName}/access` | Grant user access |
| DELETE | `/{cabinetName}/access` | Remove user access |
| POST | `/{cabinetName}/data` | Search/paginate cabinet data |

### Cabinet Rules (`/api/v1/cabinets`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{cabinetName}/rules` | List rules |
| GET | `/rules/{ruleId}` | Get specific rule |
| POST | `/{cabinetName}/rules` | Create rule |
| PUT | `/rules/{ruleId}` | Update rule |
| DELETE | `/{cabinetName}/rules` | Delete rules |
| PUT | `/{cabinetName}/rules/reorder` | Reorder execution |
| POST | `/{cabinetName}/rules/execute` | Execute all rules |
| POST | `/{cabinetName}/rules/{ruleId}/execute` | Execute specific rule |

### Records (`/api/cabinets/{cabinetName}/records`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{docId}` | Get record by document ID |
| GET | `/by-unique/{uniqueNo}` | Get record by unique number |
| PUT | `/by-unique/{uniqueNo}` | Update record by unique number |
| PUT | `/bulk` | Bulk update records |
| DELETE | `/{docId}` | Soft delete record |
| POST | `/{docId}/restore` | Restore deleted record |
| POST | `/{docId}/reviewed` | Mark record as reviewed |

### Baskets (`/api/v1/baskets`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/` | List baskets for user |
| GET | `/{basketNo}` | Get basket by number |
| GET | `/by-name/{name}` | Get basket by name |
| POST | `/` | Create basket |
| PUT | `/{basketNo}` | Update basket |
| GET | `/{basketNo}/documents` | List basket documents |
| POST | `/{basketNo}/documents` | Upload to basket (multipart) |
| DELETE | `/{basketNo}/items/{itemId}` | Remove item from basket |
| POST | `/{basketNo}/access` | Set basket access |
| DELETE | `/{basketNo}/access` | Remove basket access |

### Fields (`/api/v1/fields`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/?cabinetName=` | Get fields for cabinet |
| GET | `/{fieldId}?cabinetName=` | Get field by ID |
| GET | `/by-label/{label}?cabinetName=` | Get field by label |
| POST | `/?cabinetName=` | Create field |
| PUT | `/{fieldId}?cabinetName=` | Update field |
| DELETE | `/{fieldId}?cabinetName=` | Delete field (soft) |
| POST | `/{fieldId}/restore?cabinetName=` | Restore field |
| PATCH | `/{fieldId}/properties?cabinetName=` | Update field properties |
| GET | `/{fieldId}/browse?cabinetName=` | Get distinct field values |

### Users (`/api/users`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/` | List all users |
| GET | `/{userId}` | Get user |
| GET | `/by-email/{email}` | Get user by email |
| POST | `/` | Create user |
| PUT | `/{userId}` | Update user |
| DELETE | `/{userId}` | Delete user |
| POST | `/{userId}/restore` | Restore deleted user |
| POST | `/{userId}/password` | Change password |
| GET | `/{userId}/permissions` | Get user permissions |
| POST | `/{userId}/permissions` | Set user permission |
| GET | `/{userId}/groups` | Get user groups |
| GET | `/{userId}/cabinets` | Get user cabinets |
| GET | `/{userId}/preferences` | Get user preferences |
| POST | `/{userId}/preferences` | Set user preference |

### Search (`/api/v1/search`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/saved` | Get saved searches |
| POST | `/saved` | Save search |
| DELETE | `/saved/{searchId}` | Delete saved search |
| GET | `/filters/favorites` | Get favorite filters |

### Lookups (`/api/v1/lookup`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/tables` | List lookup tables |
| GET | `/tables/{table}/{column}/data` | Get lookup data (paginated) |
| POST | `/tables` | Create lookup table |
| POST | `/tables/{table}/data` | Add lookup data |
| GET | `/autofill?fieldId=&searchValue=` | Autofill suggestions |
| GET | `/cabinets/{cab}/fields/{field}/list` | Lookup list for field |
| GET | `/tables/{table}/{column}/count` | Count distinct values |
| GET | `/tables/{table}/{column}/browse` | Browse with visible columns |

### Audit (`/api/v1/audit`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/trail` | Get audit trail entries |
| POST | `/trail` | Record user activity |
| POST | `/log` | Record log entry |

### Retention (`/api/v1/retention`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/rules` | Get all retention rules |
| GET | `/cabinets/{cabinetName}/rules` | Get cabinet retention rules |
| POST | `/rules` | Create retention rule |
| PUT | `/rules/{ruleId}` | Update retention rule |
| DELETE | `/rules/{ruleId}` | Delete retention rule |

### AI (`/api/v1`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/QueryAI` | Query AI (deprecated, use POST) |
| POST | `/QueryAI` | Query AI with content context |
| POST | `/AIRequest` | Process content with AI |
| POST | `/AIRequestUpload` | Process uploaded file with AI |
| DELETE | `/ClearConversation/{clientId}` | Clear AI conversation |
| GET | `/GetAIModels` | List available AI models |
| GET | `/TestAIConnection` | Test AI connectivity |

### System (`/api/v1/system`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/version` | System version (no auth) |
| GET | `/config` | System configuration |
| GET | `/config/database` | Database configuration |
| POST | `/cache/clear` | Clear system cache |
| GET | `/datetime` | Get server date/time |
| POST | `/azure/cors` | Setup Azure CORS |

### Settings (`/api/v1/settings`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/` | List settings (`?component=&category=` optional filters) |
| GET | `/{component}/{key}` | Get single setting value (decrypts encrypted values, falls back to Global component) |
| POST | `/` | Create new setting |
| PUT | `/{component}/{key}` | Upsert setting (create or update) |
| DELETE | `/{component}/{key}` | Delete setting (rejects read-only settings) |
| GET | `/components` | List all distinct component names |
| GET | `/categories` | List all distinct category names |
| POST | `/cache/invalidate` | Manually invalidate settings cache |

**Notes:**
- 5-minute in-memory cache; auto-invalidated on create/update/delete, or manually via `/cache/invalidate`
- Encrypted values are masked as `"********"` in list responses; decrypted only via the single-setting GET
- Settings not found for a specific component fall back to the `Global` component
- Read-only settings cannot be updated or deleted (returns 409 Conflict)

### File Storage (`/api/v1/filestorage`)
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/upload` | Upload to temp storage |
| GET | `/content/{fileId}` | Get file content |
| GET | `/info/{fileId}` | Get file metadata |
| DELETE | `/{fileId}` | Delete file |

### Workflows (`/api/workflows`)
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/{workflowId}` | Get workflow |
| POST | `/` | Create workflow |
| PUT | `/{workflowId}` | Update workflow |
| DELETE | `/{workflowId}` | Delete workflow |

### PDF Docs (`/api/v1`, no auth required)
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/PrintPDFDoc` | Print document to PDF |
| POST | `/ExportPDFDoc/{appId}` | Export to PDF |
| GET | `/GetPDFDocStatus/{jobId}` | Check PDF generation status |
| GET | `/GetPDFDoc/{jobId}` | Get PDF URL |
| GET | `/document/{filename}` | Download generated PDF |
| DELETE | `/CancelPDFDoc/{jobId}` | Cancel PDF generation |

---

## Key Data Models

### SettingResponse (list endpoint)
```json
{
  "settingId": 1, "component": "WebApp", "settingKey": "MaxUploadSize",
  "settingValue": "52428800", "isEncrypted": false,
  "category": "Upload", "displayName": "Max Upload Size",
  "description": "Maximum file upload size in bytes",
  "isReadonly": false, "dataType": "integer", "sortOrder": 10,
  "updatedBy": "admin", "updatedAt": "2026-02-18T...", "createdAt": "2026-02-18T..."
}
```
Note: `settingValue` is `"********"` when `isEncrypted` is `true`.

### SettingValueResponse (single-setting endpoint)
```json
{
  "settingId": 1, "component": "WebApp", "settingKey": "MaxUploadSize",
  "value": "52428800", "isEncrypted": false,
  "dataType": "integer", "isReadonly": false
}
```
Note: `value` is the **decrypted** plaintext for encrypted settings.

### CreateSettingRequest
```json
{
  "component": "WebApp", "settingKey": "MaxUploadSize",
  "settingValue": "52428800", "category": "Upload",
  "displayName": "Max Upload Size",
  "description": "Maximum file upload size in bytes",
  "dataType": "integer", "isEncrypted": false,
  "isReadonly": false, "sortOrder": 10, "updatedBy": "admin"
}
```

### UpsertSettingRequest
```json
{
  "value": "104857600",
  "isEncrypted": false,
  "updatedBy": "admin"
}
```

### Attr (Document Attribute / Index Value)
```json
{ "columnName": "FieldName", "value": "FieldValue" }
```

### DocumentResponse
```json
{
  "id": "string", "batchNo": 0, "cabinet": "string",
  "comments": "string", "docID": 0, "docSize": 0,
  "extension": "string", "fileFormat": "string",
  "fileName": "string", "fileOrderNo": 0,
  "lastAccessed": "datetime", "originalFilename": "string",
  "pageCount": 0, "state": "string", "user": "string",
  "checkedOut": "string", "revisionNo": "string",
  "revisionComment": "string"
}
```

### DocumentResponseDetails
Extends DocumentResponse with: `filePath`, `checksum`, `encryptionKey`,
`publishID`, `method`, `checkedOutTime`, `revisionLink`,
`actualDocumentStatus`, `storageInfo`, `isActiveVersion`, `received`,
`rowID`, `basketNumber`, `descriptorID`, `fileVolume`, `linearized`.

### DocumentSasUrlResponse
```json
{ "url": "https://blob.storage...", "expiresInMinutes": 60 }
```

### DocumentUploadResponse
```json
{ "docId": 0, "cabinet": "string", "documents": [...] }
```

### CabinetResponse
```json
{
  "cabinetName": "FT_XXX", "cabinetLabel": "Display Name",
  "saveRevisions": 1, "noOfRevisions": 10,
  "scanPath": "string", "clearAfterSave": false,
  "scanVolume": "string", "scanDirectory": "string",
  "scanPathOverride": false, "forceUNC": false,
  "gatewayLinkEnabled": false, "encryptionEnabled": false,
  "uploadSizeLimit": 0, "webFolderReadOnly": false,
  "webRequireDocument": false, "documentsPerFolder": 0,
  "uploadFields": 0, "hitlistRecords": 0,
  "pdfFormsEnabled": false, "fullTextEnabled": false,
  "isActive": true, "allowedFileTypes": "string",
  "serializedDataRow": [{ "fieldName": "...", "value": "..." }],
  "properties": [{ "fieldName": "...", "value": "..." }]
}
```

### DataResultResponse
```json
{
  "data": [{ "Field1": "value", "Field2": "value" }],
  "fields": [{ /* FieldResponse objects */ }],
  "totalCount": 150,
  "sql": "string",
  "sqlFields": "string",
  "searchFilterSql": "string"
}
```

### FetchCabinetDataRequest
```json
{
  "userId": "string",
  "start": 0,
  "length": 10,
  "searchText": "string",
  "searchKey": "string",
  "advanceSearch": "string",
  "orderBy": "string",
  "orderDir": "asc",
  "gateway": "string",
  "gatewayCriteria": "string",
  "fullTextSearchType": "string",
  "appName": "string",
  "searchOption": "string"
}
```

---

## C# Client Integration Pattern

Full client integration guide: [references/client-integration.md](references/client-integration.md)

### Minimal Setup

```csharp
// 1. Create HttpClients for auth and API
var authClient = new HttpClient { BaseAddress = new Uri(authBaseUrl) };
var apiClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };

// 2. Create TokenManager and authenticate
var tokenManager = new TokenManager(
    apiClient, authClient, apiKey, clientId, clientSecret, profileKey, "v1");
await tokenManager.LoginAsync();
tokenManager.CurrentUser = "username";

// 3. Make API calls — token is automatically set on apiClient
var response = await apiClient.GetAsync("/api/v1/cabinets?userId=username");
var json = await response.Content.ReadAsStringAsync();
var result = JsonConvert.DeserializeObject<ApiResponse<object>>(json);
if (result.Success) { /* use result.Data */ }
```

### Document Upload Example

```csharp
using var content = new MultipartFormDataContent();
content.Add(new StringContent("FT_CABINET"), "cabinet");
content.Add(new StringContent(JsonConvert.SerializeObject(
    new[] { new { columnName = "Field1", value = "Value1" } }
)), "attributes");
content.Add(new StringContent(JsonConvert.SerializeObject(
    new[] { "File comment" }
)), "fileComments");

var fileBytes = File.ReadAllBytes(filePath);
var fileContent = new ByteArrayContent(fileBytes);
fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
content.Add(fileContent, "files", Path.GetFileName(filePath));

var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/documents/upload");
request.Content = content;
request.Headers.Add("X-Logged-In-User", "username");
request.Headers.Add("X-EasyFile-Base-Url", "https://easyfile.host/spwsrest");

var response = await apiClient.SendAsync(request);
```

---

## Common Patterns

### Pagination
Endpoints returning lists use `start`/`length` parameters in the request body:
```json
{ "start": 0, "length": 25 }
```

### Error Handling
Always check both HTTP status and the `success` field:
```csharp
if (!response.IsSuccessStatusCode)
{
    var error = await response.Content.ReadAsStringAsync();
    // Parse error message from JSON
}
var result = JsonConvert.DeserializeObject<ApiResponse<T>>(content);
if (!result.Success)
{
    // Handle API-level error via result.Message
}
```

### Token Refresh
Call `EnsureValidTokenAsync()` before each request. It auto-refreshes
if the token expires within 5 minutes.

---

## Source Code Reference

The EasyFile.Rest API source is at:
- **Controllers**: `src/EasyFile.Rest/Controllers/v1/`
- **Models/DTOs**: `src/EasyFile.Rest/Models/`
- **Data Access**: `src/EasyFile.Rest/DataAccess/`
- **Business Logic**: `src/EasyFile.Rest/BusinessLogic/`
- **Helpers**: `src/EasyFile.Rest/Helpers/`
- **Configuration**: `src/EasyFile.Rest/Program.cs`
