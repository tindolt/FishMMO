﻿using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.IO;
using StackExchange.Redis;
using FishMMO.Database.Npgsql;
using FishMMO.Database.Redis;
using FishMMO.Shared;
using Configuration = FishMMO.Shared.Configuration;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishMMO.Server
{
	// Main Server class, handles configuration and starting connections.
	public class Server : MonoBehaviour
	{
		public Configuration Configuration { get; private set; }
		public NpgsqlDbContextFactory NpgsqlDbContextFactory { get; private set; }
		public RedisDbContextFactory RedisDbContextFactory { get; private set; }
		public NetworkManager NetworkManager { get; private set; }
		public string RemoteAddress { get; private set; }
		public string Address { get; private set; }
		public ushort Port { get; private set; }

		#region LOGIN
		public AccountCreationSystem AccountCreationSystem { get; private set; }
		public CharacterSelectSystem CharacterSelectSystem { get; private set; }
		public CharacterCreateSystem CharacterCreateSystem { get; private set; }
		public ServerSelectSystem ServerSelectSystem { get; private set; }
		#endregion

		#region WORLD
		public WorldServerSystem WorldServerSystem { get; private set; }
		public WorldSceneSystem WorldSceneSystem { get; private set; }
		#endregion

		#region SCENE
		public SceneServerSystem SceneServerSystem { get; private set; }
		public CharacterSystem CharacterSystem { get; private set; }
		public CharacterInventorySystem CharacterInventorySystem { get; private set; }
		public ChatSystem ChatSystem { get; private set; }
		public GuildSystem GuildSystem { get; private set; }
		public PartySystem PartySystem { get; private set; }
		public FriendSystem FriendSystem { get; private set; }
		public NamingSystem NamingSystem { get; private set; }
		public InteractableSystem InteractableSystem { get; private set; }
		#endregion

		public ServerWindowTitleUpdater ServerWindowTitleUpdater { get; private set; }

		public bool LogToDisk = false;
		private string logFilePath;
		private DateTime startTime;

		private LocalConnectionState serverState = LocalConnectionState.Stopped;
		private string serverTypeName;

		void Awake()
		{
			startTime = DateTime.UtcNow;

			// get the server type so we know how to configure
			string serverType = GetServerType();
			if (serverType.Equals("Invalid"))
			{
				Server.Quit();
			}

			if (LogToDisk)
			{
				logFilePath = Path.Combine(GetWorkingDirectory(), "Logs", serverType + "_DebugLog_" + startTime.ToString("yyyy-MM-dd") + ".txt");

				Application.logMessageReceived += this.Application_logMessageReceived;
			}

			Debug.Log("Server: " + serverType + " is starting[" + DateTime.UtcNow + "]");

			string workingDirectory = Server.GetWorkingDirectory();
			Debug.Log("Server: Current working directory[" + workingDirectory + "]");

			// load configuration
			Configuration = new Configuration(workingDirectory);
			if (!Configuration.Load(serverTypeName + Configuration.EXTENSION))
			{
				// if we failed to load the file.. save a new one
				Configuration.Set("ServerName", "TestName");
				Configuration.Set("MaximumClients", 4000);
				Configuration.Set("Address", "0.0.0.0");
				Configuration.Set("Port", 7770);
#if !UNITY_EDITOR
				Configuration.Save();
#endif
			}

			// initialize the DB contexts
#if UNITY_EDITOR
			string dbConfigurationPath = Path.Combine(Path.Combine(workingDirectory, Constants.Configuration.SetupDirectory), "Development");

			NpgsqlDbContextFactory = new NpgsqlDbContextFactory(dbConfigurationPath, false);
			RedisDbContextFactory = new RedisDbContextFactory(dbConfigurationPath);
#else
			NpgsqlDbContextFactory = new NpgsqlDbContextFactory(workingDirectory, false);
			RedisDbContextFactory = new RedisDbContextFactory(workingDirectory);
#endif
			// ensure our NetworkManager exists in the scene
			if (NetworkManager == null)
			{
				NetworkManager = FindObjectOfType<NetworkManager>();

				if (NetworkManager == null)
				{
					throw new UnityException("Server: NetworkManager could not be found! Make sure you have a NetworkManager in your scene.");
				}
			}

			// initialize required components for our specified server type
			InternalInitializeOnce(serverType);

			// automatically start the server
			if (NetworkManager.ServerManager != null && LoadTransportServerDetails())
			{
				NetworkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;

				// start the local server connection
				NetworkManager.ServerManager.StartConnection();
				

				StartCoroutine(OnAwaitingConnectionReady());
			}
			else
			{
				Server.Quit();
			}

			Debug.Log("Server: " + serverType + " is running[" + DateTime.UtcNow + "]");
		}

		private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
		{
			try
			{
				// Ensure the directory exists
				string logDirectory = Path.GetDirectoryName(logFilePath);
				if (!Directory.Exists(logDirectory))
				{
					Directory.CreateDirectory(logDirectory);
				}

				// Append the log to the file
				File.AppendAllText(logFilePath, $"{type}: {condition}\r\n{stackTrace}\r\n");
			}
			catch (Exception e)
			{
				Debug.LogError($"Failed to write to log file: {e.Message}");
			}
		}

		public static string GetWorkingDirectory()
		{
#if UNITY_EDITOR
			return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
#else
			return AppDomain.CurrentDomain.BaseDirectory;
#endif
		}

		public void OnDestroy()
		{
			if (LogToDisk)
			{
				Application.logMessageReceived -= this.Application_logMessageReceived;
			}

			RedisDbContextFactory.CloseRedis();
		}

		public static void Quit()
		{
#if UNITY_EDITOR
			EditorApplication.ExitPlaymode();
#else
			Application.Quit();
#endif
		}

		private string GetServerType()
		{
			Scene scene = gameObject.scene;
			if (!scene.path.Contains("Bootstraps"))
			{
				throw new UnityException("Server: Active scene is not in the bootstraps folder.");
			}
			serverTypeName = scene.name;
			string upper = serverTypeName.ToUpper();
			if (upper.StartsWith("LOGIN"))
			{
				return "LOGIN";
			}
			if (upper.StartsWith("WORLD"))
			{
				return "WORLD";
			}
			if (upper.StartsWith("SCENE"))
			{
				return "SCENE";
			}
			return "Invalid";
		}

		/// <summary>
		/// Order of Execution, Dependency Injection, and all other server initialization should be handled here.
		/// </summary>
		internal void InternalInitializeOnce(string serverType)
		{
			Debug.Log("Server: Initializing Components");

			Debug.Log("Server: Fetching Remote IP Address.");
			RemoteAddress = NetHelper.GetExternalIPAddress().ToString();
			if (string.IsNullOrWhiteSpace(RemoteAddress))
			{
				throw new UnityException("Server: Failed to retrieve Remote IP Address");
			}

			// only use title updater if it has been added to the scene
			ServerWindowTitleUpdater = GetComponent<ServerWindowTitleUpdater>();
			if (ServerWindowTitleUpdater != null)
			{
				ServerWindowTitleUpdater.InternalInitializeOnce(this, NetworkManager.ServerManager);
			}

			// database factory DI
			LoginServerAuthenticator authenticator = NetworkManager.ServerManager.GetAuthenticator() as LoginServerAuthenticator;
			if (authenticator != null)
			{
				authenticator.NpgsqlDbContextFactory = NpgsqlDbContextFactory;
			}

			switch (serverType)
			{
				case "LOGIN":
					AccountCreationSystem = GetOrCreateComponent<AccountCreationSystem>();
					AccountCreationSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					CharacterSelectSystem = GetOrCreateComponent<CharacterSelectSystem>();
					CharacterSelectSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					CharacterCreateSystem = GetOrCreateComponent<CharacterCreateSystem>();
					CharacterCreateSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					ServerSelectSystem = GetOrCreateComponent<ServerSelectSystem>();
					ServerSelectSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);
					break;
				case "WORLD":
					WorldServerSystem = GetOrCreateComponent<WorldServerSystem>();
					WorldServerSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					WorldSceneSystem = GetOrCreateComponent<WorldSceneSystem>();
					WorldSceneSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);
					WorldServerAuthenticator worldAuthenticator = NetworkManager.ServerManager.GetAuthenticator() as WorldServerAuthenticator;
					if (worldAuthenticator != null)
					{
						worldAuthenticator.WorldSceneSystem = WorldSceneSystem;
					}

					// world server has special title bar that handles relay information
					if (ServerWindowTitleUpdater != null)
					{
						ServerWindowTitleUpdater.WorldSceneSystem = WorldSceneSystem;
					}
					break;
				case "SCENE":
					SceneServerSystem = GetOrCreateComponent<SceneServerSystem>();
					SceneServerSystem.SceneManager = NetworkManager.SceneManager;
					SceneServerSystem.InternalInitializeOnce(this, NetworkManager.ServerManager, NetworkManager.ClientManager);

					CharacterSystem = GetOrCreateComponent<CharacterSystem>();
					CharacterSystem.InternalInitializeOnce(this, NetworkManager.ServerManager, NetworkManager.ClientManager);

					CharacterInventorySystem = GetOrCreateComponent<CharacterInventorySystem>();
					CharacterInventorySystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					ChatSystem = GetOrCreateComponent<ChatSystem>();
					ChatSystem.SceneManager = NetworkManager.SceneManager;
					ChatSystem.InternalInitializeOnce(this, NetworkManager.ServerManager, NetworkManager.ClientManager);

					GuildSystem = GetOrCreateComponent<GuildSystem>();
					GuildSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					PartySystem = GetOrCreateComponent<PartySystem>();
					PartySystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					FriendSystem = GetOrCreateComponent<FriendSystem>();
					FriendSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					NamingSystem = GetOrCreateComponent<NamingSystem>();
					NamingSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);

					InteractableSystem = GetOrCreateComponent<InteractableSystem>();
					InteractableSystem.InternalInitializeOnce(this, NetworkManager.ServerManager);
					break;
				default:
					Server.Quit();
					return;
			}
		}

		/// <summary>
		/// Gets a component, creating and adding it if it does not exist.
		/// </summary>
		private T GetOrCreateComponent<T>() where T : UnityEngine.Component
		{
			if (gameObject.TryGetComponent<T>(out T result))
				return result;
			else
				return gameObject.AddComponent<T>();
		}

		private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
		{
			serverState = obj.ConnectionState;

			Transport transport = NetworkManager.TransportManager.Transport;
			if (transport != null)
			{
				Debug.Log("Server: " + serverTypeName +
						  " Local:" + transport.GetServerBindAddress(IPAddressType.IPv4) + ":" + transport.GetPort() +
						  " Remote:" + RemoteAddress + ":" + transport.GetPort() + 
						  " - " + serverState);
			}
		}

		IEnumerator OnAwaitingConnectionReady()
		{
			// wait for the connection to the current server to start before we connect the client
			while (serverState != LocalConnectionState.Started)
			{
				yield return new WaitForSeconds(.5f);
			}

			yield return null;
		}

		/// <summary>
		/// Loads transport server details from the configuration file.
		/// </summary>
		private bool LoadTransportServerDetails()
		{
			Transport transport = NetworkManager.TransportManager.Transport;
			if (transport != null &&
				Configuration.TryGetString("Address", out string address) &&
				Configuration.TryGetUShort("Port", out ushort port) &&
				Configuration.TryGetInt("MaximumClients", out int maximumClients))
			{
				Address = address;
				Port = port;

				transport.SetServerBindAddress(Address, IPAddressType.IPv4);
				transport.SetPort(Port);
				transport.SetMaximumClients(maximumClients);
				return true;
			}
			return false;
		}

		public bool TryGetServerIPv4AddressFromTransport(out ServerAddress address)
		{
			Transport transport = NetworkManager.TransportManager.Transport;
			if (transport != null)
			{
				address = new ServerAddress()
				{
					address = transport.GetServerBindAddress(IPAddressType.IPv4),
					port = transport.GetPort(),
				};
				return true;
			}
			address = default;
			return false;
		}

		public bool TryGetServerIPv6AddressFromTransport(out ServerAddress address)
		{
			Transport transport = NetworkManager.TransportManager.Transport;
			if (transport != null)
			{
				address = new ServerAddress()
				{
					address = transport.GetServerBindAddress(IPAddressType.IPv6),
					port = transport.GetPort(),
				};
				return true;
			}
			address = default;
			return false;
		}

		public bool TryGetServerIPAddress(out ServerAddress address)
		{
			const string LoopBack = "127.0.0.1";
			const string LocalHost = "localhost";

			Transport transport = NetworkManager.TransportManager.Transport;
			if (transport != null)
			{
				// if our assigned address is localhost, use localhost
				// otherwise try external address
				// if remote address is null we fall back to localhost
				string actualAddress = !string.IsNullOrWhiteSpace(Address) && (Address.Equals(LoopBack) | Address.Equals(LocalHost)) ? Address :
										!string.IsNullOrWhiteSpace(RemoteAddress) ? RemoteAddress : LoopBack;

				address = new ServerAddress()
				{
					address = actualAddress,
					port = transport.GetPort(),
				};
				return true;
			}
			address = default;
			return false;
		}
	}
}