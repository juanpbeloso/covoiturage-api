# Deploy en Railway (prueba)

## 1. Repo
Este proyecto se publica en GitHub y se conecta desde Railway → **Deploy from GitHub**.

Root directory: raíz de `subite-api` (donde está el `Dockerfile`).

## 2. Servicios en Railway
1. **Add PostgreSQL** (plugin).
2. Servicio **SubiteAPI** (este repo).

## 3. Variables del servicio API

| Variable | Valor |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__MigrateOnStartup` | `true` |
| `EnableSwagger` | `true` (solo mientras probás) |
| `DATABASE_URL` | `${{Postgres.DATABASE_URL}}` (referencia al plugin) |
| `Jwt__Key` | clave larga (≥32 chars), distinta a la local |
| `Jwt__Issuer` | `SubiteAPI` |
| `Jwt__Audience` | `SubiteApp` |
| `App__BackendUrl` | `https://TU-DOMINIO.up.railway.app` (después de Generate Domain) |
| `App__FrontendUrl` | `subite://` |
| `MercadoPago__AccessToken` | tu token TEST |
| `MercadoPago__UseSandbox` | `true` |

Opcional en vez de `DATABASE_URL`:

`ConnectionStrings__DefaultConnection` = connection string Npgsql del Postgres de Railway.

## 4. Dominio
Settings → Networking → **Generate Domain**.

Probar:
- `https://TU-DOMINIO.up.railway.app/health` → `{"status":"ok"}`
- `https://TU-DOMINIO.up.railway.app/swagger`

## 5. App mobile
Actualizar `EXPO_PUBLIC_API_URL` (EAS env `preview` o `.env`) con esa URL HTTPS y rebuild si es IPA.
