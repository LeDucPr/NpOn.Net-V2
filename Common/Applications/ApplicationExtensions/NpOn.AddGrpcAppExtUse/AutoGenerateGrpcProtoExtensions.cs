using System.Reflection;
using System.ServiceModel;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;

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
}