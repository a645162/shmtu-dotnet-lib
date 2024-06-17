import xml.etree.ElementTree as ET


def parse_version(xml_content):
    root = ET.fromstring(xml_content)

    version_content = \
        root.find("PropertyGroup/Version").text.strip()

    return version_content


if __name__ == "__main__":
    path = "./shmtu-dotnet-lib/shmtu-dotnet-lib.csproj"
    with open(path, "r", encoding="utf-8") as f:
        xml_content = f.read()
    print(parse_version(xml_content))
