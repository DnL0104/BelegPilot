# TaxReader

> German tax-receipt aggregator — turn a pile of PDF and image receipts into a clean per-category-per-year expense summary for ELSTER or Steuerberater hand-off.

Receipts are text-extracted (PdfPig + Tesseract OCR), parsed by format-specific parsers (Amazon, Eduki, Generic), AI-classified into tax-relevant categories, and aggregated into a German-localized PDF/CSV report.

See [`CLAUDE.md`](CLAUDE.md) for project guidelines and [`.planning/codebase/`](.planning/codebase/) for stack, architecture, and conventions intel.

## Prerequisites

- **.NET 10 SDK** — backend build (`dotnet --version` reports `10.x`)
- **Node.js 22+** — frontend build (`node --version` reports `v22.x` or higher)
- **Docker Desktop** with Docker Compose v2 — full stack run (`docker compose version` succeeds)
- **Tesseract OCR** with `deu+eng` language packs — only required for **non-container** local dev (the `api` container ships Tesseract via apt)
  - macOS: `brew install tesseract tesseract-lang`
  - Linux (Debian/Ubuntu): `sudo apt-get install tesseract-ocr tesseract-ocr-deu tesseract-ocr-eng`
  - Windows: install from <https://github.com/UB-Mannheim/tesseract/wiki>; ensure `tessdata` contains `deu.traineddata` + `eng.traineddata`

## Quick Start

```bash
# 1. Clone and enter the repo
git clone <repo-url> taxreader
cd taxreader

# 2. Copy the env template and edit secrets
cp .env.example .env
# then open .env in your editor and fill in:
#   - JWT_SECRET (any long random string for local dev)
#   - ANTHROPIC_API_KEY (your Anthropic key)
#   - POSTGRES_PASSWORD (any string)
#   - REFRESHTOKEN_HASHKEY (32-byte Base64 pepper — the API refuses to boot without it)
#       generate with: openssl rand -base64 32

# 3. Bring up the full stack
docker compose up --build

# 4. Open the app
# → https://localhost  (Caddy terminates TLS with a self-signed cert on first run)
```

The first build takes a few minutes (Docker pulls .NET 10, Node 22, Tesseract, and Postgres images). Subsequent runs reuse cached layers.

## Common tasks

| Task | Command |
|------|---------|
| Backend build | `dotnet build Backend` |
| Backend tests | `dotnet test Backend` |
| Backend run (without container) | `dotnet run --project Backend/src/TaxReader.Api` |
| Frontend dev server | `cd Frontend && npm install && npm run dev` |
| Frontend production build | `cd Frontend && npm run build` |
| Reset full stack (drops the Postgres volume) | `docker compose down -v` |
| Add an EF Core migration | `dotnet ef migrations add <Name> -p Backend/src/TaxReader.Infrastructure -s Backend/src/TaxReader.Api` |

## Documentation

- [`CLAUDE.md`](CLAUDE.md) — project guidelines, conventions, architecture rules
- [`.planning/PROJECT.md`](.planning/PROJECT.md) — product vision, constraints, key decisions
- [`.planning/ROADMAP.md`](.planning/ROADMAP.md) — milestone phases and status
- [`.planning/codebase/`](.planning/codebase/) — stack inventory, architecture, conventions, concerns

## License

See [`LICENSE`](LICENSE) (when added). Pre-commercial — see `.planning/PROJECT.md` for the launch milestone scope.
