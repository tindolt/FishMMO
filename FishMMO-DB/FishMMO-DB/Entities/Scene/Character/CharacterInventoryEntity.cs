﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FishMMO_DB.Entities
{
    [Table("character_inventory", Schema = "fish_mmo_postgresql")]
    [Index(nameof(CharacterID))]
    [Index(nameof(CharacterID), nameof(Slot), IsUnique = true)]
    public class CharacterInventoryEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }
        public long CharacterID { get; set; }
        public CharacterEntity Character { get; set; }
        public long InstanceID { get; set; }
        public int TemplateID { get; set; }
        public int Seed { get; set; }
        public int Slot { get; set; }
        public string Name { get; set; }
        public int Amount { get; set; }
    }
}