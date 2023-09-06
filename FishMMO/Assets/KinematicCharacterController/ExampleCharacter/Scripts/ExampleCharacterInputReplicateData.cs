﻿using FishNet.Object.Prediction;
using UnityEngine;

namespace KinematicCharacterController.Examples
{
	public struct ExampleCharacterInputReplicateData : IReplicateData
	{
		public float MoveAxisForward;
		public float MoveAxisRight;
		public int MoveFlags;
		public Vector3 CameraPosition;
		public Quaternion CameraRotation;

		public ExampleCharacterInputReplicateData(float moveAxisForward, float moveAxisRight, int moveFlags, Vector3 cameraPosition, Quaternion cameraRotation)
		{
			MoveAxisForward = moveAxisForward;
			MoveAxisRight = moveAxisRight;
			MoveFlags = moveFlags;
			CameraPosition = cameraPosition;
			CameraRotation = cameraRotation;

			_tick = 0;
		}

		private uint _tick;
		public void Dispose() { }
		public uint GetTick() => _tick;
		public void SetTick(uint value) => _tick = value;
	}
}