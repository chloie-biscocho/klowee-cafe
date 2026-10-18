# Klowee Cafe API. Built by Render from the `api` directory
# (docs/decisions/013-hosting-and-config.md).

# ---- Build: restore, compile and publish a Release build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project file alone first, so the restore layer is cached until a
# package reference changes.
COPY Klowee.Api/Klowee.Api.csproj Klowee.Api/
RUN dotnet restore Klowee.Api/Klowee.Api.csproj --runtime linux-x64

COPY Klowee.Api/ Klowee.Api/
# --runtime linux-x64: only Magick.NET's Linux native library is copied
# (about 37 MB), not all eight platforms' (about 240 MB). Framework-dependent:
# the runtime comes from the image below.
RUN dotnet publish Klowee.Api/Klowee.Api.csproj \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained false \
        --no-restore \
        --output /app \
        -p:UseAppHost=false

# ---- Runtime: ASP.NET Core only, no SDK ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl, for the health check below; the base image has none.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    # Render tells the container which port to listen on through PORT. The
    # default is for running the image anywhere else.
    PORT=8080

# The non-root user the .NET images ship with (uid 1654).
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error "http://localhost:${PORT}/api/health" > /dev/null || exit 1

# A shell, so ${PORT} is read when the container starts, not when it is built.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT} exec dotnet Klowee.Api.dll"]
