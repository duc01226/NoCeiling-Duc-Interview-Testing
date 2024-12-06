﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlatformExampleApp.TextSnippet.Persistence;

#nullable disable

namespace PlatformExampleApp.TextSnippet.Persistence.Migrations
{
    [DbContext(typeof(TextSnippetDbContext))]
    partial class TextSnippetDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("Easy.Platform.Application.MessageBus.InboxPattern.PlatformInboxBusMessage", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(400)
                        .HasColumnType("nvarchar(400)");

                    b.Property<string>("ConcurrencyUpdateToken")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ConsumeStatus")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConsumerBy")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("ForApplicationName")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("JsonMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("LastConsumeDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("LastConsumeError")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("LastProcessingPingDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("MessageTypeFullName")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTime?>("NextRetryProcessAfter")
                        .HasColumnType("datetime2");

                    b.Property<string>("ProduceFrom")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("RetriedProcessCount")
                        .HasColumnType("int");

                    b.Property<string>("RoutingKey")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("ConsumeStatus", "CreatedDate");

                    b.HasIndex("ConsumeStatus", "LastProcessingPingDate", "ForApplicationName", "CreatedDate");

                    b.HasIndex("ConsumeStatus", "NextRetryProcessAfter", "ForApplicationName", "CreatedDate");

                    b.ToTable("PlatformInboxEventBusMessage", (string)null);
                });

            modelBuilder.Entity("Easy.Platform.Application.MessageBus.OutboxPattern.PlatformOutboxBusMessage", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(400)
                        .HasColumnType("nvarchar(400)");

                    b.Property<string>("ConcurrencyUpdateToken")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("JsonMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("LastProcessingPingDate")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("LastSendDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("LastSendError")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("MessageTypeFullName")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTime?>("NextRetryProcessAfter")
                        .HasColumnType("datetime2");

                    b.Property<int?>("RetriedProcessCount")
                        .HasColumnType("int");

                    b.Property<string>("RoutingKey")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("SendStatus")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Id");

                    b.HasIndex("SendStatus", "CreatedDate");

                    b.HasIndex("SendStatus", "LastProcessingPingDate", "CreatedDate");

                    b.HasIndex("SendStatus", "NextRetryProcessAfter", "CreatedDate");

                    b.ToTable("PlatformOutboxEventBusMessage", (string)null);
                });

            modelBuilder.Entity("Easy.Platform.Persistence.DataMigration.PlatformDataMigrationHistory", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ConcurrencyUpdateToken")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("LastProcessError")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("LastProcessingPingTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .HasColumnType("nvarchar(450)");

                    b.HasKey("Name");

                    b.HasIndex("Status");

                    b.ToTable("ApplicationDataMigrationHistoryDbSet", (string)null);
                });

            modelBuilder.Entity("PlatformExampleApp.TextSnippet.Domain.Entities.TextSnippetEntity", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("AddressStrings")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Addresses")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ConcurrencyUpdateToken")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("CreatedBy")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("CreatedByUserId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("CreatedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("FullText")
                        .IsRequired()
                        .HasMaxLength(4000)
                        .HasColumnType("nvarchar(4000)");

                    b.Property<string>("LastUpdatedBy")
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime?>("LastUpdatedDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("SnippetText")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("TimeOnly")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("CreatedBy");

                    b.HasIndex("CreatedDate");

                    b.HasIndex("LastUpdatedBy");

                    b.HasIndex("LastUpdatedDate");

                    b.HasIndex("SnippetText")
                        .IsUnique();

                    b.ToTable("TextSnippetEntity");
                });

            modelBuilder.Entity("PlatformExampleApp.TextSnippet.Domain.Entities.TextSnippetEntity", b =>
                {
                    b.OwnsOne("PlatformExampleApp.TextSnippet.Domain.ValueObjects.ExampleAddressValueObject", "Address", b1 =>
                        {
                            b1.Property<string>("TextSnippetEntityId")
                                .HasColumnType("nvarchar(450)");

                            b1.Property<string>("Number")
                                .HasColumnType("nvarchar(max)");

                            b1.Property<string>("Street")
                                .HasColumnType("nvarchar(max)");

                            b1.HasKey("TextSnippetEntityId");

                            b1.ToTable("TextSnippetEntity");

                            b1.WithOwner()
                                .HasForeignKey("TextSnippetEntityId");
                        });

                    b.Navigation("Address");
                });
#pragma warning restore 612, 618
        }
    }
}
