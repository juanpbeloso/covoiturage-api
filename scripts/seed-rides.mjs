/**
 * Script de seed: crea un conductor, su vehículo y varios viajes vía la API.
 *
 * Uso (con la API corriendo):
 *   node scripts/seed-rides.mjs
 *
 * Variables opcionales:
 *   API_URL          (default http://localhost:5178)
 *   DRIVER_EMAIL     (default conductor.demo@subite.app)
 *   DRIVER_PASSWORD  (default Demo1234!)
 *
 * Requiere Node 18+ (usa fetch nativo).
 */

const API_URL = (process.env.API_URL || "http://localhost:5178").replace(/\/$/, "");
const EMAIL = process.env.DRIVER_EMAIL || "conductor.demo@subite.app";
const PASSWORD = process.env.DRIVER_PASSWORD || "Demo1234!";
const FULL_NAME = "Conductor Demo";

async function api(path, { method = "GET", body, token } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }
  if (!res.ok) {
    const msg = (data && (data.message || data.error)) || `HTTP ${res.status}`;
    throw new Error(`${method} ${path} -> ${msg}`);
  }
  return data;
}

async function getToken() {
  try {
    const login = await api("/api/auth/login", {
      method: "POST",
      body: { email: EMAIL, password: PASSWORD },
    });
    if (login?.accessToken) {
      console.log(`✓ Login OK como ${EMAIL}`);
      return login.accessToken;
    }
  } catch {
    // continuar a registro
  }

  const register = await api("/api/auth/register", {
    method: "POST",
    body: { email: EMAIL, password: PASSWORD, fullName: FULL_NAME, phone: "+5491100000000" },
  });
  if (!register?.accessToken) {
    throw new Error("No se pudo obtener token al registrar el conductor.");
  }
  console.log(`✓ Conductor registrado: ${EMAIL}`);
  return register.accessToken;
}

function futureDate(daysFromNow, hour, minute = 0) {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  d.setHours(hour, minute, 0, 0);
  return d.toISOString();
}

const RIDES = [
  {
    originCity: "Junín",
    originAddress: "Terminal de ómnibus, Av. Rivadavia",
    destinationCity: "Retiro",
    destinationAddress: "Terminal de Retiro",
    departureDateTime: futureDate(1, 7, 30),
    estimatedDurationMinutes: 240,
    totalSeats: 3,
    pricePerSeat: 15255,
    notes: "Salida puntual. Parada en peaje si hace falta.",
    allowsPets: false,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Junín",
    originAddress: "Rotonda principal",
    destinationCity: "Vicente López",
    destinationAddress: "Av. Maipú y Av. del Libertador",
    departureDateTime: futureDate(1, 8, 0),
    estimatedDurationMinutes: 230,
    totalSeats: 3,
    pricePerSeat: 14800,
    notes: "Bajo por Panamericana.",
    allowsPets: true,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Junín",
    originAddress: "Centro",
    destinationCity: "Almagro",
    destinationAddress: "Av. Corrientes y Medrano",
    departureDateTime: futureDate(2, 6, 45),
    estimatedDurationMinutes: 245,
    totalSeats: 3,
    pricePerSeat: 15500,
    notes: "Llegada cerca de subte B.",
    allowsPets: false,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Chacabuco",
    originAddress: "Plaza San Martín",
    destinationCity: "Buenos Aires",
    destinationAddress: "Retiro",
    departureDateTime: futureDate(2, 14, 0),
    estimatedDurationMinutes: 200,
    totalSeats: 3,
    pricePerSeat: 13500,
    notes: "Paso a buscar pasajeros por Ruta 7.",
    allowsPets: false,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Alem",
    originAddress: "Centro",
    destinationCity: "Junín",
    destinationAddress: "Terminal de ómnibus",
    departureDateTime: futureDate(3, 9, 0),
    estimatedDurationMinutes: 90,
    totalSeats: 2,
    pricePerSeat: 8500,
    notes: "Viaje directo por ruta provincial.",
    allowsPets: false,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Luján",
    originAddress: "Basílica y terminal",
    destinationCity: "Junín",
    destinationAddress: "Terminal de ómnibus",
    departureDateTime: futureDate(3, 17, 30),
    estimatedDurationMinutes: 120,
    totalSeats: 3,
    pricePerSeat: 9200,
    notes: "Vuelta a Junín, asientos disponibles.",
    allowsPets: true,
    allowsSmoking: false,
    allowsLuggage: true,
  },
  {
    originCity: "Junín",
    originAddress: "Terminal de ómnibus",
    destinationCity: "Nuñez",
    destinationAddress: "Av. Libertador y Cabildo",
    departureDateTime: futureDate(4, 7, 0),
    estimatedDurationMinutes: 235,
    totalSeats: 3,
    pricePerSeat: 15100,
    notes: "Ideal para zona norte.",
    allowsPets: false,
    allowsSmoking: false,
    allowsLuggage: true,
  },
];

async function main() {
  console.log(`API: ${API_URL}`);
  const token = await getToken();

  console.log("→ Registrando vehículo del conductor...");
  await api("/api/vehicles/me", {
    method: "PUT",
    token,
    body: {
      brand: "Renault",
      model: "Logan",
      color: "Blanco",
      licensePlate: "AB123CD",
      year: 2020,
    },
  });
  console.log("✓ Vehículo listo");

  let ok = 0;
  for (const ride of RIDES) {
    try {
      const created = await api("/api/rides", { method: "POST", token, body: ride });
      ok++;
      console.log(`✓ Viaje: ${ride.originCity} → ${ride.destinationCity} (${created.id})`);
    } catch (err) {
      console.error(`✗ Falló ${ride.originCity} → ${ride.destinationCity}: ${err.message}`);
    }
  }

  console.log(`\nListo: ${ok}/${RIDES.length} viajes creados.`);
  console.log("Abrí la app → Buscar (Junín → Retiro) para verlos.");
}

main().catch((err) => {
  console.error("\nError fatal:", err.message);
  console.error("¿La API está corriendo y accesible en", API_URL, "?");
  process.exit(1);
});
