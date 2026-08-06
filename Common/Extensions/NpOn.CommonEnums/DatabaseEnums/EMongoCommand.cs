using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

/// <summary>
/// Categorizes all MongoDB operations by level and type.
/// Levels: Database → Collection → Document.
/// </summary>
public enum EMongoCommand
{
    // ── Database level ────────────────────────────────────────────────────────
    [Display(Name = "List Collections")]        ListCollections = 0,
    [Display(Name = "Create Collection")]       CreateCollection = 1,
    [Display(Name = "Drop Collection")]         DropCollection   = 2,
    [Display(Name = "Run Command")]             RunDbCommand     = 3,

    // ── Collection / Query level ──────────────────────────────────────────────
    [Display(Name = "Find One")]                FindOne          = 10,
    [Display(Name = "Find Many")]               FindMany         = 11,
    [Display(Name = "Count Documents")]         CountDocuments   = 12,
    [Display(Name = "Distinct")]                Distinct         = 13,
    [Display(Name = "Exists")]                  Exists           = 14,

    // ── Write level ───────────────────────────────────────────────────────────
    [Display(Name = "Insert One")]              InsertOne        = 20,
    [Display(Name = "Insert Many")]             InsertMany       = 21,
    [Display(Name = "Update One")]              UpdateOne        = 22,
    [Display(Name = "Update Many")]             UpdateMany       = 23,
    [Display(Name = "Replace One")]             ReplaceOne       = 24,
    [Display(Name = "Delete One")]              DeleteOne        = 25,
    [Display(Name = "Delete Many")]             DeleteMany       = 26,
    [Display(Name = "Upsert One")]              UpsertOne        = 27,

    // ── Bulk write ────────────────────────────────────────────────────────────
    [Display(Name = "Bulk Write")]              BulkWrite        = 30,

    // ── Aggregation pipeline ──────────────────────────────────────────────────
    [Display(Name = "Aggregate")]               Aggregate        = 40,

    // ── Index management ──────────────────────────────────────────────────────
    [Display(Name = "Create Index")]            CreateIndex      = 50,
    [Display(Name = "Drop Index")]              DropIndex        = 51,
    [Display(Name = "List Indexes")]            ListIndexes      = 52,

    // ── Custom / Raw ──────────────────────────────────────────────────────────
    [Display(Name = "Customize")]               Customize        = 99,
}
