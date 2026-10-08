# tengudance

My personal home page at https://tengudance.com, and the first real test of my
deployment pipeline. It is a small ASP.NET Core (.NET 10) app that renders one
server-side page with a status panel showing whether the deployment is wired up
correctly. No database, no JavaScript, no external fonts, scripts or trackers, and
no front-end build step.

```
src/Landing/              the app
  Program.cs              services, forwarded headers, endpoints, --healthcheck probe
  Health/                 one class per diagnostic check, plus Diagnostic result helpers
  Services/               turns the health check results into the public report
  Pages/Index.cshtml      the home page
  wwwroot/site.css        the only static file
tests/Landing.Tests/      xUnit tests against an in-memory server
Dockerfile                multi-stage build of the runtime image
.github/workflows/        build, test, publish and smoke test
```

## How a request reaches the app in production

```
browser ──HTTPS──▶ Caddy container ──HTTP :8080──▶ this container
                   (terminates TLS)   private Docker network
```

1. The browser connects to `https://tengudance.com` on the VPS.
2. Caddy terminates TLS and forwards the request over plain HTTP to this container
   on port 8080, adding `X-Forwarded-Proto: https`, `X-Forwarded-Host` and
   `X-Forwarded-For`.
3. The app trusts those headers only from private network addresses (10.0.0.0/8,
   172.16.0.0/12, 192.168.0.0/16), which is where Caddy connects from. The
   forwarded-headers middleware runs first, so everything after it sees the scheme
   and host the browser used.

The container publishes no host ports, so only Caddy can reach it. The app has no
HTTPS redirection or HSTS of its own; that is Caddy's job.

Both containers are run by Docker Compose from a separate infrastructure repo. The
app container has a 256 MB memory limit (it uses about 35–55 MB) and is given
`EXPECTED_HOST=tengudance.com`.

## Endpoints

| Path       | Returns |
|------------|---------|
| `/`        | The home page with the status panel. Always 200. |
| `/status`  | The status checks as JSON: 200 when all pass, 503 when any fail. |
| `/healthz` | `ok` with 200 whenever the process is running. Liveness only. |
| `/version` | The commit SHA baked in at build time, or `unknown`, as plain text. |

Every response has `Cache-Control: no-store`.

`/healthz` deliberately ignores the status checks. The server's deploy script
aborts if a container is unhealthy, and a proxy or configuration problem should
show up on the status panel, not stop the deploy or restart the container.

## Environment variables

| Variable                 | Set by | Purpose |
|--------------------------|--------|---------|
| `APP_VERSION`            | Docker build argument of the same name | Commit SHA shown by `/version` and in the page footer. Unset or blank means `unknown`, which fails the build version check. |
| `EXPECTED_HOST`          | The infrastructure repo's Compose file | Host name requests should be addressed to, e.g. `tengudance.com`. Unset means the host check is skipped, not failed. |
| `ASPNETCORE_HTTP_PORTS`  | The Dockerfile (`8080`) | Port Kestrel listens on, on all interfaces. |

## Status checks

The status panel and `/status` use ASP.NET Core's built-in health checks
(`HealthCheckService`); there are no extra packages. Each check is a class in
`src/Landing/Health` that returns a result from the `Diagnostic` helpers:

| Check             | Passes when | Fails with likely cause |
|-------------------|-------------|-------------------------|
| Build version     | `APP_VERSION` is set | Image built without the build argument |
| Secure connection | The request is HTTPS after forwarded-header processing | Site reached directly, or proxy headers not trusted |
| Expected host     | The request host matches `EXPECTED_HOST` (skipped if unset) | Proxy not forwarding the host, or wrong `EXPECTED_HOST` |
| Site content      | `wwwroot/site.css` exists and is not empty | Content files missing from the image |

`DiagnosticReportService` runs the checks on each request to `/` or `/status` and
builds the report. When a check fails, the page lists it with its message and
likely cause and still returns 200; `/status` returns 503.

Everything in the report is public, so checks write fixed plain-English text and
never include request values, environment values, file paths or exception
details. If a check throws, the report only says the check could not be completed;
the exception goes to the logs. The health check service logs every failed check
(`fail: Microsoft.Extensions.Diagnostics.HealthChecks...`) with its message, so
`docker logs` shows what went wrong.

### Adding a check

1. Add a class to `src/Landing/Health` that implements `IHealthCheck` and returns
   `Diagnostic.Pass()`, `Diagnostic.Fail("what is wrong", "likely cause")` or
   `Diagnostic.Skip("why")`. Use `IHttpContextAccessor` if it needs the request.
2. Register it in `Program.cs` with `.AddCheck<YourCheck>("Name shown on the page")`.

## Running locally

### With dotnet

```sh
dotnet build
dotnet test
APP_VERSION=local EXPECTED_HOST=tengudance.com dotnet run --project src/Landing -- --urls http://localhost:8080
```

Requests from localhost are trusted as a proxy too, so the proxy simulation
below works against `dotnet run` as well.

### With Docker

Build with a fake commit SHA and run it as the server does:

```sh
docker build --build-arg APP_VERSION=fakesha0123456789 -t tengudance-landing .
docker run -d --name landing --memory 256m -e EXPECTED_HOST=tengudance.com \
  -p 127.0.0.1:8080:8080 tengudance-landing
docker inspect --format '{{.State.Health.Status}}' landing   # healthy within a few seconds
```

Direct requests, with no proxy in front. The secure connection and expected host
checks fail, which is correct:

```sh
curl -i http://127.0.0.1:8080/healthz   # 200 ok
curl http://127.0.0.1:8080/version      # fakesha0123456789
curl -i http://127.0.0.1:8080/status    # 503, two checks failed
curl http://127.0.0.1:8080/             # "Some systems need attention"
```

Simulating Caddy, which is the closest local equivalent of production. Everything
should pass. This works because Docker's port forwarding connects from the bridge
gateway address (172.17.0.1), which is in a trusted range:

```sh
P=(-H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: tengudance.com")
curl -i "${P[@]}" http://127.0.0.1:8080/status   # 200, all checks passed
curl "${P[@]}" http://127.0.0.1:8080/            # "All systems working"
```

Other useful checks:

```sh
docker exec landing id -u                                 # 1654, the non-root "app" user
docker exec landing dotnet Landing.dll --healthcheck; echo $?   # 0
docker stats --no-stream landing                          # memory use against the 256 MB limit
docker rm -f landing && docker rmi tengudance-landing
```

## Container health check

The runtime image has no curl or wget, so the app probes itself. Run with
`--healthcheck`, it requests `http://localhost:8080/healthz`, exits 0 on success or
1 on any failure, and never starts the web server. The Dockerfile's `HEALTHCHECK`
runs `dotnet Landing.dll --healthcheck` every 2 seconds while starting up (so the
container is healthy within seconds) and every 30 seconds after that.

## Image build and tags

The `Dockerfile` has two stages:

- `build` (`mcr.microsoft.com/dotnet/sdk:10.0.401`) restores and publishes
  `src/Landing` only. `.dockerignore` limits the build context to that project.
- `runtime` (`mcr.microsoft.com/dotnet/aspnet:10.0.12`) contains the published app,
  listens on 8080, runs as the image's non-root `app` user, and sets `APP_VERSION`
  from the build argument as its last layer.

Base images are pinned to exact versions. To update them, change both tags to a
matching SDK and runtime release and rebuild.

`.github/workflows/build.yml`:

- On every pull request and push to `main`: restore, build and test in Release.
- On push to `main` only, after the tests pass: log in to ghcr.io with the
  workflow's `GITHUB_TOKEN` (the publish job alone has `packages: write`), then
  build and push `ghcr.io/matt-hue/tengudance-landing` tagged with the full commit
  SHA and with `main`, passing the SHA as `APP_VERSION`.
- Then pull the pushed image, run it with a 256 MB limit, wait for it to be
  healthy, and check `/`, `/healthz` and that `/version` returns the commit SHA.
  It does not call `/status`, because without a proxy that correctly returns 503.

Registry paths must be lowercase, so the image name is `matt-hue` even though the
GitHub account is `Matt-hue`. A newer push to the same branch cancels a run still
in progress.
