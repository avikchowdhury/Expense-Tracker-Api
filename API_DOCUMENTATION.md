# API Documentation & Best Practices

This document provides guidance for API consumers and developers integrating with the Expense Tracker API.

## 📚 Table of Contents

1. [Authentication](#authentication)
2. [Rate Limiting](#rate-limiting)
3. [Error Handling](#error-handling)
4. [Response Format](#response-format)
5. [Pagination](#pagination)
6. [Filtering & Sorting](#filtering--sorting)
7. [Versioning](#versioning)
8. [Examples](#examples)

---

## Authentication

All API endpoints (except `/api/auth/*`) require JWT authentication.

### Obtaining a Token

#### 1. Register a New Account
```bash
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "username": "john_doe",
  "password": "SecurePassword123!"
}

Response:
{
  "success": true,
  "message": "OTP sent to email",
  "userId": 1
}
```

#### 2. Verify OTP
```bash
POST /api/auth/verify-otp
Content-Type: application/json

{
  "email": "user@example.com",
  "otpCode": "123456"
}

Response:
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400
}
```

#### 3. Login
```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "john_doe",
  "password": "SecurePassword123!"
}

Response:
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400
}
```

### Using the Token

Include the token in the `Authorization` header:

```bash
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Refresh

Tokens expire after 24 hours. Refresh using:

```bash
POST /api/auth/refresh
Authorization: Bearer your-current-token

Response:
{
  "token": "new-jwt-token",
  "expiresIn": 86400
}
```

---

## Rate Limiting

The API implements rate limiting to prevent abuse.

### Limits
- **Authenticated Users**: 1,000 requests per hour
- **Public Endpoints**: 100 requests per hour per IP

### Rate Limit Headers
Every response includes rate limit information:

```
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 950
X-RateLimit-Reset: 1620000000
```

### Handling Rate Limits
When you hit the limit, you'll receive:

```
HTTP 429 Too Many Requests

{
  "success": false,
  "message": "Rate limit exceeded",
  "retryAfter": 3600
}
```

**Recommendation**: Implement exponential backoff when receiving 429 responses.

---

## Error Handling

The API uses standard HTTP status codes and a consistent error response format.

### HTTP Status Codes

| Code | Meaning | Action |
|------|---------|--------|
| 200 | OK | Request succeeded |
| 201 | Created | Resource created successfully |
| 204 | No Content | Successful DELETE request |
| 400 | Bad Request | Invalid input; check error details |
| 401 | Unauthorized | Missing/invalid token; re-authenticate |
| 403 | Forbidden | Authenticated but insufficient permissions |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Duplicate resource or state conflict |
| 422 | Unprocessable Entity | Validation errors |
| 500 | Internal Server Error | Server error; contact support |

### Error Response Format

```json
{
  "success": false,
  "message": "Detailed error message",
  "errorCode": "VALIDATION_ERROR",
  "errors": [
    {
      "field": "amount",
      "message": "Amount must be greater than 0"
    },
    {
      "field": "category",
      "message": "Category does not exist"
    }
  ]
}
```

### Common Error Codes

```
AUTH_INVALID_CREDENTIALS      - Login failed
AUTH_TOKEN_EXPIRED            - JWT token expired
AUTH_INSUFFICIENT_PERMISSIONS - User lacks required permissions
VALIDATION_ERROR              - Input validation failed
RESOURCE_NOT_FOUND            - Requested resource doesn't exist
DUPLICATE_RESOURCE            - Resource already exists
DATABASE_ERROR                - Database operation failed
EXTERNAL_SERVICE_ERROR        - Third-party service error (Groq, Azure)
```

---

## Response Format

All API responses follow a consistent format:

### Success Response (GET, POST, PUT)
```json
{
  "success": true,
  "data": {
    "id": 1,
    "vendorName": "Grocery Store",
    "amount": 45.50,
    "transactionDate": "2026-05-10T10:30:00Z",
    "categoryId": 5,
    "notes": "Weekly groceries"
  },
  "message": "Expense retrieved successfully"
}
```

### Success Response (DELETE)
```json
{
  "success": true,
  "message": "Expense deleted successfully"
}
```

### Paginated Response
```json
{
  "success": true,
  "data": [
    { /* item 1 */ },
    { /* item 2 */ }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "totalItems": 45,
    "totalPages": 5,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

---

## Pagination

List endpoints support pagination with standard parameters:

```bash
GET /api/expenses?page=1&pageSize=20&sortBy=date&sortOrder=desc

Parameters:
- page: Page number (1-based, default: 1)
- pageSize: Items per page (1-100, default: 10)
- sortBy: Column to sort by (optional)
- sortOrder: asc or desc (default: asc)
```

### Example Request
```bash
GET /api/receipts?page=2&pageSize=15&sortBy=transactionDate&sortOrder=desc
Authorization: Bearer token
```

### Response
```json
{
  "success": true,
  "data": [ /* 15 items */ ],
  "pagination": {
    "page": 2,
    "pageSize": 15,
    "totalItems": 150,
    "totalPages": 10,
    "hasNextPage": true,
    "hasPreviousPage": true
  }
}
```

---

## Filtering & Sorting

### Filtering

Support filters for common queries:

```bash
GET /api/expenses?categoryId=5&minAmount=10&maxAmount=100&startDate=2026-01-01&endDate=2026-12-31

Filter Parameters:
- categoryId: Filter by category
- minAmount: Minimum expense amount
- maxAmount: Maximum expense amount
- startDate: Start date (ISO 8601)
- endDate: End date (ISO 8601)
- vendorName: Search by vendor name (partial match)
```

### Sorting

```bash
GET /api/expenses?sortBy=amount&sortOrder=desc

Valid sortBy values:
- transactionDate
- amount
- vendorName
- categoryId
```

---

## Versioning

Currently running API **v1**. Future versions will be available at `/api/v2/`.

**Deprecation Warning**: Endpoints may be deprecated with 6 months notice.

---

## Examples

### Create an Expense
```bash
POST /api/receipts
Authorization: Bearer token
Content-Type: application/json

{
  "vendorName": "Amazon",
  "amount": 89.99,
  "transactionDate": "2026-05-10T10:30:00Z",
  "categoryId": 3,
  "notes": "Office supplies"
}

Response (201 Created):
{
  "success": true,
  "data": {
    "id": 42,
    "vendorName": "Amazon",
    "amount": 89.99,
    "transactionDate": "2026-05-10T10:30:00Z",
    "categoryId": 3,
    "notes": "Office supplies",
    "createdAt": "2026-05-10T10:35:00Z"
  }
}
```

### Get Expenses with Filters
```bash
GET /api/receipts?page=1&pageSize=10&categoryId=3&minAmount=50&sortBy=amount&sortOrder=desc
Authorization: Bearer token

Response (200 OK):
{
  "success": true,
  "data": [
    {
      "id": 42,
      "vendorName": "Amazon",
      "amount": 89.99,
      "transactionDate": "2026-05-10T10:30:00Z",
      "categoryId": 3
    },
    { /* more items */ }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 10,
    "totalItems": 25,
    "totalPages": 3,
    "hasNextPage": true
  }
}
```

### Update an Expense
```bash
PUT /api/receipts/42
Authorization: Bearer token
Content-Type: application/json

{
  "amount": 99.99,
  "notes": "Office supplies and packaging"
}

Response (200 OK):
{
  "success": true,
  "data": {
    "id": 42,
    "amount": 99.99,
    "notes": "Office supplies and packaging",
    "updatedAt": "2026-05-10T10:40:00Z"
  }
}
```

### Delete an Expense
```bash
DELETE /api/receipts/42
Authorization: Bearer token

Response (204 No Content):
(Empty body)
```

### Handle Errors
```bash
POST /api/receipts
Authorization: Bearer token
Content-Type: application/json

{
  "vendorName": "Amazon",
  "amount": -50,  # Invalid: negative amount
  "categoryId": 999  # Invalid: category doesn't exist
}

Response (422 Unprocessable Entity):
{
  "success": false,
  "message": "Validation failed",
  "errorCode": "VALIDATION_ERROR",
  "errors": [
    {
      "field": "amount",
      "message": "Amount must be greater than 0"
    },
    {
      "field": "categoryId",
      "message": "Category 999 does not exist"
    }
  ]
}
```

---

## SDK/Client Libraries

### Using cURL
```bash
# Get token
curl -X POST https://api.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john_doe","password":"SecurePassword123!"}'

# Get expenses
curl https://api.example.com/api/receipts \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Using Python
```python
import requests

# Authenticate
response = requests.post(
    'https://api.example.com/api/auth/login',
    json={'username': 'john_doe', 'password': 'SecurePassword123!'}
)
token = response.json()['data']['token']

# Get expenses
headers = {'Authorization': f'Bearer {token}'}
response = requests.get(
    'https://api.example.com/api/receipts',
    headers=headers
)
expenses = response.json()['data']
```

### Using JavaScript/Node.js
```javascript
// Authenticate
const loginResponse = await fetch('https://api.example.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ username: 'john_doe', password: 'SecurePassword123!' })
});
const { data } = await loginResponse.json();
const token = data.token;

// Get expenses
const expensesResponse = await fetch('https://api.example.com/api/receipts', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const expenses = await expensesResponse.json();
```

---

## Support & Resources

- 📖 [Full API Documentation](https://github.com/avikchowdhury/Expense-Tracker-Api)
- 💬 [GitHub Discussions](https://github.com/avikchowdhury/Expense-Tracker-Api/discussions)
- 🐛 [Report Issues](https://github.com/avikchowdhury/Expense-Tracker-Api/issues)
- 🔒 [Security Policy](./SECURITY.md)

---

**Last Updated**: May 10, 2026
