using Microsoft.EntityFrameworkCore.Migrations;

namespace Easy.Platform.EfCore.Utils;

public static class SqlServerMigrationUtil
{
    public static void DropFullTextIndexIfExists(MigrationBuilder migrationBuilder, string tableName)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (select 1 from sys.fulltext_indexes
                join sys.objects on fulltext_indexes.object_id = objects.object_id where objects.name = '{tableName}')
                DROP FULLTEXT INDEX ON dbo.{tableName}",
            true);
    }

    public static void CreateFullTextIndexIfNotExists(
        MigrationBuilder migrationBuilder,
        string tableName,
        List<string> columnNames,
        string keyIndex,
        string fullTextCatalog)
    {
        migrationBuilder.Sql(
            @$"IF NOT EXISTS (select 1 from sys.fulltext_indexes
                join sys.objects on fulltext_indexes.object_id = objects.object_id where objects.name = '{tableName}')
                CREATE FULLTEXT INDEX ON dbo.{tableName}({columnNames.JoinToString(",")}) KEY INDEX {keyIndex} ON {fullTextCatalog} WITH (STOPLIST=OFF)",
            true);
    }

    public static void CreateFullTextCatalogIfNotExists(MigrationBuilder migrationBuilder, string catalogName)
    {
        migrationBuilder.Sql(
            @$"IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = '{catalogName}')
                BEGIN
                    CREATE FULLTEXT CATALOG [{catalogName}]
                END",
            true);
    }

    public static void DropFullTextCatalogIfExists(MigrationBuilder migrationBuilder, string catalogName)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE [name] = '{catalogName}')
                BEGIN
                    DROP FULLTEXT CATALOG {catalogName}
                END",
            true);
    }

    public static void DropIndexIfExists(MigrationBuilder migrationBuilder, string indexName, string tableName)
    {
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = '{indexName}' AND object_id = OBJECT_ID('dbo.{tableName}'))
                DROP INDEX {indexName} ON {tableName}; ",
            true);
    }

    public static void DropConstraintIfExists(MigrationBuilder migrationBuilder, string constraintName, string tableName)
    {
        // Drop foreign key constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END");

        // Drop check constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.check_constraints
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END");

        // Drop default constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.default_constraints
                WHERE name = '{constraintName}' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END");

        // Drop unique constraint if it exists
        migrationBuilder.Sql(
            @$"IF EXISTS (
                SELECT 1 FROM sys.objects
                WHERE name = '{constraintName}' AND type = 'UQ' AND parent_object_id = OBJECT_ID('dbo.{tableName}'))
            BEGIN
                ALTER TABLE [{tableName}] DROP CONSTRAINT [{constraintName}];
            END");
    }

    public static void CreateIndex(MigrationBuilder migrationBuilder, string tableName, params string[] cols)
    {
        migrationBuilder.Sql(
            @$"CREATE NONCLUSTERED INDEX [IX_{tableName}_{cols.JoinToString("_")}] ON [dbo].[{tableName}] ({cols.Select(col => $"[{col}] ASC").JoinToString(",")})",
            true);
    }

    public static void CreateIndexIfNotExists(MigrationBuilder migrationBuilder, string tableName, params string[] cols)
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

        migrationBuilder.Sql(createIndexSql, true);
    }

    public static void CreateUniqueIndex(MigrationBuilder migrationBuilder, string tableName, params string[] cols)
    {
        migrationBuilder.Sql(
            @$"CREATE UNIQUE NONCLUSTERED INDEX [IX_{tableName}_{cols.JoinToString("_")}] ON [dbo].[{tableName}] ({cols.Select(col => $"[{col}] ASC").JoinToString(",")})",
            true);
    }

    public static void CreateUniqueIndexIfNotExists(MigrationBuilder migrationBuilder, string tableName, params string[] cols)
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

        migrationBuilder.Sql(createUniqueIndexSql, true);
    }
}
