import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import test from "node:test";
import relay from "./index.mjs";

const url = "https://bkb-relay.example.workers.dev/v1/diagnostics";
const credential = "a".repeat(48);
const env = { INGEST_TOKEN: credential, GITHUB_TOKEN: "never-shipped-to-clients" };
const fingerprint = createHash("sha256").update("Devices|Timeout|TimeoutException|0x80131505")
  .digest("hex").slice(0, 16);
const good = {
  schema: "BKB-DIAG/v1",
  product: "BitKeyBridge",
  version: "0.35.0-rc.6",
  architecture: "X64",
  buckets: [{
    component: "Devices",
    failure: "Timeout",
    exceptionType: "TimeoutException",
    hresult: "0x80131505",
    count: 3,
    fingerprint,
    firstDay: "2026-11-05",
    lastDay: "2026-11-05"
  }],
  privacy: "allowlisted-error-counters-only"
};

function request(body, auth = credential, override = {}) {
  return new Request(url, {
    method: "POST",
    headers: {
      authorization: "Bearer " + auth,
      "content-type": "application/json",
      ...override
    },
    body: typeof body === "string" ? body : JSON.stringify(body)
  });
}
function clone() { return structuredClone(good); }

test("does not accept anonymous, wrong token or non-JSON uploads", async () => {
  assert.equal((await relay.fetch(request(good, "wrong"), env)).status, 401);
  assert.equal((await relay.fetch(request(good, credential, { "content-type": "text/plain" }), env)).status, 415);
  assert.equal((await relay.fetch(new Request(url), env)).status, 404);
  assert.equal((await relay.fetch(request(good), {})).status, 503);
});

test("rejects raw logs, unexpected properties, altered fingerprint, invalid dates and 10KB payloads", async () => {
  const privateData = clone();
  privateData.buckets[0].username = "MY_DOMAIN_USER";
  assert.equal((await relay.fetch(request(privateData), env)).status, 400);
  const altered = clone();
  altered.buckets[0].fingerprint = "b".repeat(16);
  assert.equal((await relay.fetch(request(altered), env)).status, 400);
  const date = clone();
  date.buckets[0].lastDay = "2026-02-31";
  assert.equal((await relay.fetch(request(date), env)).status, 400);
  assert.equal((await relay.fetch(request("SECRET_MACHINE".repeat(1100)), env)).status, 413);
});

test("valid typed counters become a GitHub issue without passing the client token", async () => {
  const originalFetch = globalThis.fetch;
  let posted = false;
  try {
    globalThis.fetch = async (endpoint, options) => {
      posted = true;
      assert.equal(endpoint, "https://api.github.com/repos/sgennadi/BitKeyBridge/issues");
      assert.equal(options.headers.authorization, "Bearer " + env.GITHUB_TOKEN);
      assert.ok(!JSON.stringify(options).includes(credential));
      const issue = JSON.parse(options.body);
      assert.match(issue.title, /^\[BKB-DIAG\/v1\] 20\d\d-[01]\d-[0-3]\d [0-9a-f]{12}$/);
      assert.match(issue.body, /^\x60{3}json\n/);
      assert.ok(issue.body.includes(fingerprint));
      assert.ok(!issue.body.includes(credential));
      return new Response(JSON.stringify({
        html_url: "https://github.com/sgennadi/BitKeyBridge/issues/123"
      }), { status: 201 });
    };
    const response = await relay.fetch(request(good), env);
    assert.equal(response.status, 201);
    assert.deepEqual(await response.json(), {
      status: "accepted",
      issueUrl: "https://github.com/sgennadi/BitKeyBridge/issues/123"
    });
    assert.ok(posted);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("GitHub 401/429/503 failures retain local counters through a 503 response", async () => {
  const originalFetch = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response("rate limited", { status: 429 });
    const result = await relay.fetch(request(good), env);
    assert.equal(result.status, 503);
    assert.deepEqual(await result.json(), { status: "delivery-unavailable" });
  } finally {
    globalThis.fetch = originalFetch;
  }
});
