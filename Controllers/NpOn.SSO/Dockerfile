FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy NuGet configuration, central package versions, pre-packed Common & Contract feed, and ONLY SSO Controller source
COPY ["NuGet.Config", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["artifacts/packages/", "artifacts/packages/"]
COPY ["Controllers/NpOn.SSO/", "Controllers/NpOn.SSO/"]

RUN dotnet restore "Controllers/NpOn.SSO/NpOn.SSO.csproj"
WORKDIR "/src/Controllers/NpOn.SSO"
RUN dotnet build "./NpOn.SSO.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NpOn.SSO.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NpOn.SSO.dll"]
