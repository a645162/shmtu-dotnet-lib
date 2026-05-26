#!/usr/bin/env bash
# SHMTU OCR ONNX Server — Shell 测试脚本
# 测试 TCP 协议(兼容现有客户端) 和 REST API
set -euo pipefail

HOST="${HOST:-127.0.0.1}"
TCP_PORT="${TCP_PORT:-21601}"
HTTP_PORT="${HTTP_PORT:-21600}"

RED='\033[31m'
GREEN='\033[32m'
NC='\033[0m'

usage() {
    echo "用法: $0 <图片路径> [图片路径...]"
    echo "环境变量: HOST TCP_PORT HTTP_PORT"
    echo ""
    echo "示例:"
    echo "  $0 test.png"
    echo "  $0 samples/*.png"
    echo "  HOST=192.168.1.1 $0 test.png"
    exit 1
}

[ $# -ge 1 ] || usage
command -v curl >/dev/null 2>&1 || { echo -e "${RED}Missing: curl${NC}"; exit 1; }

# ---- Health ----
echo "======================================================================"
echo -n "Health: "
HEALTH=$(curl -sS "http://$HOST:$HTTP_PORT/api/health" 2>&1) && echo -e "${GREEN}$HEALTH${NC}" || {
    echo -e "${RED}FAILED — $HEALTH${NC}"
    exit 1
}

FAILED=0
TOTAL=0

for IMG in "$@"; do
    [ -f "$IMG" ] || { echo -e "${RED}Not found: $IMG${NC}"; continue; }
    echo "----------------------------------------------------------------------"
    echo "Image: $IMG"

    # ---- TCP (兼容现有客户端: 原始图片字节 + <END>) ----
    TOTAL=$((TOTAL + 1))
    RESULT=$( (cat "$IMG"; printf '<END>') | nc -w 5 "$HOST" "$TCP_PORT" 2>/dev/null) || RESULT=""
    if [ -n "$RESULT" ]; then
        echo -e "  TCP:   ${GREEN}$RESULT${NC}"
    else
        FAILED=$((FAILED + 1))
        echo -e "  TCP:   ${RED}FAILED${NC}"
    fi

    # ---- REST base64 ----
    TOTAL=$((TOTAL + 1))
    B64=$(base64 -w0 "$IMG" 2>/dev/null || base64 "$IMG" | tr -d '\n')
    RESP=$(curl -sS -X POST "http://$HOST:$HTTP_PORT/api/ocr" \
        -H "Content-Type: application/json" \
        -d "{\"imageBase64\":\"$B64\"}" 2>/dev/null) || RESP=""
    if echo "$RESP" | grep -q '"success":true'; then
        EXPR=$(echo "$RESP" | python3 -c "import sys,json;print(json.load(sys.stdin)['expression'])" 2>/dev/null || echo "?")
        echo -e "  REST:  ${GREEN}$EXPR${NC}"
    else
        FAILED=$((FAILED + 1))
        echo -e "  REST:  ${RED}FAILED — $RESP${NC}"
    fi

    # ---- REST upload ----
    TOTAL=$((TOTAL + 1))
    RESP=$(curl -sS -X POST "http://$HOST:$HTTP_PORT/api/ocr/upload" \
        -F "file=@$IMG" 2>/dev/null) || RESP=""
    if echo "$RESP" | grep -q '"success":true'; then
        EXPR=$(echo "$RESP" | python3 -c "import sys,json;print(json.load(sys.stdin)['expression'])" 2>/dev/null || echo "?")
        echo -e "  UPLD:  ${GREEN}$EXPR${NC}"
    else
        FAILED=$((FAILED + 1))
        echo -e "  UPLD:  ${RED}FAILED — $RESP${NC}"
    fi
done

echo "======================================================================"
if [ "$FAILED" -eq 0 ]; then
    echo -e "${GREEN}All $TOTAL tests passed!${NC}"
else
    echo -e "${RED}$FAILED failure(s) out of $TOTAL tests${NC}"
    exit 1
fi
