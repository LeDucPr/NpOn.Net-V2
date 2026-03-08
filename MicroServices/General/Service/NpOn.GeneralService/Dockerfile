FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MicroServices/General/Service/NpOn.GeneralService/NpOn.GeneralService.csproj", "MicroServices/General/Service/NpOn.GeneralService/"]
COPY ["Common/Infrastructures/DbFactories/NpOn.PostgresFactory/NpOn.PostgresFactory.csproj", "Common/Infrastructures/DbFactories/NpOn.PostgresFactory/"]
COPY ["Common/Extensions/NpOn.ICommonDb/NpOn.ICommonDb.csproj", "Common/Extensions/NpOn.ICommonDb/"]
COPY ["Common/Extensions/NpOn.CommonEnums/NpOn.CommonEnums.csproj", "Common/Extensions/NpOn.CommonEnums/"]
COPY ["Common/Extensions/NpOn.CommonMode/NpOn.CommonMode.csproj", "Common/Extensions/NpOn.CommonMode/"]
COPY ["Common/Extensions/NpOn.HandleFlow/NpOn.HandleFlow.csproj", "Common/Extensions/NpOn.HandleFlow/"]
COPY ["Common/Extensions/NpOn.CommonInternalCache/NpOn.CommonInternalCache.csproj", "Common/Extensions/NpOn.CommonInternalCache/"]
COPY ["Common/Extensions/NpOn.CommonDb/NpOn.CommonDb.csproj", "Common/Extensions/NpOn.CommonDb/"]
COPY ["Common/Extensions/NpOn.CommonBaseDomain/NpOn.CommonBaseDomain.csproj", "Common/Extensions/NpOn.CommonBaseDomain/"]
COPY ["Common/Infrastructures/NpOn.PostgresExtCm/NpOn.PostgresExtCm.csproj", "Common/Infrastructures/NpOn.PostgresExtCm/"]
COPY ["Common/Extensions/NpOn.BaseDbFactory/NpOn.BaseDbFactory.csproj", "Common/Extensions/NpOn.BaseDbFactory/"]
COPY ["Common/Extensions/NpOn.CommonHeaderConfig/NpOn.CommonHeaderConfig.csproj", "Common/Extensions/NpOn.CommonHeaderConfig/"]
COPY ["Common/Extensions/NpOn.CommonGrpcCall/NpOn.CommonGrpcCall.csproj", "Common/Extensions/NpOn.CommonGrpcCall/"]
COPY ["Common/Extensions/NpOn.CommonScope/NpOn.CommonScope.csproj", "Common/Extensions/NpOn.CommonScope/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/NpOn.AddGrpcAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/"]
COPY ["Common/Applications/NpOn.CommonApplication/NpOn.CommonApplication.csproj", "Common/Applications/NpOn.CommonApplication/"]
COPY ["Common/Extensions/NpOn.CommonGrpcContract/NpOn.CommonGrpcContract.csproj", "Common/Extensions/NpOn.CommonGrpcContract/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.PostgresAppExtUse/NpOn.PostgresAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.PostgresAppExtUse/"]
COPY ["Common/Applications/NpOn.CommonHttpApplication/NpOn.CommonHttpApplication.csproj", "Common/Applications/NpOn.CommonHttpApplication/"]
COPY ["MicroServices/General/Contract/NpOn.GeneralServiceContract/NpOn.GeneralServiceContract.csproj", "MicroServices/General/Contract/NpOn.GeneralServiceContract/"]
COPY ["MicroServices/General/Definitions/NpOn.GeneralConstant/NpOn.GeneralConstant.csproj", "MicroServices/General/Definitions/NpOn.GeneralConstant/"]
COPY ["MicroServices/General/Service/NpOn.IGeneralService/NpOn.IGeneralService.csproj", "MicroServices/General/Service/NpOn.IGeneralService/"]
RUN dotnet restore "MicroServices/General/Service/NpOn.GeneralService/NpOn.GeneralService.csproj"
COPY . .
WORKDIR "/src/MicroServices/General/Service/NpOn.GeneralService"
RUN dotnet build "./NpOn.GeneralService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NpOn.GeneralService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NpOn.GeneralService.dll"]
