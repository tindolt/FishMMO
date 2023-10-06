﻿using System;
using System.Collections.Generic;
using System.Linq;
using FishMMO_DB;
using FishMMO_DB.Entities;

namespace FishMMO.Server.Services
{
	public class PartyUpdateService
	{
		public static void Save(ServerDbContext dbContext, long partyID)
		{
			dbContext.PartyUpdates.Add(new PartyUpdateEntity()
			{
				PartyID = partyID,
				TimeCreated = DateTime.Now,
			});
		}

		public static void Delete(ServerDbContext dbContext, long partyID)
		{
		}

		public static List<PartyUpdateEntity> Fetch(ServerDbContext dbContext, DateTime lastFetch, long lastPosition, int amount)
		{
			var nextPage = dbContext.PartyUpdates
				.OrderBy(b => b.TimeCreated)
				.ThenBy(b => b.ID)
				.Where(b => b.TimeCreated >= lastFetch &&
							b.ID > lastPosition)
				.Take(amount)
				.ToList();
			return nextPage;
		}
	}
}