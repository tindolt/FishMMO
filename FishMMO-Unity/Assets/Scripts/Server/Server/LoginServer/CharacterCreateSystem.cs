﻿using FishNet.Connection;
using FishNet.Transporting;
using System;
using FishMMO.Database.Npgsql.Entities;
using FishMMO.Server.DatabaseServices;
using FishMMO.Shared;
using FishNet.Object;

namespace FishMMO.Server
{
	/// <summary>
	/// Server Character Creation system.
	/// </summary>
	public class CharacterCreateSystem : ServerBehaviour
	{
		public int MaxCharacters = 8;

		public WorldSceneDetailsCache WorldSceneDetailsCache;

		public override void InitializeOnce()
		{
			if (ServerManager != null)
			{
				ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
			}
			else
			{
				enabled = false;
			}
		}

		private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
		{
			if (obj.ConnectionState == LocalConnectionState.Started)
			{
				ServerManager.RegisterBroadcast<CharacterCreateBroadcast>(OnServerCharacterCreateBroadcastReceived, true);
				
			}
			else if (obj.ConnectionState == LocalConnectionState.Stopped)
			{
				ServerManager.UnregisterBroadcast<CharacterCreateBroadcast>(OnServerCharacterCreateBroadcastReceived);
			}
		}

		private void OnServerCharacterCreateBroadcastReceived(NetworkConnection conn, CharacterCreateBroadcast msg, Channel channel)
		{
			if (conn.IsActive)
			{
				// validate character creation data
				if (!Constants.Authentication.IsAllowedCharacterName(msg.characterName))
				{
					// invalid character name
					Server.Broadcast(conn, new CharacterCreateResultBroadcast()
					{
						result = CharacterCreateResult.InvalidCharacterName,
					}, true, Channel.Reliable);
					return;
				}

				using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
				if (!AccountManager.GetAccountNameByConnection(conn, out string accountName))
				{
					// account not found??
					conn.Kick(FishNet.Managing.Server.KickReason.UnusualActivity);
					return;
				}
				int characterCount = CharacterService.GetCount(dbContext, accountName);
				if (characterCount >= MaxCharacters)
				{
					// too many characters
					Server.Broadcast(conn, new CharacterCreateResultBroadcast()
					{
						result = CharacterCreateResult.TooMany,
					}, true, Channel.Reliable);
					return;
				}
				var character = CharacterService.GetByName(dbContext, msg.characterName);
				if (character != null)
				{
					// character name already taken
					Server.Broadcast(conn, new CharacterCreateResultBroadcast()
					{
						result = CharacterCreateResult.CharacterNameTaken,
					}, true, Channel.Reliable);
					return;
				}

				if (WorldSceneDetailsCache == null ||
					WorldSceneDetailsCache.Scenes == null ||
					WorldSceneDetailsCache.Scenes.Count < 1)
				{
					// failed to find spawn positions to validate with
					Server.Broadcast(conn, new CharacterCreateResultBroadcast()
					{
						result = CharacterCreateResult.InvalidSpawn,
					}, true, Channel.Reliable);
					return;
				}
				// validate spawn details
				if (WorldSceneDetailsCache.Scenes.TryGetValue(msg.sceneName, out WorldSceneDetails details))
				{
					// validate spawner
					if (details.InitialSpawnPositions.TryGetValue(msg.spawnerName, out CharacterInitialSpawnPositionDetails initialSpawnPosition))
					{
						// validate race
						RaceTemplate raceTemplate = RaceTemplate.Get<RaceTemplate>(msg.raceTemplateID);
						if (raceTemplate == null ||
							raceTemplate.Prefab == null)
						{
							conn.Kick(FishNet.Managing.Server.KickReason.UnusualActivity);
							return;
						}
						bool validateAllowedRace = false;
						foreach (RaceTemplate t in initialSpawnPosition.AllowedRaces)
						{
							if (t.Name == raceTemplate.Name)
							{
								validateAllowedRace = true;
								break;
							}
						}
						if (!validateAllowedRace)
						{
							conn.Kick(FishNet.Managing.Server.KickReason.UnusualActivity);
							return;
						}

						// validate spawnable prefab
						Character characterPrefab = raceTemplate.Prefab.GetComponent<Character>();
						if (characterPrefab == null ||
							Server.NetworkManager.SpawnablePrefabs.GetObject(true, characterPrefab.NetworkObject.PrefabId) == null)
						{
							conn.Kick(FishNet.Managing.Server.KickReason.UnusualActivity);
							return;
						}

						var newCharacter = new CharacterEntity()
						{
							Account = accountName,
							Name = msg.characterName,
							NameLowercase = msg.characterName?.ToLower(),
							RaceID = msg.raceTemplateID,
							SceneName = initialSpawnPosition.SceneName,
							X = initialSpawnPosition.Position.x,
							Y = initialSpawnPosition.Position.y,
							Z = initialSpawnPosition.Position.z,
							RotX = initialSpawnPosition.Rotation.x,
							RotY = initialSpawnPosition.Rotation.y,
							RotZ = initialSpawnPosition.Rotation.z,
							RotW = initialSpawnPosition.Rotation.w,
							AccessLevel = (byte)AccessLevel.Player,
							TimeCreated = DateTime.UtcNow,
						};
						dbContext.Characters.Add(newCharacter);
						dbContext.SaveChanges();

						// send success to the client
						Server.Broadcast(conn, new CharacterCreateResultBroadcast()
						{
							result = CharacterCreateResult.Success,
						}, true, Channel.Reliable);

						// send the create broadcast back to the client
						Server.Broadcast(conn, msg, true, Channel.Reliable);
					}
				}
			}
		}
	}
}