using FishNet.Object;
using FishNet.Transporting;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FishMMO.Shared;

namespace FishMMO.Client
{
	public class UICharacterCreate : UIControl
	{
		public Button createButton;
		public TMP_Text createResultText;
		public TMP_Dropdown startRaceDropdown;
		public TMP_Dropdown startLocationDropdown;
		public RectTransform characterParent;

		public string characterName = "";
		public int raceIndex = -1;
		public List<string> initialRaceNames = new List<string>();
		public List<string> initialSpawnLocationNames = new List<string>();
		public WorldSceneDetailsCache worldSceneDetailsCache = null;
		public int selectedSpawnPosition = -1;

		private Dictionary<string, int> raceNameMap = new Dictionary<string, int>();

		private Dictionary<string, HashSet<string>> raceSpawnPositionMap = new Dictionary<string, HashSet<string>>();

		public override void OnStarting()
		{
			// initialize race dropdown
			if (startRaceDropdown != null &&
				initialRaceNames != null)
			{
				raceNameMap.Clear();
				initialRaceNames.Clear();

				Dictionary<int, RaceTemplate> raceTemplates = RaceTemplate.LoadCache<RaceTemplate>();
				foreach (KeyValuePair<int, RaceTemplate> pair in raceTemplates)
				{
					if (pair.Value.Prefab == null)
					{
						continue;
					}
					Character character = pair.Value.Prefab.GetComponent<Character>();
					if (character == null)
					{
						continue;
					}
					if (Client.NetworkManager.SpawnablePrefabs.GetObject(false, character.NetworkObject.PrefabId) == null)
					{
						continue;
					}
					raceNameMap.Add(character.gameObject.name, pair.Key);
					initialRaceNames.Add(character.gameObject.name);

					// initialize spawn position map
					if (!raceSpawnPositionMap.TryGetValue(pair.Value.Name, out HashSet<string> spawners))
					{
						raceSpawnPositionMap.Add(pair.Value.Name, spawners = new HashSet<string>());
					}

					foreach (WorldSceneDetails details in worldSceneDetailsCache.Scenes.Values)
					{
						foreach (CharacterInitialSpawnPositionDetails initialSpawnPosition in details.InitialSpawnPositions.Values)
						{
							foreach (RaceTemplate raceTemplate in initialSpawnPosition.AllowedRaces)
							{
								if (pair.Value.Name == raceTemplate.Name &&
									!spawners.Contains(initialSpawnPosition.SpawnerName))
								{
									spawners.Add(initialSpawnPosition.SpawnerName);
								}
							}
						}
					}
				}
				startRaceDropdown.ClearOptions();
				startRaceDropdown.AddOptions(initialRaceNames);

				// set initial race selection
				raceIndex = 0;
			}

			UpdateStartLocationDropdown();

			Client.NetworkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
			Client.NetworkManager.ClientManager.RegisterBroadcast<CharacterCreateResultBroadcast>(OnClientCharacterCreateResultBroadcastReceived);
		}


		public override void OnDestroying()
		{
			Client.NetworkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
			Client.NetworkManager.ClientManager.UnregisterBroadcast<CharacterCreateResultBroadcast>(OnClientCharacterCreateResultBroadcastReceived);
		}

		private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
		{
			if (obj.ConnectionState == LocalConnectionState.Stopped)
			{
				Hide();
			}
		}

		private void OnClientCharacterCreateResultBroadcastReceived(CharacterCreateResultBroadcast msg, Channel channel)
		{
			SetCreateButtonLocked(false);
			if (msg.result == CharacterCreateResult.Success)
			{
				Hide();
				UIManager.Show("UICharacterSelect");
			}
			else if (createResultText != null)
			{
				createResultText.text = msg.result.ToString();
			}
		}

		public void OnCharacterNameChangeEndEdit(TMP_InputField inputField)
		{
			characterName = inputField.text;
		}

		public void OnRaceDropdownValueChanged(TMP_Dropdown dropdown)
		{
			raceIndex = dropdown.value;

			UpdateStartLocationDropdown();
		}

		private void UpdateStartLocationDropdown()
		{
			// update start location dropdown
			if (startLocationDropdown != null &&
				initialSpawnLocationNames != null)
			{
				initialSpawnLocationNames.Clear();

				string raceName = startRaceDropdown.options[raceIndex].text;

				// find all spawn locations that allow the currently selected race
				if (raceSpawnPositionMap.TryGetValue(raceName, out HashSet<string> spawners))
				{
					foreach (string spawner in spawners)
					{
						initialSpawnLocationNames.Add(spawner);
					}
				}
				startLocationDropdown.ClearOptions();
				startLocationDropdown.AddOptions(initialSpawnLocationNames);
				selectedSpawnPosition = 0;
			}
		}

		public void OnSpawnLocationDropdownValueChanged(TMP_Dropdown dropdown)
		{
			selectedSpawnPosition = dropdown.value;
		}

		public void OnClick_CreateCharacter()
		{
			if (Client.IsConnectionReady() &&
				Constants.Authentication.IsAllowedCharacterName(characterName) &&
				worldSceneDetailsCache != null &&
				raceIndex > -1 &&
				selectedSpawnPosition > -1)
			{
				foreach (WorldSceneDetails details in worldSceneDetailsCache.Scenes.Values)
				{
					string raceName = startRaceDropdown.options[raceIndex].text;

					if (details.InitialSpawnPositions.TryGetValue(initialSpawnLocationNames[selectedSpawnPosition], out CharacterInitialSpawnPositionDetails spawnPosition) &&
						raceNameMap.TryGetValue(raceName, out int raceTemplateID))
					{
						// create character
						Client.Broadcast(new CharacterCreateBroadcast()
						{
							characterName = characterName,
							raceTemplateID = raceTemplateID,
							sceneName = spawnPosition.SceneName,
							spawnerName = spawnPosition.SpawnerName,
						}, Channel.Reliable);
						SetCreateButtonLocked(true);
						return;
					}
				}
			}
		}

		public void OnClick_QuitToLogin()
		{
			// we should go back to login..
			Client.QuitToLogin();
		}

		public void OnClick_Quit()
		{
			Client.Quit();
		}

		private void SetCreateButtonLocked(bool locked)
		{
			createButton.interactable = !locked;
		}
	}
}