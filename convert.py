import os

source_dir = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Infrastructures/NpOn.PostgresExtCm"
dest_dir = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Infrastructures/NpOn.MySqlExtCm"

mappings = {
    "Postgres": "MySql",
    "postgres": "mysql",
    "Npgsql": "MySqlConnector",
    "NpgsqlDbType": "MySqlDbType",
    "NpgsqlConnection": "MySqlConnection",
    "NpgsqlCommand": "MySqlCommand",
    "NpgsqlParameter": "MySqlParameter",
    "NpgsqlDataReader": "MySqlDataReader",
    "NpgsqlDbColumn": "MySqlDbColumn"
}

def convert_file(src, dst):
    with open(src, 'r') as f:
        content = f.read()
    
    for k, v in mappings.items():
        content = content.replace(k, v)
        
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    with open(dst, 'w') as f:
        f.write(content)

# NpOn.MySqlExtCm
src_results = os.path.join(source_dir, "Results")
dst_results = os.path.join(dest_dir, "Results")
for file in ["PostgresCell.cs", "PostgresMappingExtensions.cs", "PostgresUtils.cs", "PostgresWrapperResult.cs"]:
    convert_file(os.path.join(src_results, file), os.path.join(dst_results, file.replace("Postgres", "MySql")))

src_connections = os.path.join(source_dir, "Connections")
dst_connections = os.path.join(dest_dir, "Connections")
for file in ["PostgresDriver.cs"]:
    convert_file(os.path.join(src_connections, file), os.path.join(dst_connections, file.replace("Postgres", "MySql")))

src_sql = os.path.join(source_dir, "Sql")
dst_sql = os.path.join(dest_dir, "Sql")
for file in ["PostgresCommand.cs"]:
    convert_file(os.path.join(src_sql, file), os.path.join(dst_sql, file.replace("Postgres", "MySql")))

# NpOn.MySqlFactory
source_factory = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Infrastructures/DbFactories/NpOn.PostgresFactory"
dest_factory = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Infrastructures/DbFactories/NpOn.MySqlFactory"

for file in ["TablePostgresExtensions.cs", "IPostgresFactoryWrapper.cs", "BaseDomainPostgresExtensions.cs", "PostgresFactoryWrapper.cs"]:
    convert_file(os.path.join(source_factory, file), os.path.join(dest_factory, file.replace("Postgres", "MySql")))

convert_file(os.path.join(source_factory, "FactoryResults/PostgresDriverFactory.cs"), os.path.join(dest_factory, "FactoryResults/MySqlDriverFactory.cs"))

# NpOn.MySqlAppExtUse
source_app = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Applications/ApplicationExtensions/NpOn.PostgresAppExtUse"
dest_app = "/home/leduc/LeDucPr/Dotnet/NpOn.Net-V2/Common/Applications/ApplicationExtensions/NpOn.MySqlAppExtUse"

convert_file(os.path.join(source_app, "PostgresServiceCollectionExtensions.cs"), os.path.join(dest_app, "MySqlServiceCollectionExtensions.cs"))

print("Conversion complete!")
