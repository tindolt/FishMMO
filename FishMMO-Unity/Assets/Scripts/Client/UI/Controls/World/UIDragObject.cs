﻿using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishMMO.Client
{
	public class UIDragObject : UIControl
	{
		public RawImage Icon;
		public int ReferenceID = UIReferenceButton.NULL_REFERENCE_ID;
		public HotkeyType HotkeyType = HotkeyType.None;

		public LayerMask LayerMask;
		public float DropDistance = 5.0f;

		public override void OnStarting()
		{
		}

		public override void OnDestroying()
		{
		}

		void Update()
		{
			if (Visible)
			{
				if (Icon == null || Icon.texture == null || ReferenceID == UIReferenceButton.NULL_REFERENCE_ID)
				{
					Clear();
					return;
				}

				// clear the hotkey if we are clicking anywhere that isn't the UI
				// also we can handle dropping items to the ground here if we want
				if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
				{
					// we can drop items on the ground from inventory
					if (HotkeyType == HotkeyType.Inventory)
					{
						Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
						RaycastHit hit;
						if (Physics.Raycast(ray, out hit, DropDistance, LayerMask))
						{
							//Drop item at position of hit
							Debug.Log("Dropping item to ground at pos[" + hit.point + "]");
						}
					}
					Clear();
					return;
				}

				// UIDragObject always follows the mouse cursor
				Vector3 offset = new Vector3(Icon.texture.width * 0.5f + 1.0f, Icon.texture.height * -0.5f - 1.0f, 0.0f);
				transform.position = Input.mousePosition + offset;
			}
		}

		public void SetReference(Texture icon, int referenceID, HotkeyType hotkeyType)
		{
			this.Icon.texture = icon;
			this.ReferenceID = referenceID;
			this.HotkeyType = hotkeyType;

			// set position immediately so we don't have any position glitches before Update is triggered
			Vector3 offset = new Vector3(this.Icon.texture.width * 0.5f + 1.0f, this.Icon.texture.height * -0.5f - 1.0f, 0.0f);
			transform.position = Input.mousePosition + offset;

			Visible = true;
		}

		public void Clear()
		{
			Visible = false;

			Icon.texture = null;
			ReferenceID = UIReferenceButton.NULL_REFERENCE_ID;
			HotkeyType = HotkeyType.None;
			//transform.position = new Vector3(-9999.0f, -9999.0f, 0.0f); // do we need to do this?
		}
	}
}