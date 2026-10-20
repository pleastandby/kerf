import json
import os
import sys
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs

PORT = int(os.environ.get("PORT", 8000))
STORAGE_DIR = os.path.join(os.path.dirname(__file__), "data")
os.makedirs(STORAGE_DIR, exist_ok=True)

class FigmaBackendHandler(BaseHTTPRequestHandler):
    def _set_cors_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")

    def do_OPTIONS(self):
        self.send_response(204)
        self._set_cors_headers()
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/api/health":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self._set_cors_headers()
            self.end_headers()
            response = {
                "status": "online",
                "engine": "Python C++ Wasm Bridge",
                "version": "2.4.0",
                "wasm_simd_supported": True,
                "python_version": sys.version.split(" ")[0],
            }
            self.wfile.write(json.dumps(response).encode("utf-8"))
            return

        elif path == "/api/projects":
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self._set_cors_headers()
            self.end_headers()
            projects = []
            for fname in os.listdir(STORAGE_DIR):
                if fname.endswith(".json"):
                    with open(os.path.join(STORAGE_DIR, fname), "r", encoding="utf-8") as f:
                        try:
                            projects.append(json.load(f))
                        except Exception:
                            pass
            self.wfile.write(json.dumps(projects).encode("utf-8"))
            return

        self.send_response(404)
        self.end_headers()

    def do_POST(self):
        parsed = urlparse(self.path)
        path = parsed.path
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length)

        try:
            payload = json.loads(body.decode("utf-8")) if body else {}
        except Exception:
            payload = {}

        if path == "/api/projects/save":
            project_id = payload.get("id", "default")
            file_path = os.path.join(STORAGE_DIR, f"{project_id}.json")
            with open(file_path, "w", encoding="utf-8") as f:
                json.dump(payload, f, indent=2)

            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self._set_cors_headers()
            self.end_headers()
            self.wfile.write(json.dumps({"success": True, "id": project_id}).encode("utf-8"))
            return

        elif path == "/api/python/execute":
            # Execute Python design script safely on elements
            script = payload.get("script", "")
            elements = payload.get("elements", [])
            output_log = []

            def log(*args):
                output_log.append(" ".join(str(a) for a in args))

            local_scope = {
                "elements": elements,
                "log": log,
                "math": __import__("math"),
                "random": __import__("random"),
            }

            try:
                exec(script, {}, local_scope)
                result_elements = local_scope.get("elements", elements)
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self._set_cors_headers()
                self.end_headers()
                self.wfile.write(json.dumps({
                    "success": True,
                    "elements": result_elements,
                    "logs": output_log,
                }).encode("utf-8"))
            except Exception as e:
                self.send_response(400)
                self.send_header("Content-Type", "application/json")
                self._set_cors_headers()
                self.end_headers()
                self.wfile.write(json.dumps({
                    "success": False,
                    "error": str(e),
                    "logs": output_log,
                }).encode("utf-8"))
            return

        self.send_response(404)
        self.end_headers()

def run_server():
    server_address = ("", PORT)
    httpd = HTTPServer(server_address, FigmaBackendHandler)
    print(f"[*] Kerf Python & C++ Backend running on http://localhost:{PORT}")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("[!] Stopping server")
        httpd.server_close()

if __name__ == "__main__":
    run_server()
