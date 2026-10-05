using System.Reflection;
using System.ServiceModel;
using System.Text.RegularExpressions;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using ProtoBuf;
using ProtoBuf.Grpc.Reflection;
using ProtoBuf.Meta;

namespace Common.Applications.ApplicationsExtensions.NpOn.AddGrpcAppExtUse;

public static class AutoGenerateGrpcProtoExtensions
{
    /// <summary>
    /// Auto generates .proto files physically on disk when the application starts in Dev environment.
    /// Eliminates the need for API reflection and securely hides schemas on Production.
    /// </summary>
    public static void ExportProtoFileOnDev(this IApplicationBuilder app, Assembly assembly)
    {
        bool isDev = EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool();
        if (!isDev)
        {
            return;
        }

        // Navigate to the root directory where the application is executed
        string currentDir = Directory.GetCurrentDirectory();
        string protoRootPath = Path.Combine(currentDir, "proto");

        try
        {
            if (Directory.Exists(protoRootPath))
            {
                Directory.Delete(protoRootPath, true);
            }

            Directory.CreateDirectory(protoRootPath);

            ProtoBuf.Grpc.Reflection.SchemaGenerator generator = new ProtoBuf.Grpc.Reflection.SchemaGenerator();

            // Export bcl.proto automatically to resolve missing file errors in Postman
            string bclDirectory = Path.Combine(protoRootPath, "protobuf-net");
            if (!Directory.Exists(bclDirectory))
            {
                Directory.CreateDirectory(bclDirectory);
            }

            // from lib
            string bclContent = @"syntax = ""proto3"";
                package bcl;

                message TimeSpan {
                   int64 value = 1; // default value could not be applied: 00:00:00
                   int32 scale = 2; // default value could not be applied: Days
                }
                message DateTime {
                   int64 value = 1; // default value could not be applied: 0001-01-01T00:00:00
                   int32 scale = 2; // default value could not be applied: Days
                   int32 kind = 3; // default value could not be applied: Unspecified
                }
                message NetObjectProxy {
                   int32 existingObjectKey = 1;
                   int32 newObjectKey = 2;
                   int32 existingTypeKey = 3;
                   int32 newTypeKey = 4;
                   int32 typeNameKey = 5;
                   bytes payload = 8;
                   string typeString = 9;
                }
                message Guid {
                   fixed64 lo = 1; // default value could not be applied: 0
                   fixed64 hi = 2; // default value could not be applied: 0
                }
                message Decimal {
                   uint64 lo = 1; // default value could not be applied: 0
                   uint32 hi = 2; // default value could not be applied: 0
                   uint32 signScale = 3; // default value could not be applied: 0
                }";
            File.WriteAllText(Path.Combine(bclDirectory, "bcl.proto"), bclContent);

            // Find all interfaces marked with [ServiceContract]
            List<Type> contractTypes = assembly.GetTypes()
                .Where(t => t.IsInterface && t.GetCustomAttributes(true)
                    .Any(a => a.GetType().Name == nameof(ServiceContractAttribute)))
                .ToList();

            foreach (Type type in contractTypes)
            {
                string schema = generator.GetSchema(type);

                string nameSpace = type.Namespace ?? "";
                string subFolder = string.Empty;

                // Shorten path: Search for meaningful category (InterfaceGrpcControllers, MicroServices, etc.) from right to left
                string[] segments = nameSpace.Split('.');
                for (int i = segments.Length - 1; i >= 0; i--)
                {
                    if (segments[i].Contains("Controllers") || segments[i].Contains("MicroServices"))
                    {
                        subFolder = segments[i];
                        break;
                    }
                }

                string nestedFolderPath = Path.Combine(protoRootPath, subFolder, type.Name);

                if (!Directory.Exists(nestedFolderPath))
                {
                    Directory.CreateDirectory(nestedFolderPath);
                }

                string filePath = Path.Combine(nestedFolderPath, $"{type.Name}.proto");
                File.WriteAllText(filePath, schema);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting Proto Files: {ex.Message}");
        }
    }


    /// <summary>
    /// Xuất file .proto độc lập 100%, chuẩn cấu trúc gRPC, không phụ thuộc bcl.proto hay google protobuf imports.
    /// Tương thích ngay lập tức với Postman, grpcurl, và các ngôn ngữ khác (Go, Java, Node.js).
    /// </summary>
    public static void ExportStandaloneProtoFileOnDev(this IApplicationBuilder app, Assembly assembly)
    {
        bool isDev = EApplicationConfiguration.IsDevEnvironment.GetAppSettingConfig().AsDefaultBool();
        if (!isDev)
        {
            return;
        }

        string currentDir = Directory.GetCurrentDirectory();
        string protoRootPath = Path.Combine(currentDir, "proto");

        try
        {
            // 1. Dọn dẹp và tạo mới thư mục gốc
            if (Directory.Exists(protoRootPath))
            {
                Directory.Delete(protoRootPath, true);
            }

            Directory.CreateDirectory(protoRootPath);

            // 2. Khởi tạo Schema Generator chính chủ của protobuf-net.Grpc
            var generator = new SchemaGenerator();

            // 3. Tìm tất cả Interface có gắn attribute [ServiceContract]
            var contractTypes = assembly.GetTypes()
                .Where(t => t.IsInterface && t.GetCustomAttributes(false)
                    .Any(a => a.GetType().Name.EndsWith("ServiceContractAttribute")))
                .ToList();

            if (!contractTypes.Any())
            {
                Console.WriteLine("[Proto Export] No ServiceContract interfaces found.");
                return;
            }

            // 4. Xuất từng file .proto riêng biệt theo đúng chuẩn module hóa
            foreach (Type type in contractTypes)
            {
                try
                {
                    // Sinh schema gốc
                    string rawSchema = generator.GetSchema(type);

                    // Làm sạch schema: loại bỏ phụ thuộc bcl/google và map về primitive types
                    string sanitizedSchema = SanitizeSchemaForUniversalCompatibility(rawSchema);

                    // Xác định đường dẫn thư mục: Ưu tiên 'package' trong schema, fallback về logic namespace
                    string targetFolder = ResolveProtoFolderPath(type, sanitizedSchema, protoRootPath);

                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }

                    string filePath = Path.Combine(targetFolder, $"{type.Name}.proto");
                    File.WriteAllText(filePath, sanitizedSchema);

                    Console.WriteLine($"[Proto Export] Successfully generated: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Proto Export] Failed to generate schema for {type.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Proto Export] Critical error during export: {ex.Message}");
        }
    }

    /// <summary>
    /// Biến đổi schema thành 100% chuẩn Google Proto3 Style Guide.
    /// Xử lý triệt để: snake_case cho field, UPPER_SNAKE_CASE cho enum, xóa comment rác.
    /// </summary>
    private static string SanitizeSchemaForUniversalCompatibility(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return schema;

        // 1. Xóa sạch các lệnh import phụ thuộc
        schema = Regex.Replace(schema, @"^\s*import\s+""[^""]+"";\s*", "", RegexOptions.Multiline);

        // 2. Map các kiểu bcl đặc thù về kiểu nguyên thủy chuẩn Proto3
        schema = Regex.Replace(schema, @"(\.)?bcl\.DateTime\b", "string", RegexOptions.IgnoreCase);
        schema = Regex.Replace(schema, @"(\.)?bcl\.Guid\b", "string", RegexOptions.IgnoreCase);
        schema = Regex.Replace(schema, @"(\.)?bcl\.Decimal\b", "string", RegexOptions.IgnoreCase);
        schema = Regex.Replace(schema, @"(\.)?bcl\.TimeSpan\b", "string", RegexOptions.IgnoreCase);
        schema = Regex.Replace(schema, @"(\.)?bcl\.NetObjectProxy\b", "bytes", RegexOptions.IgnoreCase);

        // 3. Xóa comment rác của protobuf-net (xử lý cả khoảng trắng thừa)
        schema = Regex.Replace(schema, @"\s*//\s*default value could not be applied[^\n]*", "");
        schema = Regex.Replace(schema, @"\s*//\s*proto3 requires a zero value[^\n]*", "");
        schema = Regex.Replace(schema, @"\s*//\s*this is a composite/flags enumeration[^\n]*", "");
        schema = Regex.Replace(schema, @"^\s*//\s*schema for protobuf-net[^\n]*", "", RegexOptions.Multiline);

        // 4. Chuẩn hóa Package Name thành lowercase
        schema = Regex.Replace(schema, @"^package\s+([a-zA-Z0-9_.]+)\s*;", match =>
            $"package {match.Groups[1].Value.ToLower()};", RegexOptions.Multiline);

        // 5. Chuẩn hóa tên Field trong Message (bao gồm cả trường có từ khóa 'repeated')
        // Match: [optional repeated] [Type] [PascalCaseName] = [Number];
        schema = Regex.Replace(schema, @"^\s*(repeated\s+)?([a-zA-Z0-9_]+)\s+([A-Z][a-zA-Z0-9_]*)\s*=\s*(\d+)\s*;",
            match =>
            {
                string repeatedKeyword = match.Groups[1].Value; // "repeated " hoặc ""
                string type = match.Groups[2].Value.Trim();
                string fieldName = match.Groups[3].Value;
                string number = match.Groups[4].Value;

                // Chuyển PascalCase sang lower_snake_case (AccountId -> account_id)
                string snakeCaseName = Regex.Replace(fieldName, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();

                return $"  {repeatedKeyword}{type} {snakeCaseName} = {number};";
            }, RegexOptions.Multiline);

        // 6. Chuẩn hóa tên giá trị trong Enum thành UPPER_SNAKE_CASE
        // Match: [Spaces] [PascalCaseName] = [Number]; (chỉ áp dụng cho các dòng trông giống enum value)
        schema = Regex.Replace(schema, @"^\s+([A-Z][a-zA-Z0-9_]*)\s*=\s*(\d+)\s*;", match =>
        {
            string enumName = match.Groups[1].Value;
            string number = match.Groups[2].Value;

            // Chuyển PascalCase sang UPPER_SNAKE_CASE (NoErrorCode -> NO_ERROR_CODE)
            string upperSnakeName = Regex.Replace(enumName, @"([a-z0-9])([A-Z])", "$1_$2").ToUpper();

            return $"  {upperSnakeName} = {number};";
        }, RegexOptions.Multiline);

        // 7. Dọn dẹp các dòng trống thừa (giữ tối đa 1 dòng trống liên tiếp)
        schema = Regex.Replace(schema, @"\n\s*\n\s*\n+", "\n\n");

        return schema.Trim();
    }

    /// <summary>
    /// Xác định đường dẫn thư mục chứa file .proto.
    /// Ưu tiên cao nhất: Dựa vào khai báo 'package' trong file .proto (Chuẩn gRPC).
    /// Fallback: Dựa vào heuristic namespace của dự án (như code cũ).
    /// </summary>
    private static string ResolveProtoFolderPath(Type type, string schema, string protoRootPath)
    {
        // Cố gắng trích xuất package name từ schema (ví dụ: package MyCompany.Services;)
        var packageMatch = Regex.Match(schema, @"^\s*package\s+([a-zA-Z0-9_.]+)\s*;", RegexOptions.Multiline);

        if (packageMatch.Success)
        {
            string packageName = packageMatch.Groups[1].Value;
            // Chuyển đổi package name thành đường dẫn thư mục (ví dụ: MyCompany.Services -> MyCompany/Services)
            return Path.Combine(protoRootPath, packageName.Replace('.', Path.DirectorySeparatorChar));
        }

        // Fallback: Logic heuristic cũ của bạn, được làm sạch và mở rộng thêm một chút
        string nameSpace = type.Namespace ?? "";
        string subFolder = "Contracts"; // Mặc định an toàn

        string[] segments = nameSpace.Split('.');
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            string segment = segments[i];
            if (segment.Contains("Controllers") ||
                segment.Contains("MicroServices") ||
                segment.Contains("Services") ||
                segment.Contains("Contracts"))
            {
                subFolder = segment;
                break;
            }
        }

        // Cấu trúc: proto/{subFolder}/{TypeName}
        return Path.Combine(protoRootPath, subFolder, type.Name);
    }
}