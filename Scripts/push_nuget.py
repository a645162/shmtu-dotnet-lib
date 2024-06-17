import os

from get_version import parse_version

if __name__ == "__main__":
    path = "./shmtu-dotnet-lib/shmtu-dotnet-lib.csproj"

    with open(path, "r", encoding="utf-8") as f:
        xml_content = f.read()

    version = parse_version(xml_content)
    nuget_api_key = os.environ["NUGET_API_KEY"]

    cmd = (
        f"nuget push "
        f"\"Output/shmtu-dotnet-lib.{version}.nupkg\" "
        f"\"{nuget_api_key}\" "
        f"-Source https://api.nuget.org/v3/index.json"
    )

    os.system(cmd)
