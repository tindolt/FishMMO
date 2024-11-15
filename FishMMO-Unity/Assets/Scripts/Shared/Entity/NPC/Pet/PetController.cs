using FishNet.Transporting;

namespace FishMMO.Shared
{
	/// <summary>
	/// Character guild controller.
	/// </summary>
	public class PetController : CharacterBehaviour, IPetController
	{
		public Pet Pet { get; }

#if !UNITY_SERVER
		public override void OnStartCharacter()
		{
			base.OnStartCharacter();

			if (base.IsOwner)
			{
				ClientManager.RegisterBroadcast<PetAddBroadcast>(OnClientPetAddBroadcastReceived);
				ClientManager.RegisterBroadcast<PetRemoveBroadcast>(OnClientPetRemoveBroadcastReceived);
			}
		}

		public override void OnStopCharacter()
		{
			base.OnStopCharacter();

			if (base.IsOwner)
			{
				ClientManager.UnregisterBroadcast<PetAddBroadcast>(OnClientPetAddBroadcastReceived);
				ClientManager.UnregisterBroadcast<PetRemoveBroadcast>(OnClientPetRemoveBroadcastReceived);
			}
		}

		public void OnClientPetAddBroadcastReceived(PetAddBroadcast msg, Channel channel)
		{
		}

		public void OnClientPetRemoveBroadcastReceived(PetRemoveBroadcast msg, Channel channel)
		{
		}
#endif
	}
}