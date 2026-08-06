using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.MongoDbExtCm.Bsons;
using Common.Infrastructures.NpOn.MongoDbExtCm.Results;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Common.Infrastructures.NpOn.MongoDbExtCm.Connections;

/// <summary>
/// MongoDB driver implementation.
/// Handles the full command hierarchy:
///   Database → Collection → Document(s) → Aggregation / Index / BulkWrite.
/// </summary>
public class MongoDbDriver : NpOnDbDriver
{
    private MongoClient?              _client;
    private IMongoDatabase?           _database;

    public sealed override string Name    { get; set; } = "MongoDB";
    public sealed override string Version { get; set; } = "Unknown";

    public override bool IsValidSession => _client != null && _database != null;

    public MongoDbDriver(MongoNpOnDbConnectOption option) : base(option) { }

    // ─────────────────────────────────────────────────────────────────────────
    //  Connection
    // ─────────────────────────────────────────────────────────────────────────

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        try
        {
            var settings = MongoClientSettings.FromConnectionString(Option.ConnectionString);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);
            _client   = new MongoClient(settings);
            _database = _client.GetDatabase(Option.DatabaseName);

            await _database.RunCommandAsync(
                (Command<BsonDocument>)"{ping: 1}", cancellationToken: cancellationToken);

            var buildInfo = await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("buildInfo", 1), cancellationToken: cancellationToken);
            Version = buildInfo["version"].AsString;
            Name    = $"MongoDB {Version}";
        }
        catch (Exception)
        {
            _client   = null;
            _database = null;
        }
    }

    public override Task DisconnectAsync()
    {
        _client   = null;
        _database = null;
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Execute dispatcher
    // ─────────────────────────────────────────────────────────────────────────

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _database == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Session);

        // ── Legacy MongoCommand (raw BSON filter string) ──────────────────────
        if (command is INpOnDbCommand legacyCmd && command is not MongoDbCommand)
        {
            return await ExecuteLegacy(legacyCmd);
        }

        if (command is not MongoDbCommand mongoCmd)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        try
        {
            return mongoCmd.CommandType switch
            {
                // Database level
                EMongoCommand.ListCollections  => await ExecListCollections(),
                EMongoCommand.CreateCollection => await ExecCreateCollection(mongoCmd),
                EMongoCommand.DropCollection   => await ExecDropCollection(mongoCmd),
                EMongoCommand.RunDbCommand     => await ExecRunDbCommand(mongoCmd),

                // Query (read) level
                EMongoCommand.FindOne          => await ExecFindOne(mongoCmd),
                EMongoCommand.FindMany         => await ExecFindMany(mongoCmd),
                EMongoCommand.CountDocuments   => await ExecCountDocuments(mongoCmd),
                EMongoCommand.Distinct         => await ExecDistinct(mongoCmd),
                EMongoCommand.Exists           => await ExecExists(mongoCmd),

                // Write level
                EMongoCommand.InsertOne        => await ExecInsertOne(mongoCmd),
                EMongoCommand.InsertMany       => await ExecInsertMany(mongoCmd),
                EMongoCommand.UpdateOne        => await ExecUpdateOne(mongoCmd),
                EMongoCommand.UpdateMany       => await ExecUpdateMany(mongoCmd),
                EMongoCommand.ReplaceOne       => await ExecReplaceOne(mongoCmd),
                EMongoCommand.UpsertOne        => await ExecUpsertOne(mongoCmd),
                EMongoCommand.DeleteOne        => await ExecDeleteOne(mongoCmd),
                EMongoCommand.DeleteMany       => await ExecDeleteMany(mongoCmd),

                // Bulk
                EMongoCommand.BulkWrite        => await ExecBulkWrite(mongoCmd),

                // Aggregation
                EMongoCommand.Aggregate        => await ExecAggregate(mongoCmd),

                // Index management
                EMongoCommand.CreateIndex      => await ExecCreateIndex(mongoCmd),
                EMongoCommand.DropIndex        => await ExecDropIndex(mongoCmd),
                EMongoCommand.ListIndexes      => await ExecListIndexes(mongoCmd),

                // Custom / raw
                EMongoCommand.Customize        => await ExecCustomize(mongoCmd),

                _ => new MongoResultSetWrapper().SetFail(EDbError.Command)
            };
        }
        catch (Exception)
        {
            return new MongoResultSetWrapper().SetFail(EDbError.CommandText);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collection resolver
    // ─────────────────────────────────────────────────────────────────────────

    private IMongoCollection<BsonDocument> GetCollection(MongoDbCommand cmd)
    {
        var name = cmd.CollectionName
                   ?? (string.IsNullOrWhiteSpace(Option.CollectionName)
                       ? throw new InvalidOperationException(
                           "No collection name specified in command or options.")
                       : Option.CollectionName);
        return _database!.GetCollection<BsonDocument>(name);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static FindOptions<BsonDocument> BuildFindOptions(MongoDbCommand cmd)
    {
        var opts = new FindOptions<BsonDocument>();
        if (cmd.Projection != null) opts.Projection = cmd.Projection;
        if (cmd.Sort       != null) opts.Sort        = cmd.Sort;
        if (cmd.Limit.HasValue)     opts.Limit       = cmd.Limit;
        if (cmd.Skip.HasValue)      opts.Skip        = cmd.Skip;
        return opts;
    }

    private static MongoResultSetWrapper WrapScalar(long value)
    {
        var doc = new BsonDocument("value", new BsonInt64(value));
        return new MongoResultSetWrapper([doc]);
    }

    private static MongoResultSetWrapper WrapScalar(bool value)
    {
        var doc = new BsonDocument("value", new BsonBoolean(value));
        return new MongoResultSetWrapper([doc]);
    }

    private static MongoResultSetWrapper WrapAffected(long affected, string key = "affected")
    {
        var doc = new BsonDocument(key, new BsonInt64(affected));
        return new MongoResultSetWrapper([doc]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Legacy handler (old MongoCommand with raw BSON text)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecuteLegacy(INpOnDbCommand cmd)
    {
        var collectionName = Option.CollectionName;
        if (string.IsNullOrWhiteSpace(collectionName))
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = _database!.GetCollection<BsonDocument>(collectionName);
        var filterText = string.IsNullOrWhiteSpace(cmd.CommandText) ? "{}" : cmd.CommandText;
        var filter     = BsonDocument.Parse(filterText);
        var documents  = await collection.Find(filter).ToListAsync();
        return new MongoResultSetWrapper(documents);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Database-level executors
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecListCollections()
    {
        var cursor    = await _database!.ListCollectionNamesAsync();
        var names     = await cursor.ToListAsync();
        var documents = names.Select(n => new BsonDocument("name", n)).ToList();
        return new MongoResultSetWrapper(documents);
    }

    private async Task<INpOnWrapperResult> ExecCreateCollection(MongoDbCommand cmd)
    {
        await _database!.CreateCollectionAsync(cmd.CollectionName!);
        return new MongoResultSetWrapper([new BsonDocument("created", cmd.CollectionName)]);
    }

    private async Task<INpOnWrapperResult> ExecDropCollection(MongoDbCommand cmd)
    {
        await _database!.DropCollectionAsync(cmd.CollectionName!);
        return new MongoResultSetWrapper([new BsonDocument("dropped", cmd.CollectionName)]);
    }

    private async Task<INpOnWrapperResult> ExecRunDbCommand(MongoDbCommand cmd)
    {
        if (cmd.RawDbCommand == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var result = await _database!.RunCommandAsync<BsonDocument>(cmd.RawDbCommand);
        return new MongoResultSetWrapper([result]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Query executors
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecFindOne(MongoDbCommand cmd)
    {
        var collection = GetCollection(cmd);
        var filter     = cmd.Filter ?? new BsonDocument();
        var opts       = BuildFindOptions(cmd);
        opts.Limit     = 1;

        var cursor = await collection.FindAsync(filter, opts);
        var doc    = await cursor.FirstOrDefaultAsync();
        return doc == null
            ? new MongoResultSetWrapper([])
            : new MongoResultSetWrapper([doc]);
    }

    private async Task<INpOnWrapperResult> ExecFindMany(MongoDbCommand cmd)
    {
        var collection = GetCollection(cmd);
        var filter     = cmd.Filter ?? new BsonDocument();
        var opts       = BuildFindOptions(cmd);

        var cursor    = await collection.FindAsync(filter, opts);
        var documents = await cursor.ToListAsync();
        return new MongoResultSetWrapper(documents);
    }

    private async Task<INpOnWrapperResult> ExecCountDocuments(MongoDbCommand cmd)
    {
        var collection = GetCollection(cmd);
        var filter     = cmd.Filter ?? new BsonDocument();
        var count      = await collection.CountDocumentsAsync(filter);
        return WrapScalar(count);
    }

    private async Task<INpOnWrapperResult> ExecDistinct(MongoDbCommand cmd)
    {
        if (cmd.DistinctField == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = GetCollection(cmd);
        var filter     = cmd.Filter ?? new BsonDocument();
        var cursor     = await collection.DistinctAsync<BsonValue>(cmd.DistinctField, filter);
        var values     = await cursor.ToListAsync();
        var docs       = values.Select(v => new BsonDocument("value", v)).ToList();
        return new MongoResultSetWrapper(docs);
    }

    private async Task<INpOnWrapperResult> ExecExists(MongoDbCommand cmd)
    {
        var collection = GetCollection(cmd);
        var filter     = cmd.Filter ?? new BsonDocument();
        var count      = await collection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 });
        return WrapScalar(count > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Write executors
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecInsertOne(MongoDbCommand cmd)
    {
        if (cmd.Document == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        await collection.InsertOneAsync(cmd.Document);
        return new MongoResultSetWrapper([cmd.Document]);
    }

    private async Task<INpOnWrapperResult> ExecInsertMany(MongoDbCommand cmd)
    {
        if (cmd.Documents == null || cmd.Documents.Count == 0)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        await collection.InsertManyAsync(cmd.Documents);
        return WrapAffected(cmd.Documents.Count, "inserted");
    }

    private async Task<INpOnWrapperResult> ExecUpdateOne(MongoDbCommand cmd)
    {
        if (cmd.Filter == null || cmd.UpdateDefinition == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var opts       = new UpdateOptions { IsUpsert = cmd.IsUpsert };
        var result     = await collection.UpdateOneAsync(cmd.Filter, cmd.UpdateDefinition, opts);
        return WrapAffected(result.ModifiedCount);
    }

    private async Task<INpOnWrapperResult> ExecUpdateMany(MongoDbCommand cmd)
    {
        if (cmd.Filter == null || cmd.UpdateDefinition == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var opts       = new UpdateOptions { IsUpsert = cmd.IsUpsert };
        var result     = await collection.UpdateManyAsync(cmd.Filter, cmd.UpdateDefinition, opts);
        return WrapAffected(result.ModifiedCount);
    }

    private async Task<INpOnWrapperResult> ExecReplaceOne(MongoDbCommand cmd)
    {
        if (cmd.Filter == null || cmd.Document == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var opts       = new ReplaceOptions { IsUpsert = cmd.IsUpsert };
        var result     = await collection.ReplaceOneAsync(cmd.Filter, cmd.Document, opts);
        return WrapAffected(result.ModifiedCount);
    }

    private async Task<INpOnWrapperResult> ExecUpsertOne(MongoDbCommand cmd)
    {
        if (cmd.Filter == null || cmd.UpdateDefinition == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var opts       = new UpdateOptions { IsUpsert = true };
        var result     = await collection.UpdateOneAsync(cmd.Filter, cmd.UpdateDefinition, opts);
        var affected   = result.UpsertedId != null ? 1L : result.ModifiedCount;
        return WrapAffected(affected);
    }

    private async Task<INpOnWrapperResult> ExecDeleteOne(MongoDbCommand cmd)
    {
        if (cmd.Filter == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var result     = await collection.DeleteOneAsync(cmd.Filter);
        return WrapAffected(result.DeletedCount, "deleted");
    }

    private async Task<INpOnWrapperResult> ExecDeleteMany(MongoDbCommand cmd)
    {
        if (cmd.Filter == null)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);
        var collection = GetCollection(cmd);
        var result     = await collection.DeleteManyAsync(cmd.Filter);
        return WrapAffected(result.DeletedCount, "deleted");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Bulk write executor
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecBulkWrite(MongoDbCommand cmd)
    {
        if (cmd.BulkModels == null || cmd.BulkModels.Count == 0)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = GetCollection(cmd);
        var models     = cmd.BulkModels.Select(m => m.Model).ToList();
        var opts       = new BulkWriteOptions { IsOrdered = cmd.IsOrderedBulk };
        var result     = await collection.BulkWriteAsync(models, opts);

        var summary = new BsonDocument
        {
            { "inserted",  result.InsertedCount  },
            { "modified",  result.ModifiedCount  },
            { "deleted",   result.DeletedCount   },
            { "upserted",  result.Upserts.Count  }
        };
        return new MongoResultSetWrapper([summary]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Aggregation executor
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecAggregate(MongoDbCommand cmd)
    {
        if (cmd.Pipeline == null || cmd.Pipeline.Count == 0)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = GetCollection(cmd);
        var pipeline   = PipelineDefinition<BsonDocument, BsonDocument>
                            .Create(cmd.Pipeline.ToList());

        var cursor    = await collection.AggregateAsync(pipeline);
        var documents = await cursor.ToListAsync();
        return new MongoResultSetWrapper(documents);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Index executors
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecCreateIndex(MongoDbCommand cmd)
    {
        if (cmd.IndexKeys == null || cmd.IndexKeys.Count == 0)
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = GetCollection(cmd);
        var keyDoc     = new BsonDocument();
        foreach (var key in cmd.IndexKeys)
            keyDoc.Add(key.Field, key.Direction);

        var model = new CreateIndexModel<BsonDocument>(
            keyDoc,
            new CreateIndexOptions
            {
                Name   = cmd.IndexName,
                Unique = cmd.IsUniqueIndex
            });

        var indexName = await collection.Indexes.CreateOneAsync(model);
        return new MongoResultSetWrapper([new BsonDocument("indexName", indexName)]);
    }

    private async Task<INpOnWrapperResult> ExecDropIndex(MongoDbCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.IndexName))
            return new MongoResultSetWrapper().SetFail(EDbError.Command);

        var collection = GetCollection(cmd);
        await collection.Indexes.DropOneAsync(cmd.IndexName);
        return new MongoResultSetWrapper([new BsonDocument("dropped", cmd.IndexName)]);
    }

    private async Task<INpOnWrapperResult> ExecListIndexes(MongoDbCommand cmd)
    {
        var collection = GetCollection(cmd);
        var cursor     = await collection.Indexes.ListAsync();
        var indexes    = await cursor.ToListAsync();
        return new MongoResultSetWrapper(indexes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Custom / raw executor
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<INpOnWrapperResult> ExecCustomize(MongoDbCommand cmd)
    {
        var filterText = string.IsNullOrWhiteSpace(cmd.CommandText) ? "{}" : cmd.CommandText;
        var collection = GetCollection(cmd);
        var filter     = BsonDocument.Parse(filterText);
        var documents  = await collection.Find(filter).ToListAsync();
        return new MongoResultSetWrapper(documents);
    }
}