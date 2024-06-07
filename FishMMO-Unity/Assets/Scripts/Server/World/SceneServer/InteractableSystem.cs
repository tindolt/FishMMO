﻿using FishNet.Connection;
using FishNet.Transporting;
using FishMMO.Shared;
using FishMMO.Server.DatabaseServices;
using FishMMO.Database.Npgsql;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishMMO.Server
{
	/// <summary>
	/// This service helps the server validate clients interacting with Interactable objects in scenes.
	/// </summary>
	public class InteractableSystem : ServerBehaviour
	{
		public WorldSceneDetailsCache WorldSceneDetailsCache;
		public int MaxAbilityCount = 25;
		public CharacterAttributeTemplate CurrencyTemplate;

		public override void InitializeOnce()
		{
			if (Server != null)
			{
				Server.RegisterBroadcast<InteractableBroadcast>(OnServerInteractableBroadcastReceived, true);
				Server.RegisterBroadcast<MerchantPurchaseBroadcast>(OnServerMerchantPurchaseBroadcastReceived, true);
				Server.RegisterBroadcast<AbilityCraftBroadcast>(OnServerAbilityCraftBroadcastReceived, true);

				Bindstone.OnBind += TryBind;
			}
			else
			{
				enabled = false;
			}
		}

		public override void Destroying()
		{
			if (Server != null)
			{
				Server.UnregisterBroadcast<InteractableBroadcast>(OnServerInteractableBroadcastReceived);
				Server.UnregisterBroadcast<MerchantPurchaseBroadcast>(OnServerMerchantPurchaseBroadcastReceived);
				Server.UnregisterBroadcast<AbilityCraftBroadcast>(OnServerAbilityCraftBroadcastReceived);

				Bindstone.OnBind -= TryBind;
			}
		}

		/// <summary>
		/// Attempts to add items to a characters inventory controller and broadcasts the update to the client.
		/// </summary>
		private bool SendNewItemBroadcast(NpgsqlDbContext dbContext, NetworkConnection conn, ICharacter character, IInventoryController inventoryController, Item newItem)
		{
			List<InventorySetItemBroadcast> modifiedItemBroadcasts = new List<InventorySetItemBroadcast>();

			// see if we have successfully added the item
			if (inventoryController.TryAddItem(newItem, out List<Item> modifiedItems) &&
				modifiedItems != null &&
				modifiedItems.Count > 0)
			{
				// add slot update requests to our message
				foreach (Item item in modifiedItems)
				{
					// just in case..
					if (item == null)
					{
						continue;
					}

					// update or add the item to the database and initialize
					CharacterInventoryService.SetSlot(dbContext, character.ID, item);

					// create the new item broadcast
					modifiedItemBroadcasts.Add(new InventorySetItemBroadcast()
					{
						instanceID = item.ID,
						templateID = item.Template.ID,
						slot = item.Slot,
						seed = item.IsGenerated ? item.Generator.Seed : 0,
						stackSize = item.IsStackable ? item.Stackable.Amount : 0,
					});
				}
			}

			// tell the client they have new items
			if (modifiedItemBroadcasts.Count > 0)
			{
				Server.Broadcast(conn, new InventorySetMultipleItemsBroadcast()
				{
					items = modifiedItemBroadcasts,
				}, true, Channel.Reliable);

				return true;
			}
			return false;
		}

		/// <summary>
		/// Interactable broadcast received from a character.
		/// </summary>
		private void OnServerInteractableBroadcastReceived(NetworkConnection conn, InteractableBroadcast msg, Channel channel)
		{
			if (conn == null)
			{
				return;
			}

			// validate connection character
			if (conn.FirstObject == null)
			{
				return;
			}
			IPlayerCharacter character = conn.FirstObject.GetComponent<IPlayerCharacter>();
			if (character == null)
			{
				return;
			}

			// validate scene
			if (!WorldSceneDetailsCache.Scenes.TryGetValue(character.SceneName, out WorldSceneDetails details))
			{
				Debug.Log("Missing Scene:" + character.SceneName);
				return;
			}

			// validate scene object
			if (!ValidateSceneObject(msg.interactableID, character.GameObject.scene.handle, out ISceneObject sceneObject))
			{
				return;
			}

			IInteractable interactable = sceneObject.GameObject.GetComponent<IInteractable>();
			if (interactable != null &&
				interactable.CanInteract(character))
			{
				switch (interactable)
				{
					case AbilityCrafter:
						Server.Broadcast(character.Owner, new AbilityCrafterBroadcast()
						{
							interactableID = sceneObject.ID,
						}, true, Channel.Reliable);
						break;
					case Banker:
						if (character.TryGet(out IBankController bankController))
						{
							bankController.LastInteractableID = sceneObject.ID;

							Server.Broadcast(character.Owner, new BankerBroadcast(), true, Channel.Reliable);
						}
						break;
					case Merchant merchant:
						Server.Broadcast(character.Owner, new MerchantBroadcast()
						{
							interactableID = sceneObject.ID,
							templateID = merchant.Template.ID,
						}, true, Channel.Reliable);
						break;
					case WorldItem worldItem:
						if (worldItem.Template != null &&
							character.TryGet(out IInventoryController inventoryController) &&
							inventoryController.HasFreeSlot())
						{
							using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
							if (dbContext == null)
							{
								return;
							}

							Item newItem = new Item(worldItem.Template, 1);
							if (newItem == null)
							{
								return;
							}

							if (SendNewItemBroadcast(dbContext, conn, character, inventoryController, newItem))
							{
								worldItem.Despawn();
							}
						}
						break;
					default:
						return;
				}
			}
		}

		private void OnServerMerchantPurchaseBroadcastReceived(NetworkConnection conn, MerchantPurchaseBroadcast msg, Channel channel)
		{
			if (conn == null)
			{
				return;
			}

			// validate connection character
			if (conn.FirstObject == null)
			{
				return;
			}
			IPlayerCharacter character = conn.FirstObject.GetComponent<IPlayerCharacter>();
			if (character == null ||
				!character.TryGet(out IInventoryController inventoryController))
			{
				return;
			}

			// validate template exists
			MerchantTemplate merchantTemplate = MerchantTemplate.Get<MerchantTemplate>(msg.id);
			if (merchantTemplate == null)
			{
				return;
			}

			// validate scene
			if (!WorldSceneDetailsCache.Scenes.TryGetValue(character.SceneName, out WorldSceneDetails details))
			{
				Debug.Log("Missing Scene:" + character.SceneName);
				return;
			}

			// validate scene object
			if (!ValidateSceneObject(msg.interactableID, character.GameObject.scene.handle, out ISceneObject sceneObject))
			{
				return;
			}

			// validate interactable
			IInteractable interactable = sceneObject.GameObject.GetComponent<IInteractable>();
			if (interactable == null ||
				!interactable.InRange(character.Transform))
			{
				return;
			}
			Merchant merchant = interactable as Merchant;
			if (merchant == null ||
				merchantTemplate.ID != merchant.Template.ID)
			{
				return;
			}

			using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
			if (dbContext == null)
			{
				return;
			}

			switch (msg.type)
			{
				case MerchantTabType.Item:
					BaseItemTemplate itemTemplate = merchantTemplate.Items[msg.index];
					if (itemTemplate == null)
					{
						return;
					}

					// do we have enough currency to purchase this?
					if (CurrencyTemplate == null)
					{
						Debug.Log("CurrencyTemplate is null.");
						return;
					}
					if (!character.TryGet(out ICharacterAttributeController attributeController) ||
						!attributeController.TryGetAttribute(CurrencyTemplate, out CharacterAttribute currency) ||
						currency.FinalValue < itemTemplate.Price)
					{
						return;
					}

					if (merchantTemplate.Items != null &&
						merchantTemplate.Items.Count >= msg.index)
					{
						Item newItem = new Item(itemTemplate, 1);
						if (newItem == null)
						{
							return;
						}

						SendNewItemBroadcast(dbContext, conn, character, inventoryController, newItem);
					}
					break;
				case MerchantTabType.Ability:
					if (merchantTemplate.Abilities != null &&
						merchantTemplate.Abilities.Count >= msg.index)
					{
						LearnAbilityTemplate(dbContext, conn, character, merchantTemplate.Abilities[msg.index]);
					}
					break;
				case MerchantTabType.AbilityEvent:
					if (merchantTemplate.AbilityEvents != null &&
						merchantTemplate.AbilityEvents.Count >= msg.index)
					{
						LearnAbilityTemplate(dbContext, conn, character, merchantTemplate.AbilityEvents[msg.index]);
					}
					break;
				default: return;
			}
		}

		public void LearnAbilityTemplate<T>(NpgsqlDbContext dbContext, NetworkConnection conn, IPlayerCharacter character, T template) where T : BaseAbilityTemplate
		{
			// do we already know this ability?
			if (template == null ||
				character == null ||
				!character.TryGet(out IAbilityController abilityController) ||
				abilityController.KnowsAbility(template.ID))
			{
				return;
			}

			// do we have enough currency to purchase this?
			if (CurrencyTemplate == null)
			{
				Debug.Log("CurrencyTemplate is null.");
				return;
			}
			if (!character.TryGet(out ICharacterAttributeController attributeController) ||
				!attributeController.TryGetAttribute(CurrencyTemplate, out CharacterAttribute currency) ||
				currency.FinalValue < template.Price)
			{
				Debug.Log("Not enough currency!");
				return;
			}

			// learn the ability
			abilityController.LearnBaseAbilities(new List<BaseAbilityTemplate> { template });

			// remove the price from the characters currency
			currency.AddValue(template.Price);

			// add the known ability to the database
			CharacterKnownAbilityService.Add(dbContext, character.ID, template.ID);

			// tell the client about the new ability event
			Server.Broadcast(conn, new KnownAbilityAddBroadcast()
			{
				templateID = template.ID,
			}, true, Channel.Reliable);
		}

		public void OnServerAbilityCraftBroadcastReceived(NetworkConnection conn, AbilityCraftBroadcast msg, Channel channel)
		{
			if (conn == null)
			{
				return;
			}

			// validate connection character
			if (conn.FirstObject == null)
			{
				return;
			}
			IPlayerCharacter character = conn.FirstObject.GetComponent<IPlayerCharacter>();
			if (character == null ||
				!character.TryGet(out IAbilityController abilityController))
			{
				return;
			}

			// validate main ability exists
			AbilityTemplate mainAbility = AbilityTemplate.Get<AbilityTemplate>(msg.templateID);
			if (mainAbility == null)
			{
				return;
			}

			// validate scene
			if (!WorldSceneDetailsCache.Scenes.TryGetValue(character.SceneName, out WorldSceneDetails details))
			{
				Debug.Log("Missing Scene:" + character.SceneName);
				return;
			}

			// validate scene object
			if (!ValidateSceneObject(msg.interactableID, character.GameObject.scene.handle, out ISceneObject sceneObject))
			{
				return;
			}

			// validate interactable
			IInteractable interactable = sceneObject.GameObject.GetComponent<IInteractable>();
			if (interactable == null ||
				!interactable.InRange(character.Transform))
			{
				return;
			}

			// validate the character can learn the ability
			if (!abilityController.KnowsAbility(mainAbility.ID) ||
				abilityController.KnowsLearnedAbility(mainAbility.ID) ||
				abilityController.KnownAbilities.Count >= MaxAbilityCount)
			{
				return;
			}

			int price = mainAbility.Price;

			// validate eventIds if there are any...
			if (msg.events != null)
			{
				bool hasTypeOverride = false;
				HashSet<int> validatedEvents = new HashSet<int>();
				for (int i = 0; i < msg.events.Count; ++i)
				{
					int id = msg.events[i];
					if (validatedEvents.Contains(id))
					{
						// duplicate events
						return;
					}
					AbilityEvent eventTemplate = AbilityEvent.Get<AbilityEvent>(id);
					if (eventTemplate == null)
					{
						// unknown ability event
						return;
					}

					// validate that the character knows the ability event
					if (!abilityController.KnowsAbility(eventTemplate.ID))
					{
						return;
					}

					if (eventTemplate is AbilityTypeOverrideEventType)
					{
						if (hasTypeOverride)
						{
							// duplicate ability type override
							return;
						}
						hasTypeOverride = true;
					}

					price += eventTemplate.Price;
				}
			}

			// do we have enough currency to purchase this?
			if (CurrencyTemplate == null)
			{
				Debug.Log("CurrencyTemplate is null.");
				return;
			}
			if (!character.TryGet(out ICharacterAttributeController attributeController) ||
				!attributeController.TryGetAttribute(CurrencyTemplate, out CharacterAttribute currency) ||
				currency.FinalValue < price)
			{
				return;
			}

			Ability newAbility = new Ability(mainAbility, msg.events);
			if (newAbility == null)
			{
				return;
			}

			using var dbContext = Server.NpgsqlDbContextFactory.CreateDbContext();
			if (dbContext == null)
			{
				return;
			}

			CharacterAbilityService.UpdateOrAdd(dbContext, character.ID, newAbility);

			abilityController.LearnAbility(newAbility);

			currency.AddValue(price);

			AbilityAddBroadcast abilityAddBroadcast = new AbilityAddBroadcast()
			{
				id = newAbility.ID,
				templateID = newAbility.Template.ID,
				events = msg.events,
			};

			Server.Broadcast(conn, abilityAddBroadcast, true, Channel.Reliable);
		}

		private void TryBind(IPlayerCharacter character, Bindstone bindstone)
		{
			if (character == null)
			{
				Debug.Log("Character not found!");
				return;
			}

			// Validate same scene
			if (character.SceneName != bindstone.gameObject.scene.name)
			{
				Debug.Log("Character is not in the same scene as the bindstone!");
				return;
			}

			if (!ServerBehaviour.TryGet(out SceneServerSystem sceneServerSystem))
			{
				Debug.Log("SceneServerSystem not found!");
				return;
			}

			using var dbContext = sceneServerSystem.Server.NpgsqlDbContextFactory.CreateDbContext();
			if (dbContext == null)
			{
				Debug.Log("Could not get database context.");
				return;
			}

			character.BindPosition = character.Motor.Transform.position;
			character.BindScene = character.SceneName;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ValidateSceneObject(long sceneObjectID, int characterSceneHandle, out ISceneObject sceneObject)
		{
			if (!SceneObject.Objects.TryGetValue(sceneObjectID, out sceneObject))
			{
				if (sceneObject == null)
				{
					Debug.Log("Missing SceneObject");
				}
				else
				{
					Debug.Log("Missing ID:" + sceneObjectID);
				}
				return false;
			}
			if (sceneObject.GameObject.scene.handle != characterSceneHandle)
			{
				Debug.Log("Object scene mismatch.");
				return false;
			}
			return true;
		}
	}
}