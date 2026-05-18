using FluentMigrator;

namespace Migrator.Migrations;

[Migration(202605180001)]
public sealed class InitialCreateInvitesTable : Migration
{
    public override void Up()
    {
        Create.Table("invites")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("invite_id").AsGuid().NotNullable().Unique()
            .WithColumn("protected_server_secret").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("pin_hash").AsBinary(32).NotNullable()
            .WithColumn("revoke_token_hash").AsBinary(32).NotNullable()
            .WithColumn("created_at_utc").AsDateTimeOffset().NotNullable()
            .WithColumn("expires_at_utc").AsDateTimeOffset().NotNullable()
            .WithColumn("is_used").AsBoolean().NotNullable()
            .WithColumn("attempts").AsInt32().NotNullable()
            .WithColumn("locked_until_utc").AsDateTimeOffset().Nullable()
            .WithColumn("is_revoked").AsBoolean().NotNullable();

        Create.Index("ix_invites_expires_at_utc")
            .OnTable("invites")
            .OnColumn("expires_at_utc").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_invites_expires_at_utc").OnTable("invites");
        Delete.Table("invites");
    }
}
