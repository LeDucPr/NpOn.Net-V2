FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Controllers/NpOn.SSO/NpOn.SSO.csproj", "Controllers/NpOn.SSO/"]
COPY ["MicroServices/Account/Definitions/NpOn.ShareAccountConstant/NpOn.ShareAccountConstant.csproj", "MicroServices/Account/Definitions/NpOn.ShareAccountConstant/"]
COPY ["Common/Infrastructures/NpOn.PostgresExtCm/NpOn.PostgresExtCm.csproj", "Common/Infrastructures/NpOn.PostgresExtCm/"]
COPY ["Common/Extensions/NpOn.ICommonDb/NpOn.ICommonDb.csproj", "Common/Extensions/NpOn.ICommonDb/"]
COPY ["Common/Extensions/NpOn.CommonEnums/NpOn.CommonEnums.csproj", "Common/Extensions/NpOn.CommonEnums/"]
COPY ["Common/Extensions/NpOn.CommonMode/NpOn.CommonMode.csproj", "Common/Extensions/NpOn.CommonMode/"]
COPY ["Common/Extensions/NpOn.CommonInternalCache/NpOn.CommonInternalCache.csproj", "Common/Extensions/NpOn.CommonInternalCache/"]
COPY ["Common/Extensions/NpOn.CommonDb/NpOn.CommonDb.csproj", "Common/Extensions/NpOn.CommonDb/"]
COPY ["Common/Extensions/NpOn.HandleFlow/NpOn.HandleFlow.csproj", "Common/Extensions/NpOn.HandleFlow/"]
COPY ["Common/Extensions/NpOn.CommonHeaderConfig/NpOn.CommonHeaderConfig.csproj", "Common/Extensions/NpOn.CommonHeaderConfig/"]
COPY ["Common/Extensions/NpOn.CommonGrpcCall/NpOn.CommonGrpcCall.csproj", "Common/Extensions/NpOn.CommonGrpcCall/"]
COPY ["Common/Extensions/NpOn.CommonBaseDomain/NpOn.CommonBaseDomain.csproj", "Common/Extensions/NpOn.CommonBaseDomain/"]
COPY ["Common/Extensions/NpOn.CommonScope/NpOn.CommonScope.csproj", "Common/Extensions/NpOn.CommonScope/"]
COPY ["Common/Extensions/NpOn.BaseDbFactory/NpOn.BaseDbFactory.csproj", "Common/Extensions/NpOn.BaseDbFactory/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/NpOn.AddGrpcAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/"]
COPY ["MicroServices/Account/Definitions/NpOn.AccountEnum/NpOn.AccountEnum.csproj", "MicroServices/Account/Definitions/NpOn.AccountEnum/"]
COPY ["Common/Applications/NpOn.CommonApplication/NpOn.CommonApplication.csproj", "Common/Applications/NpOn.CommonApplication/"]
COPY ["Common/Extensions/NpOn.CommonGrpcContract/NpOn.CommonGrpcContract.csproj", "Common/Extensions/NpOn.CommonGrpcContract/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.TokenValidatorExtUse/NpOn.TokenValidatorExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.TokenValidatorExtUse/"]
COPY ["MicroServices/Account/Contract/NpOn.AccountServiceContract/NpOn.AccountServiceContract.csproj", "MicroServices/Account/Contract/NpOn.AccountServiceContract/"]
COPY ["MicroServices/Account/Service/NpOn.IAccountService/NpOn.IAccountService.csproj", "MicroServices/Account/Service/NpOn.IAccountService/"]
COPY ["Common/Applications/NpOn.CommonHttpApplication/NpOn.CommonHttpApplication.csproj", "Common/Applications/NpOn.CommonHttpApplication/"]
RUN dotnet restore "Controllers/NpOn.SSO/NpOn.SSO.csproj"
COPY . .
WORKDIR "/src/Controllers/NpOn.SSO"
RUN dotnet build "./NpOn.SSO.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NpOn.SSO.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NpOn.SSO.dll"]
