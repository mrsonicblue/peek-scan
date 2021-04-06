FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["Peek.Tab.csproj", ""]
RUN dotnet restore "./Peek.Tab.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Peek.Tab.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Peek.Tab.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Peek.Tab.dll"]