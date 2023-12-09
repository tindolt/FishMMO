﻿using Cysharp.Text;
using UnityEngine;

namespace FishMMO.Shared
{
	public abstract class BaseItemTemplate : CachedScriptableObject<BaseItemTemplate>, ITooltip, ICachedObject
	{
		public bool IsIdentifiable;
		public uint MaxStackSize = 1;
		public float Price;
		//use this for item generation
		public int[] IconPools;
		public Sprite icon;

		public string Name { get { return this.name; } }
		public bool IsStackable { get { return MaxStackSize > 1; } }
		public Sprite Icon { get { return this.icon; } }

		public virtual string Tooltip()
		{
			using (var sb = ZString.CreateStringBuilder())
			{
				sb.Append(RichText.Format(Name, false, "f5ad6e", "120%"));
				sb.Append(RichText.Format("Price", Price, true, "a66ef5FF"));
				return sb.ToString();
			}
		}
	}
}