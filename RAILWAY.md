# Deploy en Railway (prueba)

## 1. Repo
GitHub: `juanpbeloso/covoiturage-api` → Railway **Deploy from GitHub**.

Root: raíz del repo (donde está el `Dockerfile`).

## 2. Servicios
1. **PostgreSQL**
2. Servicio API (`covoiturage-api`)

## 3. Variables del servicio API

| Variable | Valor |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__MigrateOnStartup` | `true` |
| `EnableSwagger` | `true` (solo mientras probás) |
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` |
| `Jwt__Key` | clave larga (≥32 chars) |
| `Jwt__Issuer` | `SubiteAPI` |
| `Jwt__Audience` | `SubiteApp` |
| `App__BackendUrl` | `https://fearless-unity-production-04ec.up.railway.app` |
| `App__FrontendUrl` | `subite://` |
| `Google__ClientId` | Web Client ID (`….apps.googleusercontent.com`) — misma audiencia del id_token |
| `Apple__ClientId` | `com.subite.app` (Bundle ID / audience del identity token nativo) |
| `MercadoPago__AccessToken` | token TEST |
| `MercadoPago__UseSandbox` | `true` |
| `EnvialoSimple__Enabled` | `true` (o `false` en pruebas: el código sale en logs Railway) |
| `EnvialoSimple__ApiKey` | API key de EnvialoSimple |
| `EnvialoSimple__FromEmail` | email remitente verificado |
| `EnvialoSimple__FromName` | `Subite` (opcional) |

### Reset de contraseña
Flujo: `POST /api/auth/forgot-password` → código 6 dígitos por email → `verify-reset-code` → `reset-password`.

Con `Database__MigrateOnStartup=true`, el redeploy crea la tabla `PasswordResetCodes`.

Si `EnvialoSimple__Enabled=false`, buscá en logs: `Código de reset para …`.

## 4. Dominio
Networking → **Generate Domain**.

Probar:
- `/health` → `{"status":"ok"}`
- `/swagger`

## 5. App mobile (EAS preview)
```text
EXPO_PUBLIC_API_URL=https://fearless-unity-production-04ec.up.railway.app
EXPO_PUBLIC_GOOGLE_WEB_CLIENT_ID=<web client id>
EXPO_PUBLIC_GOOGLE_IOS_CLIENT_ID=<ios client id>
```

Webhook MP: `https://fearless-unity-production-04ec.up.railway.app/api/payments/webhook`
