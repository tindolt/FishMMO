﻿using UnityEngine;
using System;
using FishNet.Transporting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;

namespace FishMMO.Shared
{
	public class AchievementController : CharacterBehaviour, IAchievementController
	{
		private Dictionary<int, Achievement> achievements = new Dictionary<int, Achievement>();


		public Dictionary<int, Achievement> Achievements { get { return achievements; } }

		public event Action<Achievement> OnAddAchievement;
		public event Action<Achievement> OnUpdateAchievement;

#if !UNITY_SERVER
		public bool ShowAchievementCompletion = true;

		public override void OnStartCharacter()
		{
			base.OnStartCharacter();

			if (!base.IsOwner)
			{
				enabled = false;
				return;
			}

			ClientManager.RegisterBroadcast<AchievementUpdateBroadcast>(OnClientAchievementUpdateBroadcastReceived);
			ClientManager.RegisterBroadcast<AchievementUpdateMultipleBroadcast>(OnClientAchievementUpdateMultipleBroadcastReceived);
		}

		public override void OnStopCharacter()
		{
			base.OnStopCharacter();

			if (base.IsOwner)
			{
				ClientManager.UnregisterBroadcast<AchievementUpdateBroadcast>(OnClientAchievementUpdateBroadcastReceived);
				ClientManager.UnregisterBroadcast<AchievementUpdateMultipleBroadcast>(OnClientAchievementUpdateMultipleBroadcastReceived);
			}
		}

		/// <summary>
		/// Server sent an achievement update broadcast.
		/// </summary>
		private void OnClientAchievementUpdateBroadcastReceived(AchievementUpdateBroadcast msg, Channel channel)
		{
			AchievementTemplate template = AchievementTemplate.Get<AchievementTemplate>(msg.TemplateID);
			if (template != null)
			{
				SetAchievement(template.ID, msg.Tier, msg.Value);
			}
		}

		/// <summary>
		/// Server sent a multiple achievement update broadcast.
		/// </summary>
		private void OnClientAchievementUpdateMultipleBroadcastReceived(AchievementUpdateMultipleBroadcast msg, Channel channel)
		{
			foreach (AchievementUpdateBroadcast subMsg in msg.achievements)
			{
				AchievementTemplate template = AchievementTemplate.Get<AchievementTemplate>(subMsg.TemplateID);
				
				if (template != null)
				{
					SetAchievement(template.ID, subMsg.Tier, subMsg.Value);
				}
			}
		}
#endif

		public void SetAchievement(int templateID, byte tier, uint value)
		{
			if (achievements == null)
			{
				achievements = new Dictionary<int, Achievement>();
			}

			if (achievements.TryGetValue(templateID, out Achievement achievement))
			{
				achievement.CurrentTier = tier;
				achievement.CurrentValue = value;
				OnUpdateAchievement?.Invoke(achievement);
			}
			else
			{
				achievements.Add(templateID, achievement = new Achievement(templateID, tier, value));
				OnAddAchievement?.Invoke(achievement);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetAchievement(int templateID, out Achievement achievement)
		{
			return achievements.TryGetValue(templateID, out achievement);
		}

		public void Increment(AchievementTemplate template, uint amount)
		{
			if (template == null)
			{
				return;
			}
			if (achievements == null)
			{
				achievements = new Dictionary<int, Achievement>();
			}

			Achievement achievement;
			if (!achievements.TryGetValue(template.ID, out achievement))
			{
				achievements.Add(template.ID, achievement = new Achievement(template.ID));
				OnAddAchievement?.Invoke(achievement);
			}

			// get the old values
			byte currentTier = achievement.CurrentTier;
			uint currentValue = achievement.CurrentValue;

			// update current value
			achievement.CurrentValue += amount;

			List<AchievementTier> tiers = template.Tiers;
			if (tiers != null)
			{
				for (byte i = currentTier; i < tiers.Count && i < byte.MaxValue; ++i)
				{
					AchievementTier tier = tiers[i];
					if (achievement.CurrentValue > tier.MaxValue)
					{
						// Client: Display a text message above the characters head showing the achievement.
						// Server: Provide rewards.
						IAchievementController.OnCompleteAchievement?.Invoke(Character, achievement.Template, tier);
					}
					else
					{
						achievement.CurrentTier = i;
						break;
					}
				}
			}

			OnUpdateAchievement?.Invoke(achievement);
		}
	}
}