﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Server.Entities;

namespace Server.EntityConfigurations
{
    public class CharacterEntityConfiguration : IEntityTypeConfiguration<CharacterEntity>
    {
        public void Configure(EntityTypeBuilder<CharacterEntity> builder)
        {
            builder.Property(e => e.Name)
                .IsRequired();
            
            builder.Property(e => e.NameLowercase)
                .HasComputedColumnSql("LOWER(\"Name\")", stored: true);
            
            builder.HasIndex(e => e.NameLowercase)
                .IsUnique()
                .HasName("IX_CharacterEntity_NameLowercase");

            /*builder.HasIndex($"LOWER(\"{nameof(CharacterEntity.name)}\")")
                .IsUnique()
                .HasName("idx_character_name_case_insensitive");
            
            builder.HasAnnotation("Relational:SqlCreateIndexStatement", 
                "CREATE UNIQUE INDEX idx_character_name_case_insensitive ON \"MyEntities\" (LOWER(\"name\")) COLLATE \"en_US.utf8\"");*/
        }
    }
}