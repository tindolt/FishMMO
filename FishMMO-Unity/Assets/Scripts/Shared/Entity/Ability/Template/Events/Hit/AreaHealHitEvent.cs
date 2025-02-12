﻿using UnityEngine;

namespace FishMMO.Shared
{
	public sealed class AreaHealHitEvent : HitEvent
	{
		private Collider[] colliders = new Collider[100];

		public int HitCount;
		public int Amount;
		public float Radius;
		public LayerMask CollidableLayers = -1;

		public override int Invoke(Character attacker, Character defender, TargetInfo hitTarget, GameObject abilityObject)
		{
			PhysicsScene physicsScene = attacker.gameObject.scene.GetPhysicsScene();

			int overlapCount = physicsScene.OverlapSphere(//Physics.OverlapCapsuleNonAlloc(
				hitTarget.Target.transform.position,
				Radius,
				colliders,
				CollidableLayers,
				QueryTriggerInteraction.Ignore);

			int hits = 0;
			for (int i = 0; i < overlapCount && hits < HitCount; ++i)
			{
				if (colliders[i] != attacker.Motor.Capsule)
				{
					Character def = colliders[i].gameObject.GetComponent<Character>();
					if (def != null && def.DamageController != null)
					{
						def.DamageController.Heal(attacker, Amount);
						++hits;
					}
				}
			}
			return hits;
		}
	}
}