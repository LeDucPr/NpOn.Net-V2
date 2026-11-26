FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy NuGet configuration, central package versions, pre-packed Common & Contract feed, and ONLY AccountService block
COPY ["NuGet.Config", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["artifacts/packages/", "artifacts/packages/"]
COPY ["MicroServices/Account/", "MicroServices/Account/"]

RUN dotnet restore "MicroServices/Account/Service/NpOn.AccountService/NpOn.AccountService.csproj"
WORKDIR "/src/MicroServices/Account/Service/NpOn.AccountService"
RUN dotnet build "./NpOn.AccountService.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NpOn.AccountService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NpOn.AccountService.dll"]
