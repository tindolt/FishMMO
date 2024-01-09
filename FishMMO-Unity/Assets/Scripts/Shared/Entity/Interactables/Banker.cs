﻿#if UNITY_SERVER
using FishNet.Transporting;
#endif
using UnityEngine;

namespace FishMMO.Shared
{
	[RequireComponent(typeof(SceneObjectNamer))]
	public class Banker : Interactable
	{
		public override bool OnInteract(Character character)
		{
			if (!base.OnInteract(character))
			{
				return false;
			}

#if UNITY_SERVER
			character.Owner.Broadcast(new BankerBroadcast()
			{
				interactableID = ID,
			}, true, Channel.Reliable);
#endif
			return true;
		}
	}
}