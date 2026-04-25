# Identity admin gaps

Generated during ticket `honua-io/honua-server-admin#22`. Lists every identity
capability the admin UI surface intentionally promises but does not yet ship,
because the corresponding `honua-server` endpoint does not exist. This file
feeds the `#28` API-coverage audit matrix.

The admin UI ships nav entries and "pending — see follow-up ticket" cards for
each gap so operators can see the full diagnostic intent without the UI
claiming capability the server does not have.

## Gaps

| Capability | Surface in admin UI | Server status | Follow-up |
| --- | --- | --- | --- |
| Per-user API key administration (create / list / revoke / rotate, show-once plaintext on create) | `Pages/Identity/ApiKeys.razor` stub page | No endpoint. Today only `HONUA_ADMIN_PASSWORD` env-var auth via `ApiKeyAuthenticationHandler`. | `honua-server`: add `/api/v1/admin/api-keys` CRUD with show-once plaintext + masked previews. |
| SAML provider configuration | not surfaced | No endpoint, no domain model. | `honua-server`: add SAML provider configuration store + endpoints symmetrical to `OidcProviderEndpoints`. |
| Local password / managed user administration | not surfaced | `ManagedUser` domain exists in `Honua.Core/Features/Identity/Domain/`, no admin endpoints. | `honua-server`: add managed-user / local-password administration endpoints. |
| Clock-skew detection on provider test | "Clock skew check" pending card on `Diagnostics.razor` | `IdentityProviderTestResult` carries no skew field. | `honua-server`: extend `IdentityProviderTestResult` with clock-skew measurement. |
| Claim-mapping coverage validation | "Claim-mapping coverage" pending card on `Diagnostics.razor` | No endpoint. | `honua-server`: add claim-mapping validation endpoint that reports missing claims for a provider's discovery document. |
| Callback URL drift check | "Callback URL drift" pending card on `Diagnostics.razor` | No endpoint. | `honua-server`: add callback-URL drift check endpoint that compares the configured `CallbackPath` against the provider's registered redirect URIs (where introspectable). |
| Browser-based E2E coverage (Playwright or equivalent) for the OIDC provider create → reachability test → delete flow | bUnit-style component tests are also deferred (see test note below) | No E2E harness exists in the admin repo today. | `honua-server-admin`: bootstrap an E2E harness (Playwright or Microsoft.Playwright.NUnit) so the lifecycle E2E from `#22` can land. |
| Production admin authentication. The Blazor WASM build cannot safely carry server-admin credentials in client config; `Program.cs` currently only forwards `HonuaServer:ApiKey` as `X-API-Key` in `Development`, and `DevAuthenticationStateProvider` admits every browser principal. | Configuration note in README; production guidance removed; dev-only header attachment in `Program.cs`. | No real admin-auth provider yet; `ApiKeyAuthenticationHandler` is the only path. | `honua-server-admin`: front the admin UI with a same-origin BFF that injects credentials server-side, or wire a real OIDC bearer-token flow to replace `DevAuthenticationStateProvider`. |
| Server-side has-secret flag on `OidcProviderResponse`. The admin form treats the stored secret as write-only / unknown because the response carries no indicator of whether one is configured. | `••••• (write-only)` placeholder + write-only hint copy on `OidcProviderForm.razor`. | `OidcProviderResponse` does not expose a `HasClientSecret` (or equivalent) flag. | `honua-server`: add a `HasClientSecret` (or equivalent) boolean on `OidcProviderResponse` so the admin UI can disambiguate "secret stored" from "public / PKCE client" without revealing the secret. |

## Test coverage note

The admin repo today only ships xUnit unit tests; no bUnit / Blazor render
test harness is in place. Per the design's pragmatic tradeoff, ticket `#22`
ships:

- xUnit unit tests for `IdentityDiagnostics` (every diagnostic copy branch)
  and `OidcProviderFormModel` (secret write-only behavior, rotate-toggle
  semantics, validation edge cases).
- xUnit integration tests for `HttpIdentityAdminClient` exercised via a fake
  `HttpMessageHandler`, covering the OIDC provider lifecycle (list, create,
  update including secret omission, delete, reachability test) plus the
  catalog endpoints.

Browser-render component tests and the E2E lifecycle test are listed under
the gaps above and depend on the admin repo gaining a render-testing
harness — which is itself recorded as a follow-up.
