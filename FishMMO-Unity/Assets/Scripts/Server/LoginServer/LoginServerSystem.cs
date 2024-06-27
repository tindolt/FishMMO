﻿using FishNet.Managing.Server;
using FishNet.Transporting;
using FishMMO.Server.DatabaseServices;
using UnityEngine;
using FishMMO.Shared;

namespace FishMMO.Server
{
	// Login Server System handles the database heartbeat for Login Service
	public class LoginServerSystem : ServerBehaviour
	{
		private LocalConnectionState serverState;

		private long id;
		private float pulseRate = 5.0f;
		private float nextPulse = 0.0f;

		public long ID { get { return id; } }

		public override void InitializeOnce()
		{
			using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
			if (dbContext == null)
			{
				throw new UnityException("Failed to get dbContext.");
			}

			if (ServerManager != null)
			{
				ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;

				if (Server != null &&
					Server.TryGetServerIPAddress(out ServerAddress server))
				{
					Debug.Log("Login Server System: Adding Login Server to Database: " + name + ":" + server.address + ":" + server.port);
					LoginServerService.Add(dbContext, server.address, server.port, out id);
				}
			}
			else
			{
				enabled = false;
			}
		}

		public override void Destroying()
		{
			using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
			if (dbContext == null)
			{
				throw new UnityException("Failed to get dbContext.");
			}

			if (ServerManager != null)
			{
				if (Server != null)
				{
					Debug.Log("Login Server System: Removing Login Server from Database: " + id);
					LoginServerService.Delete(dbContext, id);
				}
			}
		}

		private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
		{
			serverState = args.ConnectionState;
		}

		void LateUpdate()
		{
			if (serverState == LocalConnectionState.Started)
			{
				if (nextPulse < 0)
				{
					nextPulse = pulseRate;

					using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();

					//Debug.Log("Login Server System: Pulse");
					LoginServerService.Pulse(dbContext, id);
				}
				nextPulse -= Time.deltaTime;
			}
		}
	}
}