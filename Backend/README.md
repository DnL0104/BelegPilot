# TaxReader

TaxReader is an API-first .NET 10 project for uploading PDF receipts, parsing text-based receipts, classifying expense items, and calculating yearly category totals.

## Scope of this version

This first version focuses on:

- local hosting inside a private network
- PDF upload
- parser support for Amazon, EDUKI, and generic PDF receipts
- rule-based classification
- totals for:
  - `ConsumablesAndOfficeSupplies`
  - `SpecialistLiterature`
- PostgreSQL with Entity Framework Core
- local file storage
- unit tests

## Architecture

The solution is split into four projects:

- `TaxReader.Api`
- `TaxReader.Application`
- `TaxReader.Domain`
- `TaxReader.Infrastructure`

This structure keeps the HTTP layer, application workflows, domain model, and infrastructure concerns separated. That makes it easier to evolve the project later with OCR, background jobs, more source systems, a dedicated frontend, and additional reporting endpoints.

## Why the data model looks like this

The most important design decision is that classification is stored as a separate entity instead of a single `CategoryId` directly on `ReceiptItem`.

That was intentional because the project will likely need:

- suggested classifications
- manual confirmation
- auditability
- future AI-assisted classification
- historical reprocessing

The core tables are:

- `receipt_files`
- `receipts`
- `receipt_items`
- `categories`
- `item_classifications`
- `classification_rules`
- `processing_runs`

This gives you a stable document-processing pipeline instead of a single oversized `receipts` table.

## Requirements

- .NET 10 SDK
- Docker and Docker Compose
- PostgreSQL if you do not use Docker

## Run with Docker Compose

```bash
docker compose up --build
```

The API will be available at:

- `http://localhost:8080`
- OpenAPI UI: `http://localhost:8080/scalar/v1`

## Run locally without Docker

1. Start PostgreSQL.
2. Update `src/TaxReader.Api/appsettings.json` if needed.
3. Run the API:

```bash
dotnet restore
dotnet run --project src/TaxReader.Api
```

The application automatically applies migrations in development mode.

## Example upload request

Use multipart/form-data.

### cURL

```bash
curl -X POST "http://localhost:8080/api/v1/receipt-files" \
  -H "Content-Type: multipart/form-data" \
  -F "files=@/path/to/amazon.pdf" \
  -F "sourceHint=Amazon" \
  -F "yearHint=2025" \
  -F "uploadedBy=local-user"
```

## Example totals request

```bash
curl "http://localhost:8080/api/v1/reports/category-totals/2025"
```

## Important implementation notes

### 1. Text-based PDFs only
This version supports text-based PDFs. It does not perform OCR. That was a deliberate choice because starting with OCR would make the first version much less reliable and harder to test.

### 2. Rule-based classification first
The classification engine loads rules from the database. This is a stronger starting point than hard-coding all rules in `if/else` blocks because it keeps the architecture extensible.

### 3. Local file storage first
The current storage provider writes uploaded PDFs to a local directory. That is the simplest and most robust choice for a private local network deployment.

### 4. API-first design
The backend is intentionally UI-agnostic. You can build a React, Next.js, Blazor, or desktop frontend later without changing the core processing pipeline.

## Suggested next steps

The strongest next improvements would be:

1. add manual classification confirmation endpoints
2. add OCR support for scanned PDFs
3. add vendor normalization
4. add more source-specific parsers
5. add authentication for multi-user usage
6. add integration tests with PostgreSQL and real sample PDFs

## Test command

```bash
dotnet test
```
