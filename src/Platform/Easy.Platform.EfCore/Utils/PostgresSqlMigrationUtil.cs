using Microsoft.EntityFrameworkCore.Migrations;

namespace Easy.Platform.EfCore.Utils;

public static class PostgresSqlMigrationUtil
{
    public static void DropConstraintIfExists(MigrationBuilder migrationBuilder, string name, string table, bool suppressTransaction = false)
    {
        migrationBuilder.Sql(
            $@"DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = '{name}' AND table_name = '{table}'
                ) THEN
                    EXECUTE 'ALTER TABLE ""{table}"" DROP CONSTRAINT ""{name}""';
                END IF;
            END $$;",
            suppressTransaction);
    }

    public static void DropIndexIfExists(MigrationBuilder migrationBuilder, string indexName, string table, bool suppressTransaction = false)
    {
        migrationBuilder.Sql(
            $@"DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE indexname = '{indexName}' AND tablename = '{table}'
                ) THEN
                    EXECUTE 'DROP INDEX IF EXISTS ""{indexName}""';
                END IF;
            END $$;",
            suppressTransaction);
    }
}
