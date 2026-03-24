# EasyFile REST API — Complete Endpoint Reference

All routes are relative to the base URL with path base `/spwsrest`.
Example: `https://host/spwsrest/api/v1/documents/123/info`

---

## Health Controller

**Route prefix**: `api/v1/health` | **Auth**: None

### GET `/api/v1/health`
Returns application health status.
- **Response**: `{ status, version, timestamp, environment }`
- **Status codes**: 200, 503

### GET `/api/v1/health/version`
Returns application version only.
- **Response**: `{ version }`
- **Status codes**: 200

---

## Documents Controller

**Route prefix**: `api/v1/documents` | **Auth**: Required (JWT or API Key)

### GET `/api/v1/documents/{documentId}`
Get all document versions for a document ID.
- **Path**: `documentId` (int)
- **Response**: `ApiResponse<List<DocumentResponse>>`
- **Status codes**: 200, 401, 404, 500

### GET `/api/v1/documents/{id}/info`
Get detailed document metadata.
- **Path**: `id` (int)
- **Response**: `ApiResponse<DocumentResponseDetails>`
- **Status codes**: 200, 401, 404, 500

### GET `/api/v1/documents/{id}/download`
Download document as a file stream. Supports HTTP range requests for large files.
- **Path**: `id` (int)
- **Response**: `FileResult` (binary stream with Content-Type and Content-Disposition)
- **Status codes**: 200, 401, 404, 500

### GET `/api/v1/documents/{id}/sas-url`
Get a time-limited Azure Blob Storage SAS URL for direct document access.
- **Path**: `id` (int)
- **Query**: `expiryMinutes` (int, default: 60)
- **Response**: `ApiResponse<DocumentSasUrlResponse>`
  ```json
  { "url": "https://...", "expiresInMinutes": 60 }
  ```
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/documents/upload`
Upload one or more documents to a cabinet or basket. **Max size: 1GB**.
- **Content-Type**: `multipart/form-data`
- **Form fields**:
  - `cabinet` (string, required) — target cabinet or basket name
  - `files` (IFormFile[], required) — files to upload
  - `attributes` (string) — JSON array of `[{ "columnName": "...", "value": "..." }]`
  - `mergeDocs` (bool, default: false) — merge multiple files into one document
  - `fileComments` (string) — JSON array of strings, count must match file count
- **Headers** (bound via `UploadRequestContext`):
  - `X-Logged-In-User` (required)
  - `X-Profile-Key`
  - `X-Calling-App`
  - `X-Device-Type`
  - `X-Trail-Name`
  - `X-EasyFile-Base-Url`
- **Response**: `ApiResponse<DocumentUploadResponse>` (HTTP 201)
  ```json
  { "docId": 5845, "cabinet": "FT_XXX", "documents": [...] }
  ```
- **Status codes**: 201, 400, 500

### POST `/api/v1/documents/delete-uploaded-files`
Delete uploaded files from user's working folder.
- **Body** (JSON):
  ```json
  { "userId": "string", "specificFiles": "file1.pdf|file2.pdf" }
  ```
  `specificFiles`: pipe-delimited list, or `"*"` for all files.
- **Headers**: `X-Logged-In-User` (required)
- **Response**: `ApiResponse<DeleteUploadedFilesResponse>`
  ```json
  { "userId": "...", "filesDeleted": 3, "specificFiles": "*" }
  ```
- **Status codes**: 200, 400, 500

### POST `/api/v1/documents`
Add a new document.
- **Body**: `AddDocumentRequest` (form data)
- **Status codes**: 200, 400, 401, 500

### PUT `/api/v1/documents/{documentId}`
Update document metadata.
- **Path**: `documentId` (int)
- **Body** (JSON): `UpdateDocumentRequest`
- **Status codes**: 200, 400, 401, 404, 500

### PUT `/api/v1/documents/{documentId}/replace`
Replace document file content.
- **Path**: `documentId` (int)
- **Body**: `ReplaceDocumentRequest` (form data with file)
- **Status codes**: 200, 400, 401, 404, 500

### DELETE `/api/v1/documents/{documentId}`
Delete a document.
- **Path**: `documentId` (int)
- **Status codes**: 200, 401, 404, 500

### POST `/api/v1/documents/{documentId}/copy`
Copy a document to another location.
- **Path**: `documentId` (int)
- **Body** (JSON): `CopyDocumentRequest` with destination details
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/documents/{documentId}/checkout`
Check out a document (lock for editing).
- **Path**: `documentId` (int)
- **Status codes**: 200, 401, 404, 409 (already checked out), 500

### POST `/api/v1/documents/{documentId}/checkin`
Check in a document (release lock, create new version).
- **Path**: `documentId` (int)
- **Body**: `CheckInDocumentRequest` (form data with file and comments)
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/documents/{documentId}/checkin-with-annotations`
Check in a document with annotation data.
- **Path**: `documentId` (int)
- **Body**: `CheckInDocumentWithAnnotationsRequest` (form data)
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/documents/{documentId}/undo-checkout`
Release a checkout lock without saving changes.
- **Path**: `documentId` (int)
- **Status codes**: 200, 401, 404, 500

### POST `/api/v1/documents/{documentId}/rollback`
Rollback to a previous document version.
- **Path**: `documentId` (int)
- **Body** (JSON): `RollbackDocumentRequest` with target version
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/documents/{documentId}/convert-to-pdf`
Convert a document to PDF format.
- **Path**: `documentId` (int)
- **Status codes**: 200, 401, 404, 500

### POST `/api/v1/documents/{documentId}/linearize`
Linearize a PDF for fast web viewing (streaming optimization).
- **Path**: `documentId` (int)
- **Status codes**: 200, 401, 404, 500

### POST `/api/v1/documents/merge`
Merge multiple documents into a single new document.
- **Body** (JSON): `MergeDocumentsRequest` with source document IDs
- **Status codes**: 200, 400, 401, 500

### POST `/api/v1/documents/{documentId}/combine`
Combine multiple documents into an existing target document.
- **Path**: `documentId` (int) — the target document
- **Body** (JSON): `CombineDocumentsRequest` with source document IDs
- **Status codes**: 200, 400, 401, 404, 500

---

## Cabinets Controller

**Route prefix**: `api/v1/cabinets` | **Auth**: Required

### GET `/api/v1/cabinets`
List all cabinets for a user.
- **Query**: `userId` (string)
- **Response**: `ApiResponse<object>` (list of cabinets)
- **Status codes**: 200, 401, 500

### GET `/api/v1/cabinets/{cabinetName}`
Get cabinet details by name.
- **Path**: `cabinetName` (string, e.g., `FT_XXX`)
- **Response**: `ApiResponse<CabinetResponse>`
- **Status codes**: 200, 400, 401, 404, 500

### GET `/api/v1/cabinets/bylabel/{label}`
Find cabinets by their display label.
- **Path**: `label` (string)
- **Response**: `ApiResponse<object>`
- **Status codes**: 200, 401, 500

### POST `/api/v1/cabinets/byprivileges`
Get cabinets filtered by user privileges.
- **Body** (JSON):
  ```json
  { "userId": "string", "groups": ["group1"], "privs": ["priv1"] }
  ```
- **Status codes**: 200, 400, 401, 500

### POST `/api/v1/cabinets`
Create a new cabinet.
- **Body** (JSON): `CreateCabinetRequest` with `cabinetName` (required)
- **Response**: HTTP 201 Created with Location header
- **Status codes**: 201, 400, 401, 500

### PUT `/api/v1/cabinets/{cabinetName}`
Update an existing cabinet.
- **Path**: `cabinetName` (string)
- **Body** (JSON): `UpdateCabinetRequest`
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/cabinets/{sourceCabName}/copy`
Copy a cabinet with lookup options.
- **Path**: `sourceCabName` (string)
- **Query**:
  - `currentUser` (string, required)
  - `newCabLabel` (string, required)
  - `copyOption` (enum: `CopyCabinetAndUseSameLookups`, `CopyCabinetAndCreateNewLookupWithSameData`, `CopyCabinetAndCreateNewLookupWithEmptyData`)
- **Headers**: `X-Calling-App` (required)
- **Response**: `ApiResponse<string>` (new cabinet name), HTTP 201
- **Status codes**: 201, 400, 401, 500

### POST `/api/v1/cabinets/{cabinetName}/activate`
Activate a deactivated cabinet.
- **Path**: `cabinetName` (string)
- **Response**: `ApiResponse<bool>`
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/cabinets/{cabinetName}/deactivate`
Deactivate a cabinet.
- **Path**: `cabinetName` (string)
- **Response**: `ApiResponse<bool>`
- **Status codes**: 200, 400, 401, 404, 500

### GET `/api/v1/cabinets/{cabinetName}/fields`
Get all field definitions for a cabinet.
- **Path**: `cabinetName` (string)
- **Response**: `ApiResponse<List<FieldResponse>>`
- **Status codes**: 200, 400, 401, 500

### POST `/api/v1/cabinets/{cabinetName}/permissions`
Get permissions for a user and their groups on a cabinet.
- **Path**: `cabinetName` (string)
- **Body** (JSON):
  ```json
  { "userId": "string", "groups": ["group1", "group2"] }
  ```
- **Response**: `ApiResponse<List<PermissionResponse>>`
- **Status codes**: 200, 400, 401, 500

### GET `/api/v1/cabinets/{cabinetName}/permissions/codes`
Get integer permission codes for a user.
- **Path**: `cabinetName` (string)
- **Query**: `userId` (string, required)
- **Response**: `ApiResponse<List<int>>`
- **Status codes**: 200, 400, 401, 500

### POST `/api/v1/cabinets/{cabinetName}/access`
Grant a user access to a cabinet.
- **Path**: `cabinetName` (string)
- **Query**: `userId` (string, required)
- **Response**: `ApiResponse<bool>`
- **Status codes**: 200, 400, 401, 500

### DELETE `/api/v1/cabinets/{cabinetName}/access`
Remove a user's access from a cabinet.
- **Path**: `cabinetName` (string)
- **Query**: `userId` (string, required)
- **Response**: `ApiResponse<bool>`
- **Status codes**: 200, 400, 401, 404, 500

### POST `/api/v1/cabinets/{cabinetName}/data`
Search and paginate cabinet data.
- **Path**: `cabinetName` (string)
- **Body** (JSON): `FetchCabinetDataRequest`
  ```json
  {
    "userId": "string",
    "start": 0,
    "length": 10,
    "searchText": "",
    "searchKey": "",
    "advanceSearch": "",
    "orderBy": "",
    "orderDir": "asc",
    "gateway": "",
    "gatewayCriteria": "",
    "fullTextSearchType": "",
    "appName": "",
    "searchOption": ""
  }
  ```
- **Response**: `ApiResponse<DataResultResponse>`
- **Status codes**: 200, 400, 401, 500

---

## Cabinet Rules (within Cabinets Controller)

### GET `/api/v1/cabinets/{cabinetName}/rules`
Get all rules for a cabinet.
- **Query**: `aiProcId` (string, optional)
- **Response**: `ApiResponse<List<CabinetRule>>`

### GET `/api/v1/cabinets/rules/{ruleId}`
Get a specific rule by ID.
- **Path**: `ruleId` (string)
- **Response**: `ApiResponse<CabinetRule>`

### POST `/api/v1/cabinets/{cabinetName}/rules`
Create a cabinet rule.
- **Body** (JSON): `CreateCabinetRuleRequest`
- **Response**: `ApiResponse<string>` (ruleId), HTTP 201

### PUT `/api/v1/cabinets/rules/{ruleId}`
Update a cabinet rule.
- **Path**: `ruleId` (string)
- **Body** (JSON): `UpdateCabinetRuleRequest`
- **Response**: `ApiResponse<bool>`

### DELETE `/api/v1/cabinets/{cabinetName}/rules`
Delete cabinet rules.
- **Body** (JSON): `ReorderRulesRequest` with `commands`
- **Response**: `ApiResponse<bool>`

### PUT `/api/v1/cabinets/{cabinetName}/rules/reorder`
Reorder rule execution order.
- **Body** (JSON): `ReorderRulesRequest` with `commands`
- **Response**: `ApiResponse<bool>`

### POST `/api/v1/cabinets/{cabinetName}/rules/execute`
Execute all rules for a cabinet.
- **Body** (JSON): `ExecuteRulesRequest` with `easyFileBaseUrl`, `aiProcID`
- **Response**: `ApiResponse<bool>`

### POST `/api/v1/cabinets/{cabinetName}/rules/{ruleId}/execute`
Execute a specific rule.
- **Path**: `cabinetName` (string), `ruleId` (string)
- **Body** (JSON): `ExecuteRulesRequest` with `easyFileBaseUrl`
- **Response**: `ApiResponse<bool>`

---

## Records Controller

**Route prefix**: `api/cabinets/{cabinetName}/records` | **Auth**: Required

### GET `/api/cabinets/{cabinetName}/records/{docId}`
Get record by document ID.
- **Path**: `cabinetName` (string), `docId` (long)
- **Response**: `ApiResponse<RecordResponse>`
- **Status codes**: 200, 401, 404, 500

### GET `/api/cabinets/{cabinetName}/records/by-unique/{uniqueNo}`
Get record by unique number.
- **Path**: `cabinetName` (string), `uniqueNo` (string)
- **Response**: `ApiResponse<RecordResponse>`
- **Status codes**: 200, 401, 404, 500

### PUT `/api/cabinets/{cabinetName}/records/by-unique/{uniqueNo}`
Update record by unique number.
- **Path**: `cabinetName` (string), `uniqueNo` (string)
- **Body** (JSON): `UpdateRecordRequest` with fields
- **Status codes**: 200, 400, 401, 404, 500

### PUT `/api/cabinets/{cabinetName}/records/bulk`
Bulk update multiple records.
- **Body** (JSON): `BulkUpdateRequest` with `documentIds` and `fields`
- **Status codes**: 200, 400, 401, 500

### DELETE `/api/cabinets/{cabinetName}/records/{docId}`
Soft delete a record.
- **Path**: `cabinetName` (string), `docId` (long)
- **Status codes**: 200, 401, 403, 404, 500

### POST `/api/cabinets/{cabinetName}/records/{docId}/restore`
Restore a deleted record.
- **Path**: `cabinetName` (string), `docId` (long)
- **Status codes**: 200, 401, 403, 404, 500

### POST `/api/cabinets/{cabinetName}/records/{docId}/reviewed`
Mark a record as reviewed.
- **Path**: `cabinetName` (string), `docId` (long)
- **Status codes**: 200, 401, 404, 500

---

## Baskets Controller

**Route prefix**: `api/v1/baskets` | **Auth**: Required

### GET `/api/v1/baskets`
List baskets for user.
- **Query**: `userId` (optional, defaults to `X-Logged-In-User` header)
- **Response**: `ApiResponse<List<BasketResponse>>`

### GET `/api/v1/baskets/{basketNo}`
Get basket by number.
- **Path**: `basketNo` (string)
- **Response**: `ApiResponse<object>`

### GET `/api/v1/baskets/by-name/{name}`
Get basket by name.
- **Path**: `name` (string)
- **Response**: `ApiResponse<BasketResponse>`

### POST `/api/v1/baskets`
Create a new basket.
- **Body** (JSON): `BasketResponse` with basket details
- **Response**: `ApiResponse<BasketResponse>`

### PUT `/api/v1/baskets/{basketNo}`
Update basket.
- **Path**: `basketNo` (string)
- **Body** (JSON): `BasketResponse`
- **Response**: `ApiResponse<bool>`

### GET `/api/v1/baskets/{basketNo}/documents`
List documents in a basket.
- **Path**: `basketNo` (string)
- **Query**: `userId` (optional)
- **Response**: `ApiResponse<List<BasketDocumentResponse>>`

### POST `/api/v1/baskets/{basketNo}/documents`
Upload documents to a basket. **Max size: 1GB**.
- **Path**: `basketNo` (string)
- **Content-Type**: `multipart/form-data`
- **Form fields**:
  - `files` (IFormFile[], required)
  - `fileComments` (JSON string array, optional)
- **Headers**: `X-Logged-In-User`, `X-Profile-Key`, `X-Calling-App`, `X-Trail-Name`
- **Response**: HTTP 201 Created

### DELETE `/api/v1/baskets/{basketNo}/items/{itemId}`
Remove item from basket.
- **Path**: `basketNo` (string), `itemId` (int)
- **Response**: `ApiResponse<bool>`

### POST `/api/v1/baskets/{basketNo}/access`
Set basket access for a user.
- **Path**: `basketNo` (string)
- **Body** (JSON): `SetAccessRequest` with `userId`
- **Response**: `ApiResponse<bool>`

### DELETE `/api/v1/baskets/{basketNo}/access`
Remove basket access.
- **Path**: `basketNo` (string)
- **Query**: `userId` (string, required)
- **Response**: `ApiResponse<bool>`

---

## Fields Controller

**Route prefix**: `api/v1/fields` | **Auth**: Required

All endpoints require `cabinetName` as a query parameter.

### GET `/api/v1/fields?cabinetName={name}`
Get all fields for a cabinet.
- **Response**: `ApiResponse<List<FieldResponse>>`

### GET `/api/v1/fields/{fieldId}?cabinetName={name}`
Get field by ID.
- **Path**: `fieldId` (int)
- **Response**: `ApiResponse<FieldResponse>`

### GET `/api/v1/fields/by-label/{label}?cabinetName={name}`
Get field by label.
- **Path**: `label` (string)
- **Response**: `ApiResponse<FieldResponse>`

### POST `/api/v1/fields?cabinetName={name}`
Create a field.
- **Body** (JSON): `CreateFieldRequest`
- **Response**: `ApiResponse<object>` with fieldId

### PUT `/api/v1/fields/{fieldId}?cabinetName={name}`
Update a field.
- **Path**: `fieldId` (int)
- **Body** (JSON): `UpdateFieldRequest`

### DELETE `/api/v1/fields/{fieldId}?cabinetName={name}`
Soft delete a field.
- **Path**: `fieldId` (int)

### POST `/api/v1/fields/{fieldId}/restore?cabinetName={name}`
Restore a deleted field.
- **Path**: `fieldId` (int)

### PATCH `/api/v1/fields/{fieldId}/properties?cabinetName={name}`
Update field properties (readonly, required, visibility, etc.).
- **Path**: `fieldId` (int)
- **Body** (JSON): `FieldPropertiesRequest`

### GET `/api/v1/fields/{fieldId}/browse?cabinetName={name}`
Get distinct values for a field (browse data).
- **Path**: `fieldId` (int)
- **Response**: `ApiResponse<List<string>>`

---

## Users Controller

**Route prefix**: `api/users` | **Auth**: Required

### GET `/api/users`
List all users.
- **Response**: `ApiResponse<IEnumerable<UserResponse>>`

### GET `/api/users/{userId}`
Get user by ID.
- **Path**: `userId` (string)
- **Response**: `ApiResponse<UserResponse>`

### GET `/api/users/by-email/{email}`
Get user by email.
- **Path**: `email` (string)
- **Response**: `ApiResponse<UserResponse>`

### POST `/api/users`
Create a new user.
- **Body** (JSON): `CreateUserRequest`

### PUT `/api/users/{userId}`
Update a user.
- **Path**: `userId` (string)
- **Body** (JSON): `UpdateUserRequest`

### DELETE `/api/users/{userId}`
Delete a user.
- **Path**: `userId` (string)

### POST `/api/users/{userId}/restore`
Restore a deleted user.
- **Path**: `userId` (string)

### POST `/api/users/{userId}/password`
Change user password.
- **Path**: `userId` (string)
- **Body** (JSON): `ChangePasswordRequest`

### GET `/api/users/{userId}/permissions`
Get user permissions.
- **Path**: `userId` (string)
- **Response**: `ApiResponse<IEnumerable<UserPermissionResponse>>`

### POST `/api/users/{userId}/permissions`
Set a user permission.
- **Path**: `userId` (string)
- **Body** (JSON): `SetPermissionRequest`

### GET `/api/users/{userId}/groups`
Get groups the user belongs to.
- **Path**: `userId` (string)
- **Response**: `ApiResponse<IEnumerable<UserGroupResponse>>`

### GET `/api/users/{userId}/cabinets`
Get cabinets accessible to the user.
- **Path**: `userId` (string)
- **Response**: `ApiResponse<IEnumerable<string>>`

### GET `/api/users/{userId}/preferences`
Get user preferences.
- **Path**: `userId` (string)
- **Response**: `ApiResponse<UserPreferenceResponse>`

### POST `/api/users/{userId}/preferences`
Set a user preference.
- **Path**: `userId` (string)
- **Body** (JSON): `SetPreferenceRequest`

---

## Search Controller

**Route prefix**: `api/v1/search` | **Auth**: Required

### GET `/api/v1/search/saved`
Get user's saved searches.
- **Response**: `ApiResponse<IEnumerable<SavedSearchResponse>>`

### POST `/api/v1/search/saved`
Save a new search.
- **Body** (JSON): `SaveSearchRequest`
- **Response**: `ApiResponse<object>` with searchId

### DELETE `/api/v1/search/saved/{searchId}`
Delete a saved search.
- **Path**: `searchId` (int)

### GET `/api/v1/search/filters/favorites`
Get user's favorite filters.
- **Response**: `ApiResponse<IEnumerable<FavoriteFilterResponse>>`

---

## Lookup Controller

**Route prefix**: `api/v1/lookup` | **Auth**: Required

### GET `/api/v1/lookup/tables`
List all lookup tables.

### GET `/api/v1/lookup/tables/{lookupTable}/{lookupColumn}/data`
Get lookup data with pagination.
- **Path**: `lookupTable` (string), `lookupColumn` (string)
- **Query**: `pageSize` (int, default: 50), `pageIndex` (int, default: 0)

### POST `/api/v1/lookup/tables`
Create a lookup table.
- **Body** (JSON): `CreateLookupTableRequest`

### POST `/api/v1/lookup/tables/{lookupTable}/data`
Add a value to a lookup table.
- **Path**: `lookupTable` (string)
- **Body** (JSON):
  ```json
  { "lookupColumn": "string", "value": "string" }
  ```

### GET `/api/v1/lookup/autofill`
Get autofill suggestions.
- **Query**: `fieldId` (int, required), `searchValue` (string, optional)
- **Response**: `ApiResponse<IEnumerable<string>>`

### GET `/api/v1/lookup/cabinets/{cabinetName}/fields/{fieldName}/list`
Get lookup list for a specific field in a cabinet.

### GET `/api/v1/lookup/tables/{lookupTable}/{lookupColumn}/count`
Get count of distinct values in a lookup column.

### GET `/api/v1/lookup/tables/{lookupTable}/{lookupColumn}/browse`
Browse lookup data with visible columns.
- **Query**: `visibleColumns` (string), `filterSQL` (string)

---

## Audit Controller

**Route prefix**: `api/v1/audit` | **Auth**: Required

### GET `/api/v1/audit/trail`
Get audit trail entries.
- **Query**: `startDate` (datetime), `endDate` (datetime), `userId` (string)
- **Response**: `ApiResponse<IEnumerable<LogTrailResponse>>`

### POST `/api/v1/audit/trail`
Record a user activity entry.
- **Body** (JSON): `LogUserTrailRequest` with Action, Details

### POST `/api/v1/audit/log`
Record a general log entry.
- **Body** (JSON): `LogInfoRequest` with Level, Message

---

## Retention Controller

**Route prefix**: `api/v1/retention` | **Auth**: Required

### GET `/api/v1/retention/rules`
Get all retention rules.

### GET `/api/v1/retention/cabinets/{cabinetName}/rules`
Get retention rules for a specific cabinet.

### POST `/api/v1/retention/rules`
Create a retention rule.
- **Body** (JSON): `AddRetentionRuleRequest`

### PUT `/api/v1/retention/rules/{ruleId}`
Update a retention rule.
- **Path**: `ruleId` (int)
- **Body** (JSON): `UpdateRetentionRuleRequest`

### DELETE `/api/v1/retention/rules/{ruleId}`
Delete a retention rule.
- **Path**: `ruleId` (int)

---

## AI Controller

**Route prefix**: `api/v1` | **Auth**: Required

### POST `/api/v1/QueryAI`
Execute an AI query with content context.
- **Body** (JSON): `QueryDto`
  ```json
  {
    "prompt": "string",
    "model": "string",
    "contentType": "string",
    "contentFileId": "string",
    "keepConversationOpen": false,
    "clientId": "string"
  }
  ```
- **Response**: String AI response

### POST `/api/v1/AIRequest`
Process content using AI.
- **Body** (JSON): `AIProcessingRequest`
- **Response**: `ProcessingResult`

### POST `/api/v1/AIRequestUpload`
Process content with multipart file upload (up to 500MB).
- **Content-Type**: `multipart/form-data`
- **Form fields**: `Request` (JSON string), `File` (file, optional)
- **Response**: `ProcessingResult`

### DELETE `/api/v1/ClearConversation/{clientId}`
Clear conversation history.
- **Path**: `clientId` (string)
- **Response**: 204 No Content

### GET `/api/v1/GetAIModels`
Get available AI models.
- **Response**: `{ models: [...], defaultModel: "string" }`

### GET `/api/v1/TestAIConnection`
Test AI service connectivity.
- **Response**: `"0"` (status code string)

---

## System Controller

**Route prefix**: `api/v1/system` | **Auth**: Partial

### GET `/api/v1/system/version`
Get system version. **No auth required**.
- **Response**: `ApiResponse<SystemVersionResponse>`

### GET `/api/v1/system/config`
Get system configuration. **Auth required**.

### GET `/api/v1/system/config/database`
Get database configuration. **Auth required**.

### POST `/api/v1/system/cache/clear`
Clear system cache. **Auth required**.

### GET `/api/v1/system/datetime`
Get server date and time. **Auth required**.
- **Response**: `ApiResponse<ServerDateTimeResponse>`

### POST `/api/v1/system/azure/cors`
Setup Azure CORS configuration. **Auth required**.
- **Body** (JSON): `AzureCorsRequest`

---

## File Storage Controller

**Route prefix**: `api/v1/filestorage` | **Auth**: API Key only

### POST `/api/v1/filestorage/upload`
Upload file to temporary storage (1-hour cache, auto-cleanup).
- **Content-Type**: `multipart/form-data`
- **Form fields**: `file` (IFormFile)
- **Response**: `{ fileId, fileName, filePath, sizeInBytes, contentType }`

### GET `/api/v1/filestorage/content/{fileId}`
Get file content.
- **Path**: `fileId` (string)
- **Response**: File content (text or binary)

### GET `/api/v1/filestorage/info/{fileId}`
Get file metadata.
- **Path**: `fileId` (string)
- **Response**: `ApiResponse<FileMetadata>`

### DELETE `/api/v1/filestorage/{fileId}`
Delete file.
- **Path**: `fileId` (string)

---

## Workflows Controller

**Route prefix**: `api/workflows` | **Auth**: Required

### GET `/api/workflows/{workflowId}`
Get workflow by ID.
- **Path**: `workflowId` (int)
- **Response**: `ApiResponse<WorkflowResponse>`

### POST `/api/workflows`
Create a new workflow.
- **Body** (JSON): `CreateWorkflowRequest`

### PUT `/api/workflows/{workflowId}`
Update a workflow.
- **Path**: `workflowId` (int)
- **Body** (JSON): `UpdateWorkflowRequest`

### DELETE `/api/workflows/{workflowId}`
Delete a workflow.
- **Path**: `workflowId` (int)

---

## PDF Docs Controller

**Route prefix**: `api/v1` | **Auth**: None

### POST `/api/v1/PrintPDFDoc`
Submit a document for PDF printing.
- **Body** (JSON): `PrintPDFDocRequest`

### POST `/api/v1/ExportPDFDoc/{appId}`
Export a document to PDF.
- **Path**: `appId` (string)
- **Body** (JSON): `ExportPDFDocRequest`

### GET `/api/v1/GetPDFDocStatus/{jobId}`
Check PDF generation job status.
- **Path**: `jobId` (string)
- **Response**: `PrintPDFDocResponse` or `"JobID not found."`

### GET `/api/v1/GetPDFDoc/{jobId}`
Get URL of generated PDF.
- **Path**: `jobId` (string)

### GET `/api/v1/document/{filename}`
Download a generated PDF file.
- **Path**: `filename` (string)
- **Response**: `FileStreamResult`

### DELETE `/api/v1/CancelPDFDoc/{jobId}`
Cancel a PDF generation job.
- **Path**: `jobId` (string)

---

## LDAP Settings Controller

**Route prefix**: `api/v1/ldapsetting` | **Auth**: None

### GET `/api/v1/ldapsetting`
Get all LDAP settings.

### GET `/api/v1/ldapsetting/{settingName}`
Get LDAP settings by name.

### POST `/api/v1/ldapsetting`
Add LDAP settings.
- **Body** (JSON): `LDAPSettingsRequest`

### PUT `/api/v1/ldapsetting/{settingName}`
Update LDAP settings.

### DELETE `/api/v1/ldapsetting/{settingName}`
Delete LDAP settings.

---

## Rules Controller (alternate route)

**Route prefix**: `api/cabinets/{cabinetName}/rules` | **Auth**: Required

### GET `/api/cabinets/{cabinetName}/rules`
Get all rules for a cabinet.

### POST `/api/cabinets/{cabinetName}/rules`
Create a rule.
- **Body** (JSON): `AddRuleRequest` with `Condition`, `Action`

### PUT `/api/cabinets/{cabinetName}/rules/{ruleId}`
Update a rule.
- **Path**: `ruleId` (int)
- **Body** (JSON): `UpdateRuleRequest`

### DELETE `/api/cabinets/{cabinetName}/rules/{ruleId}`
Delete a rule.
- **Path**: `cabinetName` (string), `ruleId` (int)

### POST `/api/cabinets/{cabinetName}/rules/execute`
Execute all rules for a cabinet.

### POST `/api/cabinets/{cabinetName}/rules/{ruleId}/execute`
Execute a specific rule.
- **Path**: `cabinetName` (string), `ruleId` (int)
