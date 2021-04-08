#!/bin/bash

dotnet restore "./Peek.Scan.csproj"
dotnet build "Peek.Scan.csproj" -c Release
dotnet publish "Peek.Scan.csproj" -c Release --runtime linux-arm --self-contained true -o release/scan-lib
cp appsettings-release.json release/scan-lib/appsettings.json