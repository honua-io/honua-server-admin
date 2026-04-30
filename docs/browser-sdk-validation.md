# Browser SDK Validation

Issue
[#67](https://github.com/honua-io/honua-server-admin/issues/67) tracks the
admin UI browser/WASM validation for `Honua.Sdk.Admin`.

The admin app is a Blazor WebAssembly host. That means SDK usage must stay on
the browser-safe REST path:

- `Honua.Sdk.Admin` is consumed as a NuGet package.
- The UI adapter remains `IHonuaAdminClient`.
- SDK-backed calls run through the app-provided `HttpClient` and the browser
  fetch stack.
- Native gRPC/HTTP2 is not part of the admin browser path. `Honua.Sdk.Grpc`
  should stay out of this app until a gRPC-Web endpoint and browser transport
  plan are available.

## Current Validation

The PR gate validates three layers:

| Gate | Coverage |
| --- | --- |
| Unit smoke | `HonuaAdminClientBrowserSafetyTests` exercises `IHonuaAdminClient -> HonuaAdminClient -> Honua.Sdk.Admin` with a supplied `HttpClient` handler for observability and deploy-preflight calls. |
| Blazor publish | `Publish Verification` publishes `src/Honua.Admin` as WebAssembly and verifies the published output plus bundle budgets. |
| Stub UI smoke | `AdminQualityGateTests` renders release-critical routes with deterministic local stubs so UI regressions do not require a live server. |

The unit smoke is intentionally same-process and same-origin. It proves the
adapter does not require native transports or static credentials. A live
browser/CORS run should be added once the fake-server harness exists.

## Auth Boundary

`HonuaServer:ApiKey` is development-only. Blazor WebAssembly configuration is
downloaded by clients, so production builds must not forward a static admin API
key from browser config. `Program.cs` only attaches `X-API-Key` in Development;
non-Development builds log a warning and refuse to forward it.

Production deployments should use one of these patterns:

- same-origin BFF that injects privileged admin credentials server-side;
- delegated operator bearer tokens from the real OIDC login flow;
- another app-owned auth flow that never places privileged server secrets in
  `wwwroot`, generated static config, or downloaded appsettings files.

## CORS Boundary

Same-origin deployments avoid browser CORS entirely and are the preferred admin
shape when a BFF is present. Cross-origin deployments need honua-server to allow
the admin app origin, methods, and credential headers used by the REST admin
surface. That server policy is outside this repository, but admin UI changes
must not bypass it by adding native transports or hidden static credentials.

The SDK package matrix lives in
[`honua-sdk-dotnet/docs/browser-wasm-support.md`](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/browser-wasm-support.md).
