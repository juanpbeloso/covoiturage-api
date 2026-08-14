# Pendiente: scraping de precios de referencia

## Objetivo
Actualizar automáticamente `ReferencePrices` (tope vs colectivo) consultando sitios como:

- https://www.plataforma10.com.ar/
- https://www.centraldepasajes.com.ar/

## Enfoque sugerido
1. Tabla `MonitoredRoute` (origen, destino, activo).
2. Worker aparte (Playwright/Puppeteer) — no dentro del proceso de la API.
3. Job diario en Railway + botón “Sincronizar” en el admin.
4. Siempre mantener carga manual en `/precios-referencia` como fallback.

## Notas
- Sitios con mucho JS → headless browser, no HttpClient simple.
- Scrapers frágiles: logs + alerta si falla N días.
- Revisar ToS de cada sitio; uso interno para referencia.

## Estado
Aplazado hasta cerrar Split MP + admin pricing operativo.
