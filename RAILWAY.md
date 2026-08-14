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
| `MercadoPago__AccessToken` | token de la **aplicación marketplace** (Subite) |
| `MercadoPago__ClientId` | Application ID (OAuth) |
| `MercadoPago__ClientSecret` | Client Secret (OAuth) |
| `MercadoPago__RedirectUri` | `https://<api>/api/conductores/mercadopago/callback` (debe coincidir en MP) |
| `MercadoPago__UseSandbox` | `true` |
| `MercadoPago__PaymentMode` | `wallet_only` o `all` |

### Split de pagos (marketplace 1:1)
- El conductor conecta su MP vía `GET /api/conductores/mercadopago/conectar`.
- Las preferencias se crean con el **AccessToken del conductor** + `marketplace_fee` = comisión de Subite (`PlatformSettings`, default 12,5%).
- El pasajero sigue viendo Checkout Pro normal.
- Sin cuenta MP conectada: no se puede publicar viaje ni cobrar reservas.

### Reset de contraseña
| `EnvialoSimple__Enabled` | `true` (o `false` en pruebas: el código sale en logs Railway) |
| `EnvialoSimple__ApiKey` | API key de EnvialoSimple |
| `EnvialoSimple__FromEmail` | email remitente verificado |
| `EnvialoSimple__FromName` | `Subite` (opcional) |
| `Admin__Email` | email del admin inicial (ej. `admin@subiteapp.com.ar`) |
| `Admin__Password` | contraseña del admin (cambiar en producción) |
| `Admin__FullName` | `Administrador Subite` (opcional) |

### Panel admin
Login: `POST /admin/auth/login` (usuario con rol `Admin`).
Comisión editable: `GET/PUT /admin/settings` y `/admin/settings/commission`.
Pricing mutaciones requieren JWT admin en `/api/pricing-config` y `/api/reference-prices`.

Si `Database__MigrateOnStartup=true`, el redeploy crea admin seed + tabla `PlatformSettings`.
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
