using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Common.Infrastructures.NpOn.MongoDbExtCm.Bsons;

// ─────────────────────────────────────────────────────────────────────────────
//  Index key descriptor
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Describes a single field key in an index definition.</summary>
public sealed class MongoIndexKey
{
    public string Field { get; }

    /// <summary>1 = ascending, -1 = descending, "text" for text index, "2dsphere" for geo.</summary>
    public BsonValue Direction { get; }

    public MongoIndexKey(string field, int direction = 1) =>
        (Field, Direction) = (field, direction);

    private MongoIndexKey(string field, BsonValue direction) =>
        (Field, Direction) = (field, direction);

    /// <summary>Creates a text-index key entry.</summary>
    public static MongoIndexKey Text(string field) => new(field, "text");
}

// ─────────────────────────────────────────────────────────────────────────────
//  Bulk-write model
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Wraps a single operation inside a BulkWrite command.</summary>
public sealed class MongoBulkModel
{
    public WriteModel<BsonDocument> Model { get; }

    private MongoBulkModel(WriteModel<BsonDocument> model) => Model = model;

    public static MongoBulkModel Insert(BsonDocument doc) =>
        new(new InsertOneModel<BsonDocument>(doc));

    public static MongoBulkModel UpdateOne(BsonDocument filter, BsonDocument update, bool upsert = false) =>
        new(new UpdateOneModel<BsonDocument>(filter, update) { IsUpsert = upsert });

    public static MongoBulkModel UpdateMany(BsonDocument filter, BsonDocument update) =>
        new(new UpdateManyModel<BsonDocument>(filter, update));

    public static MongoBulkModel DeleteOne(BsonDocument filter) =>
        new(new DeleteOneModel<BsonDocument>(filter));

    public static MongoBulkModel DeleteMany(BsonDocument filter) =>
        new(new DeleteManyModel<BsonDocument>(filter));

    public static MongoBulkModel ReplaceOne(BsonDocument filter, BsonDocument replacement, bool upsert = false) =>
        new(new ReplaceOneModel<BsonDocument>(filter, replacement) { IsUpsert = upsert });
}

// ─────────────────────────────────────────────────────────────────────────────
//  MongoDbCommand – main command class
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Unified command object for ALL MongoDB operations.
/// Mirrors the hierarchy:  Database → Collection → Document(s).
/// <para>
/// Use the static factory methods to create the right command type;
/// they are self-documenting and prevent invalid combinations.
/// </para>
/// </summary>
public class MongoDbCommand : NpOnDbCommand
{
    // ── Shared properties ─────────────────────────────────────────────────────

    /// <summary>Operation type.</summary>
    public EMongoCommand CommandType { get; }

    /// <summary>
    /// Override for the target collection name.
    /// If <c>null</c> the driver uses the collection configured in
    /// <c>MongoNpOnDbConnectOption.CollectionName</c>.
    /// </summary>
    public string? CollectionName { get; private set; }

    // ── Filter / query ────────────────────────────────────────────────────────

    /// <summary>BSON filter document used in Find / Update / Delete operations.</summary>
    public BsonDocument? Filter { get; private set; }

    /// <summary>Projection document (optional on FindOne / FindMany).</summary>
    public BsonDocument? Projection { get; private set; }

    /// <summary>Sort document.</summary>
    public BsonDocument? Sort { get; private set; }

    /// <summary>Maximum number of documents to return.</summary>
    public int? Limit { get; private set; }

    /// <summary>Number of documents to skip.</summary>
    public int? Skip { get; private set; }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Single document for InsertOne / ReplaceOne / UpsertOne.</summary>
    public BsonDocument? Document { get; private set; }

    /// <summary>Documents for InsertMany.</summary>
    public IReadOnlyList<BsonDocument>? Documents { get; private set; }

    /// <summary>Update definition document (e.g. <c>{ "$set": { "name": "x" } }</c>).</summary>
    public BsonDocument? UpdateDefinition { get; private set; }

    /// <summary>Upsert flag for UpdateOne / UpdateMany.</summary>
    public bool IsUpsert { get; private set; }

    // ── Aggregation ───────────────────────────────────────────────────────────

    /// <summary>Pre-built pipeline for Aggregate commands.</summary>
    public IReadOnlyList<BsonDocument>? Pipeline { get; private set; }

    // ── Bulk write ────────────────────────────────────────────────────────────

    /// <summary>Models for BulkWrite commands.</summary>
    public IReadOnlyList<MongoBulkModel>? BulkModels { get; private set; }

    /// <summary>Whether the bulk write should be ordered (default = true).</summary>
    public bool IsOrderedBulk { get; private set; } = true;

    // ── Index management ──────────────────────────────────────────────────────

    /// <summary>Index keys for CreateIndex.</summary>
    public IReadOnlyList<MongoIndexKey>? IndexKeys { get; private set; }

    /// <summary>Name of the index (used in DropIndex).</summary>
    public string? IndexName { get; private set; }

    /// <summary>Whether the index should be unique.</summary>
    public bool IsUniqueIndex { get; private set; }

    // ── Database-level commands ───────────────────────────────────────────────

    /// <summary>Raw BSON command for RunDbCommand.</summary>
    public BsonDocument? RawDbCommand { get; private set; }

    // ── Distinct ─────────────────────────────────────────────────────────────

    /// <summary>Field name used in Distinct queries.</summary>
    public string? DistinctField { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private base constructor
    // ─────────────────────────────────────────────────────────────────────────

    private MongoDbCommand(EMongoCommand commandType, string commandText)
        : base(EDb.MongoDb, commandText)
    {
        CommandType = commandType;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Fluent modifiers (return self for chaining)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Overrides the target collection at runtime.</summary>
    public MongoDbCommand WithCollection(string collectionName)
    {
        CollectionName = collectionName;
        return this;
    }

    /// <summary>Adds a projection to FindOne / FindMany.</summary>
    public MongoDbCommand WithProjection(BsonDocument projection)
    {
        Projection = projection;
        return this;
    }

    /// <summary>Convenience: project specific field names (1 = include).</summary>
    public MongoDbCommand WithProjection(params string[] includeFields)
    {
        var doc = new BsonDocument();
        foreach (var f in includeFields) doc.Add(f, 1);
        Projection = doc;
        return this;
    }

    /// <summary>Adds a sort to FindMany.</summary>
    public MongoDbCommand WithSort(BsonDocument sort)
    {
        Sort = sort;
        return this;
    }

    /// <summary>Sort by a single field.</summary>
    public MongoDbCommand WithSort(string field, bool ascending = true)
        => WithSort(new BsonDocument(field, ascending ? 1 : -1));

    /// <summary>Limits results (FindMany / Aggregate).</summary>
    public MongoDbCommand WithLimit(int limit)
    {
        Limit = limit;
        return this;
    }

    /// <summary>Skips documents (FindMany / Aggregate).</summary>
    public MongoDbCommand WithSkip(int skip)
    {
        Skip = skip;
        return this;
    }

    /// <summary>Configures paging (skip = pageIndex * pageSize, limit = pageSize).</summary>
    public MongoDbCommand WithPage(int pageIndex, int pageSize)
        => WithSkip(pageIndex * pageSize).WithLimit(pageSize);

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Database level
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Lists all collections in the connected database.</summary>
    public static MongoDbCommand ForListCollections()
        => new(EMongoCommand.ListCollections, "ListCollections");

    /// <summary>Creates a new collection in the database.</summary>
    public static MongoDbCommand ForCreateCollection(string collectionName)
        => new(EMongoCommand.CreateCollection, $"CreateCollection {collectionName}")
        {
            CollectionName = collectionName
        };

    /// <summary>Drops a collection from the database.</summary>
    public static MongoDbCommand ForDropCollection(string collectionName)
        => new(EMongoCommand.DropCollection, $"DropCollection {collectionName}")
        {
            CollectionName = collectionName
        };

    /// <summary>Runs a raw BSON command against the database (e.g. <c>{ ping: 1 }</c>).</summary>
    public static MongoDbCommand ForRunDbCommand(BsonDocument command)
        => new(EMongoCommand.RunDbCommand, $"RunDbCommand {command}")
        {
            RawDbCommand = command
        };

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Query (read) level
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds a single document matching <paramref name="filter"/>.
    /// Pass <c>"{}"</c> or <c>BsonDocument.Empty</c> to match any document.
    /// </summary>
    public static MongoDbCommand ForFindOne(BsonDocument filter)
        => new(EMongoCommand.FindOne, $"FindOne {filter}")
        {
            Filter = filter
        };

    /// <inheritdoc cref="ForFindOne(BsonDocument)"/>
    public static MongoDbCommand ForFindOne(string filterJson)
        => ForFindOne(BsonDocument.Parse(filterJson));

    /// <summary>Finds all documents matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForFindMany(BsonDocument filter)
        => new(EMongoCommand.FindMany, $"FindMany {filter}")
        {
            Filter = filter
        };

    /// <inheritdoc cref="ForFindMany(BsonDocument)"/>
    public static MongoDbCommand ForFindMany(string filterJson = "{}")
        => ForFindMany(BsonDocument.Parse(filterJson));

    /// <summary>Counts documents matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForCount(BsonDocument? filter = null)
        => new(EMongoCommand.CountDocuments, "CountDocuments")
        {
            Filter = filter ?? new BsonDocument()
        };

    /// <summary>Returns distinct values of <paramref name="field"/> matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForDistinct(string field, BsonDocument? filter = null)
        => new(EMongoCommand.Distinct, $"Distinct {field}")
        {
            DistinctField = field,
            Filter        = filter ?? new BsonDocument()
        };

    /// <summary>Checks whether at least one document matches <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForExists(BsonDocument filter)
        => new(EMongoCommand.Exists, $"Exists {filter}")
        {
            Filter = filter
        };

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Write level
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Inserts a single document.</summary>
    public static MongoDbCommand ForInsertOne(BsonDocument document)
        => new(EMongoCommand.InsertOne, "InsertOne")
        {
            Document = document
        };

    /// <summary>Inserts multiple documents in one round-trip.</summary>
    public static MongoDbCommand ForInsertMany(IReadOnlyList<BsonDocument> documents)
        => new(EMongoCommand.InsertMany, $"InsertMany ({documents.Count} docs)")
        {
            Documents = documents
        };

    /// <summary>Updates the first document matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForUpdateOne(BsonDocument filter, BsonDocument update, bool upsert = false)
        => new(EMongoCommand.UpdateOne, $"UpdateOne {filter}")
        {
            Filter           = filter,
            UpdateDefinition = update,
            IsUpsert         = upsert
        };

    /// <summary>Updates all documents matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForUpdateMany(BsonDocument filter, BsonDocument update)
        => new(EMongoCommand.UpdateMany, $"UpdateMany {filter}")
        {
            Filter           = filter,
            UpdateDefinition = update
        };

    /// <summary>Replaces a single document matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForReplaceOne(BsonDocument filter, BsonDocument replacement, bool upsert = false)
        => new(EMongoCommand.ReplaceOne, $"ReplaceOne {filter}")
        {
            Filter   = filter,
            Document = replacement,
            IsUpsert = upsert
        };

    /// <summary>Upserts a single document (insert-or-update).</summary>
    public static MongoDbCommand ForUpsertOne(BsonDocument filter, BsonDocument update)
        => new(EMongoCommand.UpsertOne, $"UpsertOne {filter}")
        {
            Filter           = filter,
            UpdateDefinition = update,
            IsUpsert         = true
        };

    /// <summary>Deletes the first document matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForDeleteOne(BsonDocument filter)
        => new(EMongoCommand.DeleteOne, $"DeleteOne {filter}")
        {
            Filter = filter
        };

    /// <summary>Deletes all documents matching <paramref name="filter"/>.</summary>
    public static MongoDbCommand ForDeleteMany(BsonDocument filter)
        => new(EMongoCommand.DeleteMany, $"DeleteMany {filter}")
        {
            Filter = filter
        };

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Bulk write
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes multiple write operations as a single bulk request.
    /// Use <see cref="MongoBulkModel"/> static helpers to build each operation.
    /// </summary>
    public static MongoDbCommand ForBulkWrite(IReadOnlyList<MongoBulkModel> models, bool ordered = true)
        => new(EMongoCommand.BulkWrite, $"BulkWrite ({models.Count} ops)")
        {
            BulkModels    = models,
            IsOrderedBulk = ordered
        };

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Aggregation pipeline
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the given aggregation pipeline against a collection.
    /// Build your pipeline with <see cref="MongoAggregateBuilder"/>.
    /// </summary>
    public static MongoDbCommand ForAggregate(IReadOnlyList<BsonDocument> pipeline)
        => new(EMongoCommand.Aggregate, $"Aggregate ({pipeline.Count} stages)")
        {
            Pipeline = pipeline
        };

    /// <summary>
    /// Convenience overload that accepts a <see cref="MongoAggregateBuilder"/> directly.
    /// </summary>
    public static MongoDbCommand ForAggregate(MongoAggregateBuilder builder)
        => ForAggregate(builder.Build());

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Index management
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Creates an index on the collection.</summary>
    public static MongoDbCommand ForCreateIndex(
        IReadOnlyList<MongoIndexKey> keys,
        string? indexName   = null,
        bool    isUnique    = false)
        => new(EMongoCommand.CreateIndex, $"CreateIndex [{string.Join(",", keys.Select(k => k.Field))}]")
        {
            IndexKeys       = keys,
            IndexName       = indexName,
            IsUniqueIndex   = isUnique
        };

    /// <summary>Drops a named index from the collection.</summary>
    public static MongoDbCommand ForDropIndex(string indexName)
        => new(EMongoCommand.DropIndex, $"DropIndex {indexName}")
        {
            IndexName = indexName
        };

    /// <summary>Lists all indexes defined on the collection.</summary>
    public static MongoDbCommand ForListIndexes()
        => new(EMongoCommand.ListIndexes, "ListIndexes");

    // ─────────────────────────────────────────────────────────────────────────
    //  Static factories – Custom / Raw
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Sends a raw command text (BSON JSON string) for special cases.</summary>
    public static MongoDbCommand ForCustomize(string rawCommandText)
        => new(EMongoCommand.Customize, rawCommandText);
}