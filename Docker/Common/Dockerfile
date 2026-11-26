# Shared Common & Contract NuGet Feed Builder Image
# Usage:
#   docker build -f Docker/Common/Dockerfile -t npon-common-packages:latest .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["NuGet.Config", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Common/", "Common/"]

# Shared cross-service Contracts, Definitions & Interfaces
COPY ["MicroServices/Account/Definitions/", "MicroServices/Account/Definitions/"]
COPY ["MicroServices/Account/Contract/", "MicroServices/Account/Contract/"]
COPY ["MicroServices/Account/Service/NpOn.IAccountService/", "MicroServices/Account/Service/NpOn.IAccountService/"]
COPY ["MicroServices/General/Contract/", "MicroServices/General/Contract/"]
COPY ["MicroServices/General/Service/NpOn.IGeneralService/", "MicroServices/General/Service/NpOn.IGeneralService/"]
COPY ["MicroServices/Migration/NpOn.IMigrationService/", "MicroServices/Migration/NpOn.IMigrationService/"]
COPY ["MicroServices/Tracker/Definitions/", "MicroServices/Tracker/Definitions/"]
COPY ["MicroServices/Tracker/Contract/", "MicroServices/Tracker/Contract/"]
COPY ["MicroServices/Tracker/Service/NpOn.ITrackerService/", "MicroServices/Tracker/Service/NpOn.ITrackerService/"]
COPY ["MicroServices/Tracker/Service/NpOn.Tracker2WayService/", "MicroServices/Tracker/Service/NpOn.Tracker2WayService/"]
COPY ["build-common.ps1", "./"]

RUN pwsh -NoProfile -ExecutionPolicy Bypass -File ./build-common.ps1 -Configuration $BUILD_CONFIGURATION -Force && \
    mkdir -p /packages && \
    cp -a artifacts/packages/*.nupkg /packages/

FROM scratch AS export
COPY --from=builder /packages /packages
