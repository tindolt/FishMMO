﻿using System;

namespace FishMMO.Server
{
	public class SceneInstanceDetails
	{
		public long WorldServerID;
		public string Name;
		public int Handle;
		public int CharacterCount;
		public bool StalePulse = false;
		public DateTime LastExit = DateTime.UtcNow;

		public void AddCharacterCount(int count)
		{
			//UnityEngine.Debug.Log($"{Name} adding {count} to CharacterCount {CharacterCount}");
			CharacterCount += count;
			if (CharacterCount < 1)
			{
				LastExit = DateTime.UtcNow;
				StalePulse = true;
			}
		}
	}
}