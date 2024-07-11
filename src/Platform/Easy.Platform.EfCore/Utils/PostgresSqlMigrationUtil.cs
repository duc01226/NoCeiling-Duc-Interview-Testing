using Microsoft.EntityFrameworkCore.Migrations;

namespace Easy.Platform.EfCore.Utils;

public static class PostgresSqlMigrationUtil
{
    public static void DropConstraintIfExists(MigrationBuilder migrationBuilder, string name, string table)
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
            END $$;");
    }
}
