﻿#if !UNITY_SERVER
using TMPro;
#endif
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using KinematicCharacterController;
using UnityEngine;
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Serializing;

namespace FishMMO.Shared
{
	[RequireComponent(typeof(CharacterAttributeController))]
	[RequireComponent(typeof(TargetController))]
	[RequireComponent(typeof(CooldownController))]
	[RequireComponent(typeof(InventoryController))]
	[RequireComponent(typeof(EquipmentController))]
	[RequireComponent(typeof(BankController))]
	[RequireComponent(typeof(AbilityController))]
	[RequireComponent(typeof(AchievementController))]
	[RequireComponent(typeof(BuffController))]
	[RequireComponent(typeof(QuestController))]
	[RequireComponent(typeof(CharacterDamageController))]
	[RequireComponent(typeof(GuildController))]
	[RequireComponent(typeof(PartyController))]
	[RequireComponent(typeof(FriendController))]
	[RequireComponent(typeof(FactionController))]
	public class Character : NetworkBehaviour, IPooledResettable
	{
		private Dictionary<Type, ICharacterBehaviour> behaviours = new Dictionary<Type, ICharacterBehaviour>();

		public Transform Transform { get; private set; }

		#region KCC
		public KinematicCharacterMotor Motor { get; private set; }
		public KCCController CharacterController { get; private set; }
		public KCCPlayer KCCPlayer { get; private set; }
		#endregion

#if !UNITY_SERVER
		public TextMeshPro CharacterNameLabel;
		public TextMeshPro CharacterGuildLabel;
		public Camera EquipmentViewCamera;
#endif

		public static Action<Character> OnReadPayload;
		public static Action<Character> OnStartLocalClient;
		public static Action<Character> OnStopLocalClient;

		// accountID for reference
		public long ID;

		/// <summary>
		/// The characters real name. Use this if you are referencing a character by name. Avoid character.name unless you want the name of the game object.
		/// </summary>
		public string CharacterName;
		public string CharacterNameLower;
		public string Account;
		public long WorldServerID;
		public AccessLevel AccessLevel = AccessLevel.Player;
		public string TeleporterName = "";
		public bool IsTeleporting { get { return !string.IsNullOrWhiteSpace(TeleporterName); } }
		public readonly SyncVar<long> Currency = new SyncVar<long>(new SyncTypeSettings()
		{
			SendRate = 0.0f,
			Channel = Channel.Unreliable,
			ReadPermission = ReadPermission.OwnerOnly,
			WritePermission = WritePermission.ServerOnly,
		});
		/// <summary>
		/// The prefab ID for the character object.
		/// </summary>
		public readonly SyncVar<int> RaceID = new SyncVar<int>(new SyncTypeSettings()
		{
			SendRate = 0.0f,
			Channel = Channel.Unreliable,
			ReadPermission = ReadPermission.OwnerOnly,
			WritePermission = WritePermission.ServerOnly,
		});
		public readonly SyncVar<string> RaceName = new SyncVar<string>(new SyncTypeSettings()
		{
			SendRate = 0.0f,
			Channel = Channel.Unreliable,
			ReadPermission = ReadPermission.OwnerOnly,
			WritePermission = WritePermission.ServerOnly,
		});
		public readonly SyncVar<string> SceneName = new SyncVar<string>(new SyncTypeSettings()
		{
			SendRate = 0.0f,
			Channel = Channel.Unreliable,
			ReadPermission = ReadPermission.OwnerOnly,
			WritePermission = WritePermission.ServerOnly,
		});
		public int SceneHandle;
		public string LastChatMessage = "";
		public DateTime NextChatMessageTime = DateTime.UtcNow;
		public DateTime NextInteractTime = DateTime.UtcNow;

		void Awake()
		{
			Transform = transform;

			#region KCC
			Motor = gameObject.GetComponent<KinematicCharacterMotor>();
			CharacterController = gameObject.GetComponent<KCCController>();
			if (CharacterController != null)
			{
				CharacterController.Character = this;
			}
			KCCPlayer = gameObject.GetComponent<KCCPlayer>();
			#endregion

			ICharacterBehaviour[] c = gameObject.GetComponents<ICharacterBehaviour>();
			if (c != null)
			{
				for (int i = 0; i < c.Length; ++i)
				{
					ICharacterBehaviour behaviour = c[i];
					if (behaviour == null)
					{
						continue;
					}

					behaviour.InitializeOnce(this);
				}
			}
		}

		public override void ReadPayload(NetworkConnection connection, Reader reader)
		{
			ID = reader.ReadInt64();

#if !UNITY_SERVER
			OnReadPayload?.Invoke(this);
#endif
		}

		public override void WritePayload(NetworkConnection connection, Writer writer)
		{
			writer.WriteInt64(ID);
		}

#if !UNITY_SERVER
		public override void OnStartClient()
		{
			base.OnStartClient();

			if (base.IsOwner)
			{
				Character.OnStartLocalClient?.Invoke(this);

				gameObject.layer = Constants.Layers.LocalEntity;
				CharacterController.MeshRoot.gameObject.layer = Constants.Layers.LocalEntity;

				foreach (ICharacterBehaviour behaviour in this.behaviours.Values)
				{
					behaviour.OnStartCharacter();
				}
			}
		}

		public override void OnStopClient()
		{
			base.OnStopClient();
			if (base.IsOwner)
			{
				foreach (ICharacterBehaviour behaviour in this.behaviours.Values)
				{
					behaviour.OnStopCharacter();
				}

				Character.OnStopLocalClient?.Invoke(this);

				gameObject.layer = Constants.Layers.Default;
				CharacterController.MeshRoot.gameObject.layer = Constants.Layers.Default;
			}
		}
#endif

		internal void SetSyncVarDatabaseValue<T>(SyncVar<T> syncVar, T value)
		{
			syncVar.Value = value;
			syncVar.SetInitialValues(value);
		}

		public void RegisterCharacterBehaviour(ICharacterBehaviour behaviour)
		{
			if (behaviour == null)
			{
				return;
			}
			Type[] interfaces = behaviour.GetType().GetInterfaces();
			for (int i = 0; i < interfaces.Length; ++i)
			{
				Type interfaceType = interfaces[i];
				if (interfaceType == typeof(ICharacterBehaviour) ||
					interfaceType == typeof(IItemContainer)) // IItemContainer has multiple inheritence, this fixes any issues related to it
				{
					continue;
				}
				if (!behaviours.ContainsKey(interfaceType))
				{
					//Debug.Log(CharacterName + ": Registered " + interfaceType.Name);
					behaviours.Add(interfaceType, behaviour);
					return;
				}
			}
		}

		public void UnregisterCharacterBehaviour<T>(T behaviour) where T : ICharacterBehaviour
		{
			if (behaviour == null)
			{
				return;
			}
			Type[] interfaces = behaviour.GetType().GetInterfaces();
			for (int i = 0; i < interfaces.Length; ++i)
			{
				Type interfaceType = interfaces[i];
				if (interfaceType == typeof(ICharacterBehaviour) ||
					interfaceType == typeof(IItemContainer)) // IItemContainer has multiple inheritence, this fixes any issues related to it
				{
					continue;
				}
				//Debug.Log(CharacterName + ": Unregistered " + interfaceType.Name);
				behaviours.Remove(interfaceType);
			}
		}

		public bool TryGet<T>(out T control) where T : class, ICharacterBehaviour
		{
			if (behaviours.TryGetValue(typeof(T), out ICharacterBehaviour result))
			{
				if ((control = result as T) != null)
				{
					return true;
				}
			}
			control = null;
			return false;
		}

		public T Get<T>() where T : class, ICharacterBehaviour
		{
			if (behaviours.TryGetValue(typeof(T), out ICharacterBehaviour result))
			{
				return result as T;
			}
			return null;
		}

		/// <summary>
		/// Resets the Character values to default for pooling.
		/// </summary>
		public void OnPooledReset()
		{
			TeleporterName = "";
			LastChatMessage = "";
			NextChatMessageTime = DateTime.UtcNow;
			NextInteractTime = DateTime.UtcNow;
		}

		public void Teleport(string teleporterName)
		{
			TeleporterName = teleporterName;

#if UNITY_SERVER
			// just disconnect
			Owner.Disconnect(false);
#endif
		}

		public void SetGuildName(string guildName)
		{
#if !UNITY_SERVER
			if (CharacterGuildLabel != null)
			{
				CharacterGuildLabel.text = !string.IsNullOrWhiteSpace(guildName) ? "[" + guildName + "]" : "";
			}
#endif
		}
	}
}