/**
 * Smoke test de notificaciones (inbox + endpoint test).
 * Uso: node scripts/test-notifications.mjs
 */
const API_URL = (process.env.API_URL || "http://127.0.0.1:5178").replace(/\/$/, "");
const EMAIL = process.env.DRIVER_EMAIL || "conductor.demo@subite.app";
const PASSWORD = process.env.DRIVER_PASSWORD || "Demo1234!";

async function req(path, { method = "GET", body, token } = {}) {
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
  return { status: res.status, data };
}

async function main() {
  let login = await req("/api/auth/login", {
    method: "POST",
    body: { email: EMAIL, password: PASSWORD },
  });
  if (login.status !== 200 || !login.data?.accessToken) {
    login = await req("/api/auth/register", {
      method: "POST",
      body: {
        email: EMAIL,
        password: PASSWORD,
        fullName: "Conductor Demo",
      },
    });
  }
  if (!login.data?.accessToken) {
    console.error("FAIL auth", login.status);
    process.exit(1);
  }

  const token = login.data.accessToken;

  const test = await req("/api/notifications/test", {
    method: "POST",
    token,
    body: { title: "Prueba API", body: "Inbox de notificaciones OK" },
  });
  console.log("POST /test", test.status, test.data?.message || test.data?.error);

  const list = await req("/api/notifications", { token });
  console.log("GET /notifications", list.status, "count=", Array.isArray(list.data) ? list.data.length : 0);

  const unread = await req("/api/notifications/unread-count", { token });
  console.log("GET /unread-count", unread.status, "count=", unread.data?.count);

  const ok =
    test.status === 200 &&
    list.status === 200 &&
    Array.isArray(list.data) &&
    list.data.length > 0 &&
    unread.data?.count >= 1;

  console.log(ok ? "PASS" : "FAIL");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
