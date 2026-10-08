# tengudance

A small, server-rendered personal home page and deployment-pipeline smoke test. It has no database, external runtime calls, or front-end build step.

## Run locally

Run the app with the .NET 10 SDK:

```sh
dotnet run --project src/Landing
```

The app listens on the development URL printed by `dotnet run`. Without `APP_VERSION`, the version diagnostic is expected to fail and `/version` returns `unknown`.

Build and run the container:

```sh
docker build --build-arg APP_VERSION="$(git rev-parse HEAD)" -t tengudance-landing .
docker run --rm -p 8080:8080 -e EXPECTED_HOST=tengudance.com tengudance-landing
```

The container listens on port 8080 and runs as the non-root runtime-image user.

## Endpoints

- `/` renders the home page and current diagnostics. It always responds with 200, including when checks fail.
- `/status` returns the same diagnostic report as JSON, with 503 if a check fails.
- `/healthz` is a liveness endpoint and returns 200 while the app is running.
- `/version` returns the baked-in commit SHA as plain text.

Responses are sent with `Cache-Control: no-store`.

## Environment

- `EXPECTED_HOST` (optional): expected request host, such as `tengudance.com`. When omitted, the host diagnostic is skipped.
- `APP_VERSION` (optional): commit SHA baked into the image through the Docker build argument of the same name. If absent, the endpoint returns `unknown` and the version diagnostic fails.

## Adding a diagnostic check

Create a class implementing `IHealthCheck` in `src/Landing/Health`. Register it in the `AddHealthChecks()` list in `src/Landing/Program.cs`, then add its public-facing name and messages in `DiagnosticReportService`. Keep diagnostic details in server logs; the report must only contain safe, plain-English messages.

## Image and CI

Pull requests restore, build, and test the solution. Pushes to `main` do the same, then publish `ghcr.io/matt-hue/tengudance-landing` with both the full commit SHA and `main` tags. The full SHA is passed to the Docker build as `APP_VERSION`. CI starts the pushed image, waits for its Docker health check, and verifies `/` and `/healthz`.
