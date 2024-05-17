﻿using UnityEngine;
using FishNet.Object;

namespace FishMMO.Shared
{
	[CreateAssetMenu(fileName = "New Spawnable", menuName = "Spawnable/Spawnable", order = 1)]
	[ExecuteInEditMode]
	public class Spawnable : CachedScriptableObject<Spawnable>, ICachedObject
	{
		public NetworkObject NetworkObject;
		public float MinimumRespawnTime;
		public float MaximumRespawnTime;

		[ShowReadonly]
		public float YOffset;

		public string Name { get { return this.name; } }

		void OnValidate()
		{
			// get the collider height
			Collider collider = NetworkObject.GetComponent<Collider>();
			switch (collider)
			{
				case CapsuleCollider capsule:
					YOffset = capsule.height * 0.5f;
					break;
				case BoxCollider box:
					YOffset = box.bounds.extents.y;
					break;
				case SphereCollider sphere:
					YOffset = sphere.radius * 0.5f;
					break;
				default: break;
			}
		}
	}
}