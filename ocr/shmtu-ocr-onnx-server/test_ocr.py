#!/usr/bin/env python3
"""
SHMTU OCR ONNX Server 测试脚本
测试 TCP 协议(兼容现有客户端) 和 REST API

用法:
  python3 test_ocr.py <图片路径> [图片路径...]
  python3 test_ocr.py *.png --concurrent 10
  python3 test_ocr.py test.png --no-tcp
"""

import socket
import base64
import json
import sys
import time
import argparse
import os
from concurrent.futures import ThreadPoolExecutor, as_completed
from urllib import request

DEFAULT_HOST = "127.0.0.1"
DEFAULT_TCP_PORT = 21601
DEFAULT_HTTP_PORT = 5000
END_MARKER = b"<END>"
TIMEOUT = 10


def tcp_ocr(host: str, port: int, image_path: str) -> str:
    """通过 TCP 协议(兼容现有 C++/Rust/C#/Kotlin 客户端)进行 OCR"""
    with open(image_path, "rb") as f:
        image_data = f.read()

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(TIMEOUT)
    try:
        sock.connect((host, port))
        sock.sendall(image_data + END_MARKER)
        response = b""
        while True:
            chunk = sock.recv(4096)
            if not chunk:
                break
            response += chunk
        return response.decode("utf-8").strip()
    finally:
        sock.close()


def rest_ocr_base64(host: str, port: int, image_path: str) -> dict:
    """REST API — JSON base64"""
    with open(image_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    data = json.dumps({"imageBase64": b64}).encode("utf-8")
    req = request.Request(
        f"http://{host}:{port}/api/ocr",
        data=data,
        headers={"Content-Type": "application/json"},
    )
    with request.urlopen(req, timeout=TIMEOUT) as resp:
        return json.loads(resp.read())


def rest_ocr_upload(host: str, port: int, image_path: str) -> dict:
    """REST API — multipart upload"""
    boundary = "----FormBoundary" + os.urandom(16).hex()
    filename = os.path.basename(image_path)
    with open(image_path, "rb") as f:
        image_data = f.read()

    body = (
        f"--{boundary}\r\n"
        f'Content-Disposition: form-data; name="file"; filename="{filename}"\r\n'
        f"Content-Type: image/png\r\n\r\n"
    ).encode("utf-8")
    body += image_data
    body += f"\r\n--{boundary}--\r\n".encode("utf-8")

    req = request.Request(
        f"http://{host}:{port}/api/ocr/upload",
        data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    with request.urlopen(req, timeout=TIMEOUT) as resp:
        return json.loads(resp.read())


def health_check(host: str, port: int) -> dict:
    url = f"http://{host}:{port}/api/health"
    with request.urlopen(url, timeout=TIMEOUT) as resp:
        return json.loads(resp.read())


def green(s: str) -> str:
    return f"\033[32m{s}\033[0m"


def red(s: str) -> str:
    return f"\033[31m{s}\033[0m"


def main():
    parser = argparse.ArgumentParser(description="SHMTU OCR Server 测试")
    parser.add_argument("image", nargs="+", help="测试图片路径")
    parser.add_argument("--host", default=DEFAULT_HOST, help=f"地址 (默认 {DEFAULT_HOST})")
    parser.add_argument("--tcp-port", type=int, default=DEFAULT_TCP_PORT)
    parser.add_argument("--http-port", type=int, default=DEFAULT_HTTP_PORT)
    parser.add_argument("--concurrent", type=int, default=1, help="TCP 并发数")
    parser.add_argument("--no-tcp", action="store_true", help="跳过 TCP")
    parser.add_argument("--no-rest", action="store_true", help="跳过 REST")
    parser.add_argument("--no-upload", action="store_true", help="跳过 Upload")
    args = parser.parse_args()

    print("=" * 55)
    try:
        h = health_check(args.host, args.http_port)
        print(f"Health: {green(json.dumps(h, ensure_ascii=False))}")
    except Exception as e:
        print(f"Health: {red(f'FAILED — {e}')}")
        return 1

    failed = 0
    total = 0

    for img in args.image:
        print("-" * 55)
        print(f"Image: {img}")

        # TCP (兼容现有客户端协议)
        if not args.no_tcp:
            total += 1
            try:
                t0 = time.perf_counter()
                expr = tcp_ocr(args.host, args.tcp_port, img)
                ms = (time.perf_counter() - t0) * 1000
                print(f"  TCP:   {green(expr):30s} ({ms:6.1f}ms)")
            except Exception as e:
                failed += 1
                print(f"  TCP:   {red(f'FAILED — {e}')}")

        # REST base64
        if not args.no_rest:
            total += 1
            try:
                t0 = time.perf_counter()
                r = rest_ocr_base64(args.host, args.http_port, img)
                ms = (time.perf_counter() - t0) * 1000
                if r["success"]:
                    print(f"  REST:  {green(r['expression']):30s} ({ms:6.1f}ms)")
                else:
                    failed += 1
                    print(f"  REST:  {red('FAILED — ' + r['error'])}")
            except Exception as e:
                failed += 1
                print(f"  REST:  {red(f'FAILED — {e}')}")

        # REST upload
        if not args.no_upload:
            total += 1
            try:
                t0 = time.perf_counter()
                r = rest_ocr_upload(args.host, args.http_port, img)
                ms = (time.perf_counter() - t0) * 1000
                if r["success"]:
                    print(f"  UPLD:  {green(r['expression']):30s} ({ms:6.1f}ms)")
                else:
                    failed += 1
                    print(f"  UPLD:  {red('FAILED — ' + r['error'])}")
            except Exception as e:
                failed += 1
                print(f"  UPLD:  {red(f'FAILED — {e}')}")

    # 并发 TCP 测试
    if args.concurrent > 1 and not args.no_tcp:
        print("-" * 55)
        print(f"TCP 并发测试: {args.concurrent} 并发 @ {args.image[0]}")
        t0 = time.perf_counter()
        c_fail = 0
        with ThreadPoolExecutor(max_workers=args.concurrent) as ex:
            futs = [ex.submit(tcp_ocr, args.host, args.tcp_port, args.image[0]) for _ in range(args.concurrent)]
            for f in as_completed(futs):
                try:
                    f.result()
                except Exception:
                    c_fail += 1
        ms = (time.perf_counter() - t0) * 1000
        if c_fail == 0:
            print(f"  {green(f'All {args.concurrent} passed')} ({ms:.0f}ms total)")
        else:
            print(f"  {red(f'{c_fail}/{args.concurrent} FAILED')}")
            failed += c_fail

    print("=" * 55)
    if failed == 0:
        print(green(f"All {total} tests passed!"))
        return 0
    else:
        print(red(f"{failed} failure(s) out of {total} tests"))
        return 1


if __name__ == "__main__":
    sys.exit(main())
