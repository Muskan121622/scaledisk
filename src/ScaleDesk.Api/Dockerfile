# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first for better layer caching (copy all csproj + sln).
COPY ScaleDesk.sln ./
COPY src/ScaleDesk.Domain/ScaleDesk.Domain.csproj src/ScaleDesk.Domain/
COPY src/ScaleDesk.Application/ScaleDesk.Application.csproj src/ScaleDesk.Application/
COPY src/ScaleDesk.Infrastructure/ScaleDesk.Infrastructure.csproj src/ScaleDesk.Infrastructure/
COPY src/ScaleDesk.Api/ScaleDesk.Api.csproj src/ScaleDesk.Api/
RUN dotnet restore src/ScaleDesk.Api/ScaleDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/ScaleDesk.Api/ScaleDesk.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

# curl is only needed so compose/the orchestrator can probe /health/live & /health/ready.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root.
RUN adduser --disabled-password --gecos "" appuser || true
USER appuser

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ScaleDesk.Api.dll"]
