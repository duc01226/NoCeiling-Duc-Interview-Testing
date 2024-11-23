using Microsoft.EntityFrameworkCore.Migrations;

namespace Easy.Platform.EfCore.Utils;

public static class SqlServerMigrationUtil
{
    public static void DropFullTextIndexIfExists(MigrationBuilder migrationBuilder, string tableName, bool suppressTransaction = true)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (select 1 from sys.fulltext_indexes
                join sys.objects on fulltext_indexes.object_id = objects.object_id where objects.name = '{tableName}')
                DROP FULLTEXT INDEX ON dbo.{tableName}",
            suppressTransaction);
    }

    public static void CreateFullTextIndexIfNotExists(
        MigrationBuilder migrationBuilder,
        string tableName,
        List<string> columnNames,
        string keyIndex,
        string fullTextCatalog,
        bool suppressTransaction = true)
    {
        CreateFullTextCatalogIfNotExists(migrationBuilder, fullTextCatalog, suppressTransaction);

        migrationBuilder.Sql(
            @$"IF NOT EXISTS (select 1 from sys.fulltext_indexes
                join sys.objects on fulltext_indexes.object_id = objects.object_id where objects.name = '{tableName}')
                CREATE FULLTEXT INDEX ON dbo.{tableName}({columnNames.JoinToString(",")}) KEY INDEX {keyIndex} ON {fullTextCatalog} WITH (STOPLIST=OFF)",
            suppressTransaction);
    }

    public static void CreateFullTextCatalogIfNotExists(MigrationBuilder migrationBuilder, string catalogName, bool suppressTransaction = true)
    {
        migrationBuilder.Sql(
            @$"IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = '{catalogName}')
                BEGIN
                    CREATE FULLTEXT CATALOG [{catalogName}]
                END",
            suppressTransaction);
    }

    public static void DropFullTextCatalogIfExists(MigrationBuilder migrationBuilder, string catalogName, bool suppressTransaction = true)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = '{catalogName}')
                BEGIN
                    DROP FULLTEXT CATALOG {catalogName}
                END",
            suppressTransaction);
    }

    public static void DropIndexIfExists(MigrationBuilder migrationBuilder, string indexName, string tableName, bool suppressTransaction = false)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = '{indexName}' AND object_id = OBJECT_ID('dbo.{tableName}'))
                DROP INDEX {indexName} ON {tableName}; ",
            suppressTransaction);
    }

    public static void DropConstraintIfExists(MigrationBuilder migrationBuilder, string constraintName, string tableName, bool suppressTransaction = false)
    {
        // Drop foreign key constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END",
            suppressTransaction);

        // Drop check constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END",
            suppressTransaction);

        // Drop default constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END",
            suppressTransaction);

        // Drop unique constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.objects
                WHERE name = '{constraintName}' AND type = 'UQ' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END",
            suppressTransaction);
    }

    public static void CreateIndex(MigrationBuilder migrationBuilder, string tableName, string[] cols, bool suppressTransaction = false)
    {
        migrationBuilder.Sql(
            @$"CREATE NONCLUSTERED INDEX [IX_{tableName}_{cols.JoinToString("_")}] ON [dbo].[{tableName}] ({cols.Select(col => $"[{col}] ASC").JoinToString(",")})",
            suppressTransaction);
    }

    public static void CreateIndexIfNotExists(MigrationBuilder migrationBuilder, string tableName, string[] cols, bool suppressTransaction = false)
    {
        var indexName = $"IX_{tableName}_{string.Join("_", cols)}";
        var createIndexSql = @$"
        IF NOT EXISTS (SELECT 1 
                       FROM sys.indexes 
                       WHERE name = '{indexName}' 
                       AND object_id = OBJECT_ID('[dbo].[{tableName}]'))
        BEGIN
            CREATE NONCLUSTERED INDEX [{indexName}] 
            ON [dbo].[{tableName}] ({string.Join(", ", cols.Select(col => $"[{col}] ASC"))})
        END";

        migrationBuilder.Sql(createIndexSql, suppressTransaction);
    }

    public static void CreateUniqueIndex(MigrationBuilder migrationBuilder, string tableName, string[] cols, bool suppressTransaction = false)
    {
        migrationBuilder.Sql(
            @$"CREATE UNIQUE NONCLUSTERED INDEX [IX_{tableName}_{cols.JoinToString("_")}] ON [dbo].[{tableName}] ({cols.Select(col => $"[{col}] ASC").JoinToString(",")})",
            suppressTransaction);
    }

    public static void CreateUniqueIndexIfNotExists(MigrationBuilder migrationBuilder, string tableName, string[] cols, bool suppressTransaction = false)
    {
        var indexName = $"IX_{tableName}_{string.Join("_", cols)}";
        var createUniqueIndexSql = @$"
        IF NOT EXISTS (SELECT 1 
                       FROM sys.indexes 
                       WHERE name = '{indexName}' 
                       AND object_id = OBJECT_ID('[dbo].[{tableName}]'))
        BEGIN
            CREATE UNIQUE NONCLUSTERED INDEX [{indexName}] 
            ON [dbo].[{tableName}] ({string.Join(", ", cols.Select(col => $"[{col}] ASC"))})
        END";

        migrationBuilder.Sql(createUniqueIndexSql, suppressTransaction);
    }
}
