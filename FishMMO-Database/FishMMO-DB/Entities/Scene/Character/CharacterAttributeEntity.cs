﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FishMMO.Database.Entities
{
	[Table("character_attributes", Schema = "fish_mmo_postgresql")]
	[Index(nameof(CharacterID))]
	public class CharacterAttributeEntity
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long ID { get; set; }
		public long CharacterID { get; set; }
		public CharacterEntity Character { get; set; }
		public int TemplateID { get; set; }
		public int BaseValue { get; set; }
		public int Modifier { get; set; }
		public int CurrentValue { get; set; }
	}
}