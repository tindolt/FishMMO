﻿using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishMMO.Shared
{
	public class FactionController : CharacterBehaviour, IFactionController
	{
		private Dictionary<int, Faction> factions = new Dictionary<int, Faction>();
		private Dictionary<int, Faction> allied = new Dictionary<int, Faction>();
		private Dictionary<int, Faction> neutral = new Dictionary<int, Faction>();
		private Dictionary<int, Faction> hostile = new Dictionary<int, Faction>();
		[SerializeField]
		private bool isAggressive = false;

		public bool IsAggressive { get { return isAggressive; } set { isAggressive = value; } }
		public Dictionary<int, Faction> Factions { get { return factions; } }

		public Dictionary<int, Faction> Allied { get { return allied; } }
		public Dictionary<int, Faction> Neutral { get { return neutral; } }
		public Dictionary<int, Faction> Hostile { get { return hostile; } }

		[SerializeField]
		private FactionTemplate template;
		public FactionTemplate Template { get { return this.template; } set { this.template = value; } }

#if !UNITY_SERVER
        public override void OnStartCharacter()
		{
			base.OnStartCharacter();

			if (!base.IsOwner)
			{
				enabled = false;
				return;
			}

			ClientManager.RegisterBroadcast<FactionUpdateBroadcast>(OnClientFactionUpdateBroadcastReceived);
			ClientManager.RegisterBroadcast<FactionUpdateMultipleBroadcast>(OnClientFactionUpdateMultipleBroadcastReceived);
		}

		public override void OnStopCharacter()
		{
			base.OnStopCharacter();

			if (base.IsOwner)
			{
				ClientManager.UnregisterBroadcast<FactionUpdateBroadcast>(OnClientFactionUpdateBroadcastReceived);
				ClientManager.UnregisterBroadcast<FactionUpdateMultipleBroadcast>(OnClientFactionUpdateMultipleBroadcastReceived);
			}
		}

		/// <summary>
		/// Server sent an faction update broadcast.
		/// </summary>
		private void OnClientFactionUpdateBroadcastReceived(FactionUpdateBroadcast msg, Channel channel)
		{
			FactionTemplate template = FactionTemplate.Get<FactionTemplate>(msg.TemplateID);
				if (template != null)
				{
					SetFaction(template.ID, msg.NewValue);
				}
		}

		/// <summary>
		/// Server sent a multiple faction update broadcast.
		/// </summary>
		private void OnClientFactionUpdateMultipleBroadcastReceived(FactionUpdateMultipleBroadcast msg, Channel channel)
		{
			foreach (FactionUpdateBroadcast subMsg in msg.Factions)
			{
				FactionTemplate template = FactionTemplate.Get<FactionTemplate>(subMsg.TemplateID);
				if (template != null)
				{
					SetFaction(template.ID, subMsg.NewValue);
				}
			}
		}
#endif

		public override void ResetState(bool asServer)
		{
			base.ResetState(asServer);

			Factions.Clear();
			Allied.Clear();
			Neutral.Clear();
			Hostile.Clear();
		}
		
		public override void ReadPayload(NetworkConnection conn, Reader reader)
		{
			Factions.Clear();
			Allied.Clear();
			Neutral.Clear();
			Hostile.Clear();

			int factionCount = reader.ReadInt32();
			if (factionCount < 1)
			{
				return;
			}

			for (int i = 0; i < factionCount; ++i)
			{
				int factionID = reader.ReadInt32();
				int value = reader.ReadInt32();

				SetFaction(factionID, value);
			}
		}

		public override void WritePayload(NetworkConnection conn, Writer writer)
		{
			// Write the factions for the clients
			writer.WriteInt32(Factions.Count);
			foreach (Faction faction in Factions.Values)
			{
				writer.WriteInt32(faction.Template.ID);
				writer.WriteInt32(faction.Value);
			}
		}

		public void CopyFrom(IFactionController factionController)
		{
			Factions.Clear();
			Allied.Clear();
			Neutral.Clear();
			Hostile.Clear();

			foreach (Faction faction in factionController.Factions.Values)
			{
				Factions.Add(faction.Template.ID, faction);
			}
			foreach (Faction faction in factionController.Allied.Values)
			{
				Allied.Add(faction.Template.ID, faction);
			}
			foreach (Faction faction in factionController.Neutral.Values)
			{
				Neutral.Add(faction.Template.ID, faction);
			}
			foreach (Faction faction in factionController.Hostile.Values)
			{
				Hostile.Add(faction.Template.ID, faction);
			}
		}

		public void SetFaction(int templateID, int value)
		{
			if (factions.TryGetValue(templateID, out Faction faction))
			{
				RemoveFromAllianceGroup(faction);

				faction.Value = value;
			}
			else
			{
				factions.Add(templateID, faction = new Faction(templateID, value));
			}
			InsertToAllianceGroup(faction);
		}

		public void Add(IFactionController defenderFactionController)
		{
			if (defenderFactionController == null)
			{
				return;
			}
			foreach (Faction faction in defenderFactionController.Allied.Values)
			{
				Add(faction.Template, -Mathf.RoundToInt(faction.Value * 0.01f));
			}
			foreach (Faction faction in defenderFactionController.Hostile.Values)
			{
				Add(faction.Template, Mathf.RoundToInt(faction.Value * 0.01f));
			}
		}

		public void Add(FactionTemplate template, int amount = 1)
		{
			if (template == null)
			{
				return;
			}
			if (factions.TryGetValue(template.ID, out Faction faction))
			{
				RemoveFromAllianceGroup(faction);

				// update value
				faction.Value = (faction.Value + amount).Clamp(FactionTemplate.Minimum, FactionTemplate.Maximum);
			}
			else
			{
				factions.Add(template.ID, faction = new Faction(template.ID, amount));
			}
			if (faction != null)
			{
				IFactionController.OnAddFaction?.Invoke(faction, amount);

				InsertToAllianceGroup(faction);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RemoveFromAllianceGroup(Faction faction)
		{
			if (faction == null ||
				factions == null)
			{
				return;
			}
			if (faction.Value > 0)
			{
				Allied.Remove(faction.Template.ID);
			}
			else if (faction.Value < 0)
			{
				Hostile.Remove(faction.Template.ID);
			}
			else
			{
				Neutral.Remove(faction.Template.ID);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void InsertToAllianceGroup(Faction faction)
		{
			if (faction == null ||
				factions == null)
			{
				return;
			}
			if (faction.Value > 0)
			{
				Allied[faction.Template.ID] = faction;
			}
			else if (faction.Value < 0)
			{
				Hostile[faction.Template.ID] = faction;
			}
			else
			{
				Neutral[faction.Template.ID] = faction;
			}
		}

		public FactionAllianceLevel GetAllianceLevel(IFactionController otherFactionController)
		{
			if (otherFactionController == null)
			{
				return FactionAllianceLevel.Neutral;
			}

			// same party?
			if (Character.TryGet(out IPartyController partyController) &&
				otherFactionController.Character.TryGet(out IPartyController otherPartyController) &&
				partyController.ID != 0 &&
				partyController.ID == otherPartyController.ID)
			{
				return FactionAllianceLevel.Ally;
			}

			// same guild?
			if (Character.TryGet(out IGuildController guildController) &&
				otherFactionController.Character.TryGet(out IGuildController otherGuildController) &&
				guildController.ID != 0 &&
				guildController.ID == otherGuildController.ID)
			{
				return FactionAllianceLevel.Ally;
			}

			// is aggression toggled on either?
			if (IsAggressive || otherFactionController.IsAggressive)
			{
				return FactionAllianceLevel.Enemy;
			}

			if (otherFactionController.Character as NPC != null)
			{
				if (Hostile.ContainsKey(otherFactionController.Template.ID))
				{
					//UnityEngine.Debug.Log($"{otherFactionController.Template.Name}: {otherFactionController.Character.GameObject.name} is an Enemy of {this.Character.GameObject.name}.");

					return FactionAllianceLevel.Enemy;
				}
			}
			else
			{
				foreach (Faction faction in Hostile.Values)
				{
					if (otherFactionController.Factions.TryGetValue(faction.Template.ID, out Faction enemyFaction))
					{
						//UnityEngine.Debug.Log($"{faction.Template.Name}: The target is an {(enemyFaction.Value > 0 ? "Ally" : "Enemy")} of this faction.");

						// Is the enemy allied with our enemy?
						if (enemyFaction.Value > 0)
						{
							return FactionAllianceLevel.Enemy;
						}
					}
				}
			}
			return FactionAllianceLevel.Neutral;
		}

		public Color GetAllianceLevelColor(IFactionController otherFactionController)
		{
			if (IsAggressive || otherFactionController.IsAggressive)
			{
				return TinyColor.ToUnityColor(TinyColor.red);
			}

			FactionAllianceLevel allianceLevel = GetAllianceLevel(otherFactionController);

			switch (allianceLevel)
			{
				case FactionAllianceLevel.Ally:
					return TinyColor.ToUnityColor(TinyColor.green);
				case FactionAllianceLevel.Neutral:
					return TinyColor.ToUnityColor(TinyColor.skyBlue);
				case FactionAllianceLevel.Enemy:
					return TinyColor.ToUnityColor(TinyColor.red);
				default: return Color.white;
			}
		}
	}
}