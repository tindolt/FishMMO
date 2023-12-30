﻿using System.Collections.Generic;
using System.Linq;
using FishMMO.Database.Npgsql;
using FishMMO.Database.Npgsql.Entities;
using FishMMO.Shared;

namespace FishMMO.Server.DatabaseServices
{
	public class CharacterAbilityService
	{
		/// <summary>
		/// Adds a known ability for a character to the database using the Ability Template ID.
		/// </summary>
		public static void UpdateOrAdd(NpgsqlDbContext dbContext, long characterID, Ability ability)
		{
			if (ability == null)
			{
				return;
			}

			var dbAbility = dbContext.CharacterAbilities.FirstOrDefault(c => c.CharacterID == characterID && c.ID == ability.ID);
			// update or add to known abilities
			if (dbAbility != null)
			{
				dbAbility.CharacterID = characterID;
				dbAbility.TemplateID = ability.Template.ID;
				dbAbility.AbilityEvents = ability.AbilityEvents.Keys.ToList();
			}
			else
			{
				dbAbility = new CharacterAbilityEntity()
				{
					CharacterID = characterID,
					TemplateID = ability.Template.ID,
					AbilityEvents = ability.AbilityEvents.Keys.ToList(),
				};
				dbContext.CharacterAbilities.Add(dbAbility);
				dbContext.SaveChanges();
				ability.ID = dbAbility.ID;
			}
		}

		/// <summary>
		/// Save a characters abilities to the database.
		/// </summary>
		public static void Save(NpgsqlDbContext dbContext, Character character)
		{
			if (character == null)
			{
				return;
			}

			var dbAbilities = dbContext.CharacterAbilities.Where(c => c.CharacterID == character.ID)
																	.ToDictionary(k => k.ID);

			foreach (KeyValuePair<long, Ability> pair in character.AbilityController.KnownAbilities)
			{
				if (pair.Key < 0)
				{
					continue;
				}
				if (dbAbilities.TryGetValue(pair.Key, out CharacterAbilityEntity ability))
				{
					ability.TemplateID = pair.Value.Template.ID;
					ability.AbilityEvents.Clear();
					ability.AbilityEvents = pair.Value.AbilityEvents.Keys.ToList();
				}
				else
				{
					dbContext.CharacterAbilities.Add(new CharacterAbilityEntity()
					{
						CharacterID = character.ID,
						TemplateID = pair.Value.Template.ID,
						AbilityEvents = pair.Value.AbilityEvents.Keys.ToList(),
					});
				}
			}
		}

		/// <summary>
		/// KeepData is automatically false... This means we delete the ability entry. TODO Deleted field is simply set to true just incase we need to reinstate a character..
		/// </summary>
		public static void Delete(NpgsqlDbContext dbContext, long characterID, bool keepData = false)
		{
			if (!keepData)
			{
				var dbAbilities = dbContext.CharacterAbilities.Where(c => c.CharacterID == characterID);
				if (dbAbilities != null)
				{
					dbContext.CharacterAbilities.RemoveRange(dbAbilities);
				}
			}
		}

		/// <summary>
		/// KeepData is automatically false... This means we delete the ability entry. TODO Deleted field is simply set to true just incase we need to reinstate a character..
		/// </summary>
		public static void Delete(NpgsqlDbContext dbContext, long characterID, long id, bool keepData = false)
		{
			if (!keepData)
			{
				var dbAbility = dbContext.CharacterAbilities.FirstOrDefault(c => c.CharacterID == characterID && c.ID == id);
				if (dbAbility != null)
				{
					dbContext.CharacterAbilities.Remove(dbAbility);
				}
			}
		}

		/// <summary>
		/// Load a characters known abilities from the database.
		/// </summary>
		public static void Load(NpgsqlDbContext dbContext, Character character)
		{
			var dbAbilities = dbContext.CharacterAbilities.Where(c => c.CharacterID == character.ID);

			foreach (CharacterAbilityEntity dbAbility in dbAbilities)
			{
				AbilityTemplate template = AbilityTemplate.Get<AbilityTemplate>(dbAbility.TemplateID);
				if (template != null)
				{
					character.AbilityController.LearnAbility(new Ability(dbAbility.ID, template, dbAbility.AbilityEvents));
				}
			};
		}
	}
}