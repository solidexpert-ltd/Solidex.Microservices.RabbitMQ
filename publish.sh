#!/bin/sh

echo "Configure credentials"
dotnet nuget add source $FEED_URL -n space -u "%JB_SPACE_CLIENT_ID%" -p "%JB_SPACE_CLIENT_SECRET%" --store-password-in-clear-text
VERSION=1.0.$JB_SPACE_EXECUTION_NUMBER

echo "Publish nuget package"
dotnet pack -p:PackageVersion=$VERSION -o ./
dotnet nuget push Solidex.Microservices.RabbitMQ.$VERSION.nupkg -s space