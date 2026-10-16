# RaceDay — Part 2 REST API

RaceDay manages South African running, walking and cycling events. This submission implements the ASP.NET Core API only.

## Roles

- **Organiser:** manage owned events and categories, view their enrolments, capture and correct results.
- **Participant:** manage their profile, enter categories, cancel before the event, view their enrolment and result history.
- **Public:** browse upcoming events, category information and category results.

## Tech stack and prerequisites

C# / .NET 8 ASP.NET Core Web API; EF Core 8.0.10 with SQL Server; BCrypt; server-side ASP.NET sessions; Swagger; xUnit with isolated SQLite integration tests.

Install the **.NET 8 SDK** (includes the ASP.NET Core runtime). A .NET 9 SDK alone can compile this project but does not supply its required ASP.NET Core 8 runtime. SQL Server is required for application data. On macOS use a reachable SQL Server instance; Windows LocalDB does not run on macOS. No Docker is required for this project.

## Structure

- `RaceDay.Api/Controllers`: requests, validation DTOs, session/ownership checks and safe response DTOs.
- `RaceDay.Api/Models`: the six Part 1 entities.
- `RaceDay.Api/Data`: EF configuration matching the Part 1 SQL schema.
- `RaceDay.Api/Migrations`: Code-First schema history.
- `RaceDay.Api.Tests`: unit validation tests and HTTP integration tests using real session middleware.
- `docs`: original Part 1 files, alignment decisions, manual test pack, rubric audit and demo guide.
- `.github/workflows`: original documentation validation and Part 2 restore/build/test workflow.

## Configuration and database setup

The checked-in connection string is the original Windows LocalDB development setting. Override it for your own SQL Server without committing passwords:

```sh
dotnet user-secrets init --project RaceDay.Api
dotnet user-secrets set 'ConnectionStrings:RaceDayDB' 'Server=YOUR_SERVER;Database=RaceDayPart2;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True' --project RaceDay.Api
```

`TrustServerCertificate=True` is intended only for your trusted development server. Windows users may use the existing integrated-security LocalDB connection string.

```sh
dotnet tool restore
dotnet restore RaceDay.Api.sln
dotnet ef database update --project RaceDay.Api
```

Use a **fresh database** for the initial migration. Do not apply the initial migration to a database already created by the Part 1 SQL script without a deliberate migration baseline. Do not run that script against valuable data: it drops and recreates tables.

The original SQL seed users contain `HASH_PLACEHOLDER_*`, not usable password hashes. Register fresh accounts through the API. The original script is preserved as Part 1 evidence; the API does not execute it automatically.

## Run the API

```sh
dotnet dev-certs https --trust
dotnet run --project RaceDay.Api --launch-profile https
```

Open <https://localhost:7229/swagger>. Swagger is available in Development. The HTTP development profile uses <http://localhost:5298/swagger>.

## Authentication and Swagger

1. Register an Organiser and Participant using `POST /api/auth/register`.
2. Call `POST /api/auth/login` with email and password. Note the returned user ID and role.
3. The browser receives `RaceDay.Session`; user ID and role are stored on the server.
4. Call a protected endpoint in the **same Swagger browser session**. No Bearer token is needed.
5. Logout before changing roles, or use separate normal/private browser windows for the roles.
6. Verify `401` after logout and `403` for a wrong role or another owner's record.

Sessions expire after 30 minutes of inactivity and are held in memory for this single-instance coursework application. Restarting the API clears them. Production multi-instance deployment would need shared session storage. Serve over HTTPS; the cookie is HttpOnly and SameSite Strict.

Passwords must be at least 8 characters, at most 72 characters and at most 72 UTF-8 bytes. The byte limit avoids BCrypt truncation. Email addresses are stored in lowercase.

## API overview

All 19 Part 1 operations are implemented, plus logout and an organiser event-enrolment list. See [endpoint inventory and alignment](docs/PART2-ALIGNMENT.md) for exact routes and documented differences.

Distance belongs to categories in the approved schema; event responses include those categories and their distances. Event types use the approved `Running`, `Walking`, and `Cycling` values. Dates are treated as local event time; use consistent South African event times in the demo.

## Run tests

```sh
dotnet test RaceDay.Api.sln --logger trx --results-directory TestResults
```

The HTTP tests use an independent SQLite in-memory database per test. They exercise sessions, validation, roles, ownership and relational behaviour without touching your SQL Server. SQLite is useful for isolated foreign-key/uniqueness testing but does not prove SQL Server migration syntax, locking, collation or deployment. SQL Server verification remains a separate manual check.

## GitHub Actions / CI

`.github/workflows/part2.yml` restores, builds and runs tests on every push and pull request, and uploads TRX results. Existing Part 1 document validation is preserved.

### CI green-build screenshot

**Pending:** run the Part 2 workflow in GitHub, then save its successful run screenshot as `docs/part2-ci-success.png` and embed it here. `docs/ci-success.png` is historical Part 1 evidence and must not be presented as a Part 2 test run.

### Meaningful commits

The inspected remote `main` contained 16 total Part 1 commits and no API source. This does not establish 20 meaningful Part 2 commits. Commit genuine ongoing implementation, review and verification work using your own account; do not manufacture history or count trivial edits.

## YouTube demonstration

**Part 2 video: pending.** Record your own voice, show the running SQL Server-backed API and test/CI evidence, upload an unlisted video, and replace this paragraph with the link. The original Part 1 README linked https://www.youtube.com/shorts/ibWM3m3GF6c; it is not verified as Part 2 evidence.

Use [the demo guide](docs/PART2-DEMO.md) and [manual test pack](docs/PART2-MANUAL-TESTS.md).

## AI usage disclosure

OpenAI Codex assisted with requirements analysis, implementation, test creation and documentation. The student must review and understand these changes, validate the system, explain design choices in their own words, and follow the institution's AI disclosure and academic integrity requirements. This work is not represented as entirely manually authored.

## Verification and remaining evidence

Release build: **0 warnings, 0 errors**. Local tests: **58 passed, 0 failed, 0 skipped**. See [test evidence](docs/PART2-TEST-RESULTS.md) and [the Part 2 audit](docs/PART2-AUDIT.md). Local automated results are separate from SQL Server, GitHub green-build and video evidence. No claim of guaranteed marks is made.
