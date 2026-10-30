"""
Local fake Cloudinary used ONLY to verify tools/c0/CloudinaryC0Tool without a real account.
It emulates the endpoints the tool uses (Admin API listing, raw upload, signed private download,
rename, destroy, CDN delivery) and logs every request to requests.log for later assertions.
Usage: python3 fake_cloudinary.py <port> <log-file>
"""
import json, re, sys, time, uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

PORT, LOG = int(sys.argv[1]), sys.argv[2]
PDF = b"%PDF-1.4\n% fake asset served by fake_cloudinary\n%%EOF\n"
ASSETS = {}   # (type, public_id) -> metadata

def seed():
    # pre-existing "legacy" CV assets: 3 public (one orphan), 1 authenticated, 120 extra public to force pagination
    for pid in ["wazzuf-jobs/cvs/legacy-a", "wazzuf-jobs/cvs/legacy-b", "wazzuf-jobs/cvs/orphan-c"]:
        ASSETS[("upload", pid)] = {"created_at": "2026-05-14T10:00:00Z", "bytes": 2048}
    ASSETS[("authenticated", "wazzuf-jobs/cvs/private-d")] = {"created_at": "2026-10-01T10:00:00Z", "bytes": 1024}
    for i in range(120):
        ASSETS[("upload", f"wazzuf-jobs/cvs/bulk-{i:03d}")] = {"created_at": "2026-05-20T10:00:00Z", "bytes": 100 + i}

def resource(t, pid, base):
    return {"public_id": pid, "format": "", "version": 1, "resource_type": "raw", "type": t,
            "created_at": ASSETS[(t, pid)]["created_at"], "bytes": ASSETS[(t, pid)]["bytes"],
            "url": f"{base}/res/raw/{t}/v1/{pid}", "secure_url": f"{base}/res/raw/{t}/v1/{pid}"}

def form_fields(handler, body):
    ctype = handler.headers.get("Content-Type", "")
    if "multipart/form-data" in ctype:
        return {m.group(1).decode(): m.group(2).decode(errors="replace")
                for m in re.finditer(rb'name="([^"]+)"\r\n\r\n(.*?)\r\n--', body, re.S) if m.group(1) != b"file"}
    return {k: v[0] for k, v in parse_qs(body.decode(errors="replace")).items()}

class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass

    def record(self, extra=None):
        with open(LOG, "a") as f:
            f.write(json.dumps({"t": time.time(), "method": self.command, "path": urlparse(self.path).path, **(extra or {})}) + "\n")

    def send(self, code, payload=None, raw=None, ctype="application/json"):
        data = raw if raw is not None else json.dumps(payload or {}).encode()
        self.send_response(code); self.send_header("Content-Type", ctype); self.send_header("Content-Length", str(len(data))); self.end_headers()
        if self.command != "HEAD": self.wfile.write(data)

    def base(self): return f"http://localhost:{PORT}"

    def read_body(self):
        if "chunked" in self.headers.get("Transfer-Encoding", "").lower():
            data = b""
            while True:
                size = int(self.rfile.readline().strip().split(b";")[0], 16)
                if size == 0:
                    self.rfile.readline(); return data
                data += self.rfile.read(size); self.rfile.readline()
        return self.rfile.read(int(self.headers.get("Content-Length", 0) or 0))

    def do_HEAD(self): self.do_GET()

    def do_GET(self):
        u = urlparse(self.path); q = parse_qs(u.query); self.record()
        m = re.match(r"^/v1_1/[^/]+/resources/raw(?:/(upload|authenticated|private))?$", u.path)
        if m:
            t = m.group(1) or q.get("type", ["upload"])[0]; items = sorted(pid for (tt, pid) in ASSETS if tt == t)
            start = int(q.get("next_cursor", ["0"])[0] or 0); size = int(q.get("max_results", ["500"])[0])
            size = min(size, 50)  # force pagination
            page = items[start:start + size]; nxt = str(start + size) if start + size < len(items) else None
            body = {"resources": [resource(t, p, self.base()) for p in page]}
            if nxt: body["next_cursor"] = nxt
            return self.send(200, body)
        if re.match(r"^/v1_1/[^/]+/raw/download$", u.path):
            needed = {"signature", "api_key", "timestamp", "public_id", "type"}
            if not needed.issubset(q): return self.send(401, {"error": {"message": "unsigned"}})
            if ("%s" % q["type"][0], q["public_id"][0]) not in ASSETS: return self.send(404, {"error": {"message": "not found"}})
            return self.send(200, raw=PDF, ctype="application/pdf")
        m = re.match(r"^/res/raw/(upload|authenticated|private)/v\d+/(.+)$", u.path)
        if m:
            t, pid = m.group(1), m.group(2)
            if (t, pid) not in ASSETS: return self.send(404, raw=b"not found", ctype="text/plain")
            if t != "upload": return self.send(401, raw=b"unauthorized", ctype="text/plain")
            return self.send(200, raw=PDF, ctype="application/pdf")
        return self.send(404, {"error": {"message": "unknown endpoint"}})

    def do_POST(self):
        u = urlparse(self.path); body = self.read_body()
        f = form_fields(self, body)
        if "--debug" in sys.argv:
            sys.stderr.write(f"POST {u.path} ctype={self.headers.get('Content-Type')}\n{body[:700]!r}\n\n"); sys.stderr.flush()
        if re.match(r"^/v1_1/[^/]+/raw/upload$", u.path):
            t = f.get("type", "upload"); pid = f"{f.get('folder', '').strip('/')}/{uuid.uuid4().hex}".strip("/")
            ASSETS[(t, pid)] = {"created_at": "2026-10-30T12:00:00Z", "bytes": len(body)}
            self.record({"op": "upload", "type": t, "public_id": pid})
            r = resource(t, pid, self.base()); return self.send(200, r)
        if re.match(r"^/v1_1/[^/]+/raw/rename$", u.path):
            t, to_t, frm, to = f.get("type", "upload"), f.get("to_type", f.get("type", "upload")), f.get("from_public_id"), f.get("to_public_id")
            self.record({"op": "rename", "from": frm, "to": to, "type": t, "to_type": to_t, "invalidate": f.get("invalidate")})
            if (t, frm) not in ASSETS: return self.send(404, {"error": {"message": "not found"}})
            ASSETS[(to_t, to)] = ASSETS.pop((t, frm)); return self.send(200, resource(to_t, to, self.base()))
        if re.match(r"^/v1_1/[^/]+/raw/destroy$", u.path):
            t, pid = f.get("type", "upload"), f.get("public_id")
            self.record({"op": "destroy", "type": t, "public_id": pid, "invalidate": f.get("invalidate")})
            return self.send(200, {"result": "ok" if ASSETS.pop((t, pid), None) is not None else "not found"})
        self.record({"op": "unknown-post"}); return self.send(404, {"error": {"message": "unknown endpoint"}})

seed()
ThreadingHTTPServer(("0.0.0.0", PORT), H).serve_forever()
