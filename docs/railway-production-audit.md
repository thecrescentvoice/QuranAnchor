# QuranAnchor — Railway production deployment audit

## Diagnosis from repository inspection

The supplied Railway frames (`ExecutionStrategy...` and `RunAsync`) are framework call sites, not the root exception. The original exception and Railway environment values are needed to prove one exact cause. Repository inspection does establish two production blockers that can independently make startup fail:

- `appsettings.json` had a checked-in `Server=localhost;User=root;Password=CHANGE_ME` connection string. In a Railway app container, `localhost` is the app container, not the separate MySQL service. No Railway environment override means EF cannot connect.
- `Database:AutoMigrate` defaults to `false`, while the hosted initializer immediately makes Identity and seed queries that require the migration-created tables. On a new/unmigrated database, startup fails on missing schema. This can surface under the EF execution-strategy frame shown in logs.

Neither of these can be identified as the sole cause without the full inner exception. The startup initializer now logs the full exception and actionable diagnostic context without logging secrets; it rethrows rather than hiding failure. With migrations disabled, it checks for unapplied migrations and reports them before Identity/seeding accesses tables. `EnsureCreated` is deliberately not used.

Repository migration inspection found three migrations (`InitialCreate`, `AddCertificates`, `AddMoreFeatures`) and a model snapshot in the Infrastructure assembly, which is explicitly configured as the migrations assembly. `AddMoreFeatures` drops `PlanRows` and old plan columns. This is potentially destructive to databases containing data in those columns/table; back up and review its generated SQL/data-retention impact before applying it to a populated production database. No migration is automatically enabled by this change.

## Railway configuration

Configure Railway service variables/secrets (never commit their values):

- `ConnectionStrings__Default`: connection string for the Railway MySQL service, using its **private/internal host** where available, database name, user and password. Do not use `localhost`. Ensure the service is allowed to connect and the DB user can create/alter tables if migrations run.
- `Database__ServerVersion`: actual deployed MySQL version in provider format, e.g. `8.0.36-mysql`. Verify against the Railway database rather than copying the example blindly.
- Migration strategy: review and back up first. Either run reviewed EF migrations as a release/deployment step, or explicitly set `Database__AutoMigrate=true` for a controlled deployment after review. Keep it false when using an external migration step. Do not use `EnsureCreated` or manually mark unapplied migrations as applied.
- `App__BaseUrl`: public HTTPS origin, so background-job emails use the deployed site URL instead of localhost.
- `Seed__SuperAdmin__Email` and `Seed__SuperAdmin__Password`: optional first-admin bootstrap secrets. The seed happens only when no Super Admin exists; the new account is marked to change its password. Remove the bootstrap password from Railway after successful creation.
- `Paystack__SecretKey`: secret only if Paystack payments are enabled.
- `Email__Provider=Resend`, `Email__ApiKey`, and `Email__FromAddress`: only if using Resend; otherwise the configured logging sender is not production email delivery.

ASP.NET Core configuration maps double underscores (`__`) to nested keys. The DB connection string is intentionally blank in checked-in config, causing clear startup validation if deployment configuration is absent.

## Deployment/runtime review

- Dockerfile uses .NET 10 SDK and ASP.NET runtime, restores and publishes the Presentation project, includes fontconfig/DejaVu fonts for Linux PDF rendering, and binds `ASPNETCORE_URLS` to Railway's `$PORT` (8080 fallback).
- The app enables forwarded headers before HTTPS redirection and uses secure cookies outside Development. The existing forwarded-header setup clears trusted proxy restrictions; preserve a trusted Railway ingress boundary and reassess this if the app can be reached directly, since untrusted forwarded headers affect scheme/client-IP handling.
- Files uploaded by the app should not be assumed durable on an ephemeral container filesystem. Configure persistent storage or object storage if production uploads must survive deploys/restarts.
- ASP.NET Data Protection keys are not configured for persistence. Configure a protected persistent key store if sessions, password reset tokens, or other protected payloads must remain valid across container replacement/replicas.
- The hosted scheduler runs in every app replica. Its database claim logic uses a unique notification key to prevent many duplicate sends, but the service is not a distributed singleton; review multi-replica execution for non-notification jobs such as session generation.

## Verification status and remaining checks

This audit was performed through GitHub repository inspection and commits. No local shell, .NET SDK, Docker engine, Railway database or test execution is available in this session, so `dotnet restore`, `dotnet build`, automated tests, `dotnet ef migrations list/has-pending-model-changes`, Docker image build, migration application, and runtime connectivity have **not** been run or verified. The exact startup exception also remains unconfirmed until the full inner exception is available from Railway logs. Run these checks in CI or a local .NET 10 environment before production rollout; apply migrations to a database copy and compare schema/data before touching production.
