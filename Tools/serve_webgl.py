#!/usr/bin/env python3
"""Unity WebGL 빌드(Build/WebGL)를 여는 간단한 웹 서버.

사용: python Tools/serve_webgl.py [--port 5173] [--dir Build/WebGL] [--bind 0.0.0.0]
- .gz / .br 파일에 Content-Encoding 헤더를 붙여 압축 빌드도 그대로 열립니다.
- 0.0.0.0 에 바인드하므로 같은 네트워크의 다른 기기(또는 포트 포워딩한 외부)에서 http://<이 PC IP>:5173 으로 접속할 수 있습니다.
"""
import argparse
import os
import sys
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer


class WebGLHandler(SimpleHTTPRequestHandler):
    extensions_map = {
        **SimpleHTTPRequestHandler.extensions_map,
        ".wasm": "application/wasm",
        ".js": "application/javascript",
        ".data": "application/octet-stream",
        ".json": "application/json",
    }

    def guess_type(self, path):
        base = path
        for ext in (".gz", ".br"):
            if path.endswith(ext):
                base = path[: -len(ext)]
                break
        return super().guess_type(base)

    def end_headers(self):
        if self.path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        elif self.path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def log_message(self, fmt, *args):
        sys.stdout.write("%s - %s\n" % (self.address_string(), fmt % args))
        sys.stdout.flush()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5173)
    parser.add_argument("--dir", default="Build/WebGL")
    parser.add_argument("--bind", default="0.0.0.0")
    args = parser.parse_args()

    directory = os.path.abspath(args.dir)
    if not os.path.exists(os.path.join(directory, "index.html")):
        print("index.html 이 없습니다: " + directory + " (먼저 CatGame/Build WebGL 로 빌드하세요)")
        sys.exit(1)

    handler = partial(WebGLHandler, directory=directory)
    server = ThreadingHTTPServer((args.bind, args.port), handler)
    print("CatGame WebGL: http://%s:%d/  (폴더 %s)" % (args.bind, args.port, directory))
    sys.stdout.flush()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
