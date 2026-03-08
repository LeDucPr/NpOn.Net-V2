FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MicroServices/Account/Service/NpOn.AccountService/NpOn.AccountService.csproj", "MicroServices/Account/Service/NpOn.AccountService/"]
COPY ["Common/Infrastructures/NpOn.RedisExtCm/NpOn.RedisExtCm.csproj", "Common/Infrastructures/NpOn.RedisExtCm/"]
COPY ["Common/Extensions/NpOn.ICommonDb/NpOn.ICommonDb.csproj", "Common/Extensions/NpOn.ICommonDb/"]
COPY ["Common/Extensions/NpOn.CommonEnums/NpOn.CommonEnums.csproj", "Common/Extensions/NpOn.CommonEnums/"]
COPY ["Common/Extensions/NpOn.CommonMode/NpOn.CommonMode.csproj", "Common/Extensions/NpOn.CommonMode/"]
COPY ["Common/Extensions/NpOn.CommonDb/NpOn.CommonDb.csproj", "Common/Extensions/NpOn.CommonDb/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.RedisAppExtUse/NpOn.RedisAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.RedisAppExtUse/"]
COPY ["Common/Extensions/NpOn.CommonInternalCache/NpOn.CommonInternalCache.csproj", "Common/Extensions/NpOn.CommonInternalCache/"]
COPY ["Common/Extensions/NpOn.BaseDbFactory/NpOn.BaseDbFactory.csproj", "Common/Extensions/NpOn.BaseDbFactory/"]
COPY ["Common/Extensions/NpOn.CommonBaseDomain/NpOn.CommonBaseDomain.csproj", "Common/Extensions/NpOn.CommonBaseDomain/"]
COPY ["Common/Infrastructures/NpOn.PostgresExtCm/NpOn.PostgresExtCm.csproj", "Common/Infrastructures/NpOn.PostgresExtCm/"]
COPY ["Common/Extensions/NpOn.HandleFlow/NpOn.HandleFlow.csproj", "Common/Extensions/NpOn.HandleFlow/"]
COPY ["Common/Infrastructures/DbFactories/NpOn.RedisFactory/NpOn.RedisFactory.csproj", "Common/Infrastructures/DbFactories/NpOn.RedisFactory/"]
COPY ["Common/Infrastructures/DbFactories/NpOn.PostgresFactory/NpOn.PostgresFactory.csproj", "Common/Infrastructures/DbFactories/NpOn.PostgresFactory/"]
COPY ["MicroServices/General/Service/NpOn.IGeneralService/NpOn.IGeneralService.csproj", "MicroServices/General/Service/NpOn.IGeneralService/"]
COPY ["Common/Extensions/NpOn.CommonHeaderConfig/NpOn.CommonHeaderConfig.csproj", "Common/Extensions/NpOn.CommonHeaderConfig/"]
COPY ["Common/Extensions/NpOn.CommonGrpcCall/NpOn.CommonGrpcCall.csproj", "Common/Extensions/NpOn.CommonGrpcCall/"]
COPY ["Common/Extensions/NpOn.CommonGrpcContract/NpOn.CommonGrpcContract.csproj", "Common/Extensions/NpOn.CommonGrpcContract/"]
COPY ["MicroServices/General/Contract/NpOn.GeneralServiceContract/NpOn.GeneralServiceContract.csproj", "MicroServices/General/Contract/NpOn.GeneralServiceContract/"]
COPY ["Common/Applications/NpOn.CommonApplication/NpOn.CommonApplication.csproj", "Common/Applications/NpOn.CommonApplication/"]
COPY ["Common/Extensions/NpOn.CommonScope/NpOn.CommonScope.csproj", "Common/Extensions/NpOn.CommonScope/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/NpOn.AddGrpcAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.AddGrpcAppExtUse/"]
COPY ["MicroServices/Account/Definitions/NpOn.AccountConstant/NpOn.AccountConstant.csproj", "MicroServices/Account/Definitions/NpOn.AccountConstant/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.KafkaAppExtUse/NpOn.KafkaAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.KafkaAppExtUse/"]
COPY ["Common/Infrastructures/NpOn.KafkaExtCm/NpOn.KafkaExtCm.csproj", "Common/Infrastructures/NpOn.KafkaExtCm/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.PostgresAppExtUse/NpOn.PostgresAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.PostgresAppExtUse/"]
COPY ["Common/Applications/ApplicationExtensions/NpOn.RabbitMqAppExtUse/NpOn.RabbitMqAppExtUse.csproj", "Common/Applications/ApplicationExtensions/NpOn.RabbitMqAppExtUse/"]
COPY ["Common/Infrastructures/NpOn.RabbitMqExtMs/NpOn.RabbitMqExtMs.csproj", "Common/Infrastructures/NpOn.RabbitMqExtMs/"]
COPY ["Common/Applications/NpOn.CommonHttpApplication/NpOn.CommonHttpApplication.csproj", "Common/Applications/NpOn.CommonHttpApplication/"]
COPY ["MicroServices/Account/Contract/NpOn.AccountServiceContract/NpOn.AccountServiceContract.csproj", "MicroServices/Account/Contract/NpOn.AccountServiceContract/"]
COPY ["MicroServices/Account/Definitions/NpOn.AccountEnum/NpOn.AccountEnum.csproj", "MicroServices/Account/Definitions/NpOn.AccountEnum/"]
COPY ["MicroServices/Account/Definitions/NpOn.ShareAccountConstant/NpOn.ShareAccountConstant.csproj", "MicroServices/Account/Definitions/NpOn.ShareAccountConstant/"]
COPY ["MicroServices/Account/StorageAdapter/NpOn.AccountStorageAdapter/NpOn.AccountStorageAdapter.csproj", "MicroServices/Account/StorageAdapter/NpOn.AccountStorageAdapter/"]
COPY ["MicroServices/Account/StorageAdapter/NpOn.IAccountStorageAdapter/NpOn.IAccountStorageAdapter.csproj", "MicroServices/Account/StorageAdapter/NpOn.IAccountStorageAdapter/"]
COPY ["MicroServices/Account/Service/NpOn.IAccountService/NpOn.IAccountService.csproj", "MicroServices/Account/Service/NpOn.IAccountService/"]
RUN dotnet restore "MicroServices/Account/Service/NpOn.AccountService/NpOn.AccountService.csproj"
COPY . .
WORKDIR "/src/MicroServices/Account/Service/NpOn.AccountService"
RUN dotnet build "./NpOn.AccountService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NpOn.AccountService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NpOn.AccountService.dll"]
