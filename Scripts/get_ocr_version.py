#!/usr/bin/env python3
"""读取 OCR Server 的版本号，用于 CI 构建标签。"""

import xml.etree.ElementTree as ET
import sys
from pathlib import Path

def get_version():
    csproj = Path(__file__).parent.parent / "ocr" / "shmtu-ocr-onnx-server" / "shmtu-ocr-onnx-server.csproj"
    tree = ET.parse(csproj)
    root = tree.getroot()
    version = root.find(".//{http://schemas.microsoft.com/developer/msbuild/2003}Version")
    if version is None:
        # SDK style project without namespace
        version = root.find(".//Version")
    if version is None:
        print("ERROR: Version not found in csproj", file=sys.stderr)
        sys.exit(1)
    return ".".join(version.text.strip().split(".")[:3])

if __name__ == "__main__":
    print(get_version())
