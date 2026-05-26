import os

from get_version import parse_version

if __name__ == "__main__":
    path = "./Core/shmtu-dotnet-lib/shmtu-dotnet-lib.csproj"

    with open(path, "r", encoding="utf-8") as f:
        xml_content = f.read()

    version = parse_version(xml_content)
    nupkg = f"Output/shmtu-dotnet-lib.{version}.nupkg"

    # 推送到 NuGet.org
    if os.environ.get("NUGET_API_KEY"):
        cmd_nuget = (
            f"dotnet nuget push "
            f'"{nupkg}" '
            f'--api-key "{os.environ["NUGET_API_KEY"]}" '
            f"--source https://api.nuget.org/v3/index.json"
        )
        print(f"Pushing to NuGet.org: {cmd_nuget}")
        os.system(cmd_nuget)

    # 推送到 GitHub Packages
    if os.environ.get("GITHUB_TOKEN"):
        cmd_github = (
            f"dotnet nuget push "
            f'"{nupkg}" '
            f'--api-key "{os.environ["GITHUB_TOKEN"]}" '
            f"--source https://nuget.pkg.github.com/a645162/index.json"
        )
        print(f"Pushing to GitHub Packages: {cmd_github}")
        os.system(cmd_github)
