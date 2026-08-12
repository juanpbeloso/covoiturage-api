/**
 * Smoke test Didit endpoints (no secrets printed).
 * Uso: node scripts/test-didit.mjs
 */
const API_URL = (process.env.API_URL || "http://localhost:5178").replace(/\/$/, "");
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
        phone: "+5491100000000",
      },
    });
  }
  if (!login.data?.accessToken) {
    console.error("FAIL login/register", login.status, login.data?.message || login.data?.error);
    process.exit(1);
  }

  const token = login.data.accessToken;
  const userId = login.data.user?.id;
  console.log("user.isVerified=", login.data.user?.isVerified);

  const me = await req("/api/auth/me", { token });
  console.log("GET /me", me.status, "isVerified=", me.data?.isVerified);

  const session = await req("/api/verification/didit/session", {
    method: "POST",
    token,
  });
  console.log(
    "POST /session",
    session.status,
    session.data?.error || session.data?.message || session.data?.status || ""
  );

  const statusBefore = await req("/api/verification/didit/status", { token });
  console.log(
    "GET /status",
    statusBefore.status,
    "isVerified=",
    statusBefore.data?.isVerified,
    statusBefore.data?.message || ""
  );

  const webhook = await req("/api/webhooks/didit", {
    method: "POST",
    body: {
      webhook_type: "status.updated",
      session_id: "00000000-0000-4000-8000-000000000099",
      status: "Approved",
      event_id: "test-event-1",
      vendor_data: userId,
    },
  });
  console.log("POST /webhooks/didit", webhook.status);

  const statusAfter = await req("/api/verification/didit/status", { token });
  console.log(
    "GET /status after webhook",
    statusAfter.status,
    "isVerified=",
    statusAfter.data?.isVerified,
    "sessionStatus=",
    statusAfter.data?.status
  );

  const meAfter = await req("/api/auth/me", { token });
  console.log("GET /me after webhook isVerified=", meAfter.data?.isVerified);

  const ok =
    session.status === 503 &&
    (session.data?.error === "DIDIT_001" || String(session.data?.message || "").includes("WorkflowId")) &&
    webhook.status === 200 &&
    meAfter.data?.isVerified === true;

  console.log(ok ? "PASS" : "FAIL (check WorkflowId message + webhook IsVerified)");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
