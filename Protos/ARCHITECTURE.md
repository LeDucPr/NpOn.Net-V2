# Kiến Trúc Schema-First gRPC NuGet (`.Proto`) Toàn Hệ Thống NpOn.Net-V2

Toàn bộ các luồng giao tiếp gRPC giữa **API Gateway (`Controllers/NpOn.SSO`)**, **TokenValidator (`NpOn.TokenValidatorExtUse`)**, và **4 Microservices (`GeneralService`, `AccountService`, `TrackerService`, `MigrationService`)** đều sử dụng mô hình **Schema-First `.proto` đóng gói thành NuGet Package (`NpOn.*.Proto`)**.

Các project gọi sang service khác chỉ cần khai báo đúng 1 dòng trong `.csproj` như tham chiếu thư viện DI thông thường:
```xml
<PackageReference Include="NpOn.Account.Proto" />
```
- **Không** dùng đường dẫn tương đối (`..\..\..\..\Protos\...`) trong từng microservice/controller.
- **Không** chia sẻ chéo các DLL nội bộ (`NpOn.IAccountService`, `NpOn.IGeneralService`, `NpOn.ITrackerService`, `*ReadModel`, `*Command`), tránh kéo theo DB driver (`Npgsql`, `NpOn.CommonDb`, `HandleFlow`) sang Gateway hay các service khác.

---

## 1. Cấu trúc thư mục `Protos/` & Ma trận Package

```text
NpOn.Net-V2/
└── Protos/
    ├── ARCHITECTURE.md                       # Tài liệu kiến trúc (file này)
    ├── account/account.proto                 # Contract Authentication / AccountInfo / AccountGroup / Permission
    ├── general/fld_master.proto              # Contract FldMasterProtoService (GetExecCommand, GetExecution)
    ├── tracker/tracker.proto                 # Contract TrackerLogProtoService (PushLogs)
    ├── migration/migration.proto             # Contract CassandraMigrationProtoService (TransferTable)
    ├── sso/sso_account.proto                 # Contract SsoAccountProtoService (Public gRPC trên SSO)
    ├── NpOn.Account.Proto/NpOn.Account.Proto.csproj
    ├── NpOn.General.Proto/NpOn.General.Proto.csproj
    ├── NpOn.Tracker.Proto/NpOn.Tracker.Proto.csproj
    ├── NpOn.Migration.Proto/NpOn.Migration.Proto.csproj
    └── NpOn.Sso.Proto/NpOn.Sso.Proto.csproj
```

| NuGet Package (`artifacts/packages/`) | File `.proto` nguồn | Service Server (Implement & MapGrpc) | Consumers (Chỉ dùng `<PackageReference>`) |
|---|---|---|---|
| **`NpOn.General.Proto`** | `general/fld_master.proto` | `NpOn.GeneralService` (`FldMasterProtoGrpcService`) | `NpOn.AccountStorageAdapter` (`AccountService`), `NpOn.TrackerService` |
| **`NpOn.Account.Proto`** | `account/account.proto` | `NpOn.AccountService` (`AuthenticationProtoGrpcService`, `AccountInfoProtoGrpcService`, `AccountGroupProtoGrpcService`, `AccountPermissionProtoGrpcService`) | `Controllers/NpOn.SSO`, `NpOn.TokenValidatorExtUse`, `NpOn.TrackerService`, `NpOn.AccountService` |
| **`NpOn.Tracker.Proto`** | `tracker/tracker.proto` | `NpOn.TrackerService` (`TrackerLogProtoGrpcService`) | `NpOn.AccountService`, `NpOn.TrackerService` |
| **`NpOn.Migration.Proto`** | `migration/migration.proto` | `NpOn.MigrationService` (`CassandraMigrationProtoGrpcService`) | `NpOn.MigrationService` |
| **`NpOn.Sso.Proto`** | `sso/sso_account.proto` | `Controllers/NpOn.SSO` (`SsoAccountProtoGrpcService`) | Public gRPC Clients / Envoy |

> [!NOTE]
> Mỗi project `NpOn.*.Proto.csproj` chỉ phụ thuộc `Google.Protobuf`, `Grpc.Core.Api`, và `Grpc.Tools` (`PrivateAssets="All"`), **0 phụ thuộc** vào bất kỳ package `NpOn.*` hay DB driver nào. `build-common.ps1` tự hash cả file `.proto` và đóng gói lại khi có thay đổi.

---

## 2. Sơ đồ Cơ chế Build & Đóng gói `.Proto` NuGet

```mermaid
flowchart LR
    subgraph Sources["Single Source of Truth (Protos/)"]
        P_Gen["general/fld_master.proto"]
        P_Acc["account/account.proto"]
        P_Trk["tracker/tracker.proto"]
        P_Mig["migration/migration.proto"]
        P_Sso["sso/sso_account.proto"]
    end

    subgraph Pack["build-common.ps1 (Phase 1)"]
        GT["Grpc.Tools (GrpcServices=Both)\n-> sinh Client Stub + Server Base"]
        Feed["artifacts/packages/\nNpOn.*.Proto.2.0.0.nupkg"]
        GT --> Feed
    end

    Sources --> GT

    subgraph Consumers["Services & Controllers (Chỉ dùng PackageReference)"]
        SSO["Controllers/NpOn.SSO\n(NpOn.Account.Proto + NpOn.Sso.Proto)"]
        TV["NpOn.TokenValidatorExtUse\n(NpOn.Account.Proto)"]
        ACC["NpOn.AccountService + Adapter\n(NpOn.Account.Proto + NpOn.General.Proto + NpOn.Tracker.Proto)"]
        GEN["NpOn.GeneralService\n(NpOn.General.Proto + Riok.Mapperly)"]
        TRK["NpOn.TrackerService\n(NpOn.Tracker.Proto + NpOn.Account.Proto + NpOn.General.Proto)"]
        MIG["NpOn.MigrationService\n(NpOn.Migration.Proto)"]
    end

    Feed --> SSO
    Feed --> TV
    Feed --> ACC
    Feed --> GEN
    Feed --> TRK
    Feed --> MIG
```

---

## 3. Sơ đồ Tương tác Runtime Toàn Hệ Thống

```mermaid
flowchart TB
    Client["Client / Web / Mobile\n(HTTP/1.1 JSON :14023 | Public gRPC :15023)"]

    subgraph SSO["Controllers/NpOn.SSO (:14023 / :15023)"]
        SsoCtrl["AccountController / AccountInfoController / AccountGroupController"]
        SsoGrpc["SsoAccountProtoGrpcService (NpOn.Sso.Proto)"]
        TV["NpOn.TokenValidatorExtUse\n(TokenValidationMiddleware + AuthenService + PermissionService)"]
        SsoClient["AddProtoConnectService (NpOn.Account.Proto)\n+ SharedGrpcInterceptor"]
        SsoCtrl --> SsoClient
        SsoGrpc --> SsoClient
        TV --> SsoClient
    end

    subgraph Account["NpOn.AccountService (:40004)"]
        AccMw["GrpcServerAuthMiddleware (JWT / mTLS / ServiceMesh)"]
        AccProto["AccountProtoGrpcServices\n+ AccountProtoMapper (Mapperly)"]
        AccBiz["AuthenticationService / AccountInfoService / Group / Permission"]
        AccAdp["NpOn.AccountStorageAdapter\n(GeneralProtoCommandClient)"]
        AccMw --> AccProto --> AccBiz --> AccAdp
    end

    subgraph General["NpOn.GeneralService (:40000)"]
        GenMw["GrpcServerAuthMiddleware"]
        GenProto["FldMasterProtoGrpcService\n+ FldMasterProtoMapper (Mapperly)"]
        GenBiz["IFldMasterService + WrapperCacheStore"]
        GenMw --> GenProto --> GenBiz
    end

    subgraph Tracker["NpOn.TrackerService (:40003)"]
        TrkProto["TrackerLogProtoGrpcService\n(NpOn.Tracker.Proto)"]
        TrkBiz["TrackerLogService"]
        TrkProto --> TrkBiz
    end

    subgraph Migration["NpOn.MigrationService"]
        MigProto["CassandraMigrationProtoGrpcService\n(NpOn.Migration.Proto)"]
    end

    PG_Acc[("PostgreSQL (account)")]
    PG_Gen[("PostgreSQL (general)")]
    CH[("ClickHouse (logs)")]
    Redis[("Dragonfly / Redis")]

    Client --> SSO
    SsoClient -- "gRPC HTTP/2 (npon.account.v1.*)" --> AccMw
    AccAdp -- "gRPC HTTP/2 (npon.general.v1.FldMasterProtoService)" --> GenMw
    AccBiz -- "gRPC HTTP/2 (npon.tracker.v1.TrackerLogProtoService)" --> TrkProto
    AccAdp --> PG_Acc
    AccBiz --> Redis
    GenBiz --> PG_Gen
    TrkBiz --> CH
```

---

## 4. Luồng Tuần Tự Mẫu qua `.Proto` NuGet (`Login` + `GetAccountInfo` có xác thực Token)

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant SSO as NpOn.SSO (:14023)
    participant TV as TokenValidatorExtUse (NpOn.Account.Proto)
    participant ACC as NpOn.AccountService (:40004)
    participant GEN as NpOn.GeneralService (:40000)

    C->>SSO: POST /api/Account/Login
    SSO->>ACC: AuthenticationProtoService.Login (NpOn.Account.Proto)
    ACC->>GEN: FldMasterProtoService.GetExecCommand (NpOn.General.Proto)
    GEN-->>ACC: CommandSpec (SQL + ParamSpec)
    ACC-->>SSO: AccountLoginReplyProto (JWT Token + SessionId)
    SSO-->>C: 200 OK (JWT Token)

    C->>SSO: POST /api/AccountInfo/GetAccountInfo (Authorization: Bearer ...)
    SSO->>TV: TokenValidationMiddleware -> ContextService.ValidateToken (Stateless JWT)
    Note over TV: Xác thực chữ ký + expiry JWT cục bộ (0 Redis, 0 gRPC sang AccountService)
    SSO->>ACC: AccountInfoProtoService.AccountInfoGetByAccountId (NpOn.Account.Proto)
    ACC->>GEN: FldMasterProtoService.GetExecCommand (NpOn.General.Proto)
    GEN-->>ACC: CommandSpec
    ACC-->>SSO: AccountInfoSingleReplyProto
    SSO-->>C: 200 OK (AccountInfoDetailOutputModel)
```
