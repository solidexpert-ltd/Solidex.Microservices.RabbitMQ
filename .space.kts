job(".NET Core sdk. Build, test, publish"){
    container(image = "mcr.microsoft.com/dotnet/sdk:6.0"){
        env["FEED_URL"] = "https://nuget.pkg.jetbrains.space/solidexpert/p/scrm/nuget/v3/index.json"
        shellScript {
            content = """
                echo Run build...
                dotnet nuget add source "https://nuget.pkg.jetbrains.space/solidexpert/p/scrm/nuget/v3/index.json" -n space -u "%JB_SPACE_CLIENT_ID%" -p "%JB_SPACE_CLIENT_SECRET%" --store-password-in-clear-text
                dotnet build
                echo Publish NuGet package...
                chmod +x publish.sh
                ./publish.sh
            """
        }
    }
}