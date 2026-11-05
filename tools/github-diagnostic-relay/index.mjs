// GitHub diagnostic submission relay for installed BitKeyBridge clients.
// Deploy as a Cloudflare Worker with GITHUB_TOKEN and INGEST_TOKEN secrets.
// The sole GitHub destination is fixed; no machine/device/user identifiers,
// free-form logs, stack traces, recovery secrets or arbitrary fields are accepted.
const REPO = "sgennadi/BitKeyBridge";
const SCHEMA = "BKB-DIAG/v1";
const names = new Set(["Startup", "Devices", "Graph", "BitLocker", "LAPS", "Update", "UI", "Helpdesk", "Service", "Other"]);
const failures = new Set(["AccessDenied", "Timeout", "Network", "NotFound", "InvalidConfig", "Unexpected", "IO"]);
const exceptions = new Set(["UnauthorizedAccessException", "SecurityException", "TimeoutException",
  "HttpRequestException", "IOException", "FileNotFoundException", "DirectoryNotFoundException",
  "ArgumentException", "InvalidOperationException", "Win32Exception", "Other"]);
const arches = new Set(["X64", "X86", "Arm64"]);
const topKeys = ["architecture", "buckets", "privacy", "product", "schema", "version"];
const bucketKeys = ["component", "count", "exceptionType", "failure", "fingerprint",
  "firstDay", "hresult", "lastDay"];
const bytes = new TextEncoder();

function keysMatch(value, keys) {
  return value && typeof value === "object" && !Array.isArray(value) &&
    Object.keys(value).sort().join("|") === keys.join("|");
}

function respond(status, message) {
  return new Response(JSON.stringify({ status: message }), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" }
  });
}

async function hash(input) {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", bytes.encode(input)));
}

async function sameSecret(a, b) {
  if (!a || !b) return false;
  const [x, y] = await Promise.all([hash(a), hash(b)]);
  let difference = 0;
  for (let i = 0; i < x.length; i++) difference |= x[i] ^ y[i];
  return difference === 0;
}

async function limitedText(request) {
  const declared = Number(request.headers.get("content-length") || 0);
  if (declared > 10000) throw new RangeError("Large body");
  if (!request.body) throw new SyntaxError("Empty body");
  const reader = request.body.getReader();
  const chunks = [];
  let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > 10000) throw new RangeError("Large body");
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }
  const result = new Uint8Array(size);
  let position = 0;
  for (const chunk of chunks) {
    result.set(chunk, position);
    position += chunk.length;
  }
  return new TextDecoder("utf-8", { fatal: true }).decode(result);
}

function validDay(day) {
  if (typeof day !== "string" || !/^20\d{2}-[01]\d-[0-3]\d$/.test(day))
    return false;
  const date = new Date(day + "T00:00:00Z");
  return !Number.isNaN(date.valueOf()) && date.toISOString().slice(0, 10) === day;
}

async function validatedPayload(obj) {
  if (!keysMatch(obj, topKeys) ||
      obj.schema !== SCHEMA || obj.product !== "BitKeyBridge" ||
      obj.privacy !== "allowlisted-error-counters-only" ||
      typeof obj.version !== "string" ||
      !/^\d{1,3}\.\d{1,3}\.\d{1,3}(?:-rc\.\d{1,3})?$/.test(obj.version) ||
      !arches.has(obj.architecture) ||
      !Array.isArray(obj.buckets) || obj.buckets.length < 1 || obj.buckets.length > 20)
    return null;
  for (const bucket of obj.buckets) {
    if (!keysMatch(bucket, bucketKeys) ||
        !names.has(bucket.component) ||
        !failures.has(bucket.failure) ||
        !exceptions.has(bucket.exceptionType) ||
        typeof bucket.hresult !== "string" || !/^0x[0-9A-F]{8}$/.test(bucket.hresult) ||
        !Number.isInteger(bucket.count) || bucket.count < 1 || bucket.count > 10000 ||
        typeof bucket.fingerprint !== "string" || !/^[a-f0-9]{16}$/.test(bucket.fingerprint) ||
        !validDay(bucket.firstDay) || !validDay(bucket.lastDay) ||
        bucket.firstDay > bucket.lastDay)
      return null;
    const digest = await hash(bucket.component + "|" + bucket.failure + "|" +
      bucket.exceptionType + "|" + bucket.hresult);
    const fingerprint = Array.from(digest.slice(0, 8), n => n.toString(16).padStart(2, "0")).join("");
    if (fingerprint !== bucket.fingerprint) return null;
  }
  return obj;
}

async function handle(request, env) {
  const url = new URL(request.url);
  if (url.pathname !== "/v1/diagnostics" || request.method !== "POST")
    return respond(404, "not-found");
  if (!env || !env.GITHUB_TOKEN || !env.INGEST_TOKEN)
    return respond(503, "not-configured");
  const authorization = request.headers.get("authorization") || "";
  if (!authorization.startsWith("Bearer ") ||
      !(await sameSecret(authorization.slice(7), env.INGEST_TOKEN)))
    return respond(401, "unauthorized");
  if (!/^application\/json(?:\s*;|$)/i.test(request.headers.get("content-type") || ""))
    return respond(415, "json-required");
  let payload;
  try {
    payload = await validatedPayload(JSON.parse(await limitedText(request)));
  } catch (error) {
    return respond(error instanceof RangeError ? 413 : 400, "invalid-report");
  }
  if (!payload) return respond(400, "invalid-report");

  // The worker never receives a GitHub PAT from the client.
  const name = "[BKB-DIAG/v1] " + new Date().toISOString().slice(0, 10) + " " +
    Array.from(crypto.getRandomValues(new Uint8Array(6)),
      n => n.toString(16).padStart(2, "0")).join("");
  const fence = String.fromCharCode(96).repeat(3);
  const issue = {
    title: name,
    body: fence + "json\n" + JSON.stringify(payload, null, 2) + "\n" + fence
  };
  let result;
  try {
    result = await fetch("https://api.github.com/repos/" + REPO + "/issues", {
      method: "POST",
      headers: {
        authorization: "Bearer " + env.GITHUB_TOKEN,
        accept: "application/vnd.github+json",
        "content-type": "application/json",
        "user-agent": "BitKeyBridge-DiagnosticRelay/1",
        "x-github-api-version": "2022-11-28"
      },
      body: JSON.stringify(issue),
      redirect: "error"
    });
    if (!result.ok) return respond(503, "delivery-unavailable");
    const published = await result.json();
    const issueUrl = published && typeof published.html_url === "string"
      ? published.html_url : "";
    if (!/^https:\/\/github\.com\/sgennadi\/BitKeyBridge\/issues\/[0-9]+$/.test(issueUrl))
      return respond(503, "delivery-unconfirmed");
    return new Response(JSON.stringify({ status: "accepted", issueUrl }), {
      status: 201,
      headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" }
    });
  } catch {
    return respond(503, "delivery-unavailable");
  }
}

export default { fetch: handle };
