﻿using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishMMO.Shared
{
	public class CustomObjectPool : ObjectPool
	{
		#region Public.
		/// <summary>
		/// Cache for pooled NetworkObjects.
		/// </summary>  //Remove on 2024/01/01 Convert to IReadOnlyList.
		public IReadOnlyCollection<Dictionary<int, Stack<NetworkObject>>> Cache => _cache;
		private List<Dictionary<int, Stack<NetworkObject>>> _cache = new List<Dictionary<int, Stack<NetworkObject>>>();
		#endregion

		#region Serialized.
		/// <summary>
		/// True if to use object pooling.
		/// </summary>
		[Tooltip("True if to use object pooling.")]
		[SerializeField]
		private bool _enabled = true;
		#endregion

		#region Private.
		/// <summary>
		/// Current count of the cache collection.
		/// </summary>
		private int _cacheCount = 0;
		/// <summary>
		/// When a NetworkObject is stored it's parent is set to this object.
		/// </summary>
		private Transform _objectParent;
		#endregion

		public override void InitializeOnce(NetworkManager nm)
		{
			base.InitializeOnce(nm);
			_objectParent = new GameObject().transform;
			_objectParent.name = "DefaultObjectPool Parent";
			_objectParent.transform.SetParent(nm.transform);
		}

		/// <summary>
		/// Returns an object that has been stored. A new object will be created if no stored objects are available.
		/// </summary>
		/// <param name="prefabId">PrefabId of the object to return.</param>
		/// <param name="collectionId">CollectionId of the object to return.</param>
		/// <param name="asServer">True if being called on the server side.</param>
		/// <returns></returns>
		public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, Transform parent = null, Vector3? nullableLocalPosition = null, Quaternion? nullableLocalRotation = null, Vector3? nullableLocalScale = null, bool makeActive = true, bool asServer = true)
		{
			if (!_enabled)
				return GetFromInstantiate();

			Stack<NetworkObject> cache = GetOrCreateCache(collectionId, prefabId);
			NetworkObject nob = null;

			//Iterate until nob is populated just in case cache entries have been destroyed.
			while (nob == null && cache.Count > 0)
			{
				nob = cache.Pop();
				if (nob != null)
				{
					nob.transform.SetParent(parent);
					nob.transform.SetLocalPositionRotationAndScale(nullableLocalPosition, nullableLocalRotation, nullableLocalScale);
					nob.gameObject.SetActive(false);

					IPooledResettable[] pooledResettables = nob.gameObject.GetComponents<IPooledResettable>();
					if (pooledResettables != null)
					{
						for (int i = 0; i < pooledResettables.Length; ++i)
						{
							pooledResettables[i].OnPooledReset();
						}
					}
					return nob;
				}
			}
			//Fall through, nothing in cache.
			return GetFromInstantiate();

			//Returns a network object via instantation.
			NetworkObject GetFromInstantiate()
			{
				NetworkObject prefab = GetPrefab(prefabId, collectionId, asServer);
				if (prefab == null)
				{
					return null;
				}
				else
				{
					prefab.transform.OutLocalPropertyValues(nullableLocalPosition, nullableLocalRotation, nullableLocalScale, out Vector3 pos, out Quaternion rot, out Vector3 scale);
					NetworkObject result = Instantiate(prefab, pos, rot, parent);
					result.transform.localScale = scale;
					result.gameObject.SetActive(false);

					IPooledResettable[] pooledResettables = result.gameObject.GetComponents<IPooledResettable>();
					if (pooledResettables != null)
					{
						for (int i = 0; i < pooledResettables.Length; ++i)
						{
							pooledResettables[i].OnPooledReset();
						}
					}
					return result;
				}
			}
		}
		/// <summary>
		/// Returns a prefab for prefab and collectionId.
		/// </summary>
		public override NetworkObject GetPrefab(int prefabId, ushort collectionId, bool asServer)
		{
			PrefabObjects po = base.NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, false);
			return po.GetObject(asServer, prefabId);
		}
		/// <summary>
		/// Stores an object into the pool.
		/// </summary>
		/// <param name="instantiated">Object to store.</param>
		/// <param name="asServer">True if being called on the server side.</param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override void StoreObject(NetworkObject instantiated, bool asServer)
		{
			//Pooling is not enabled.
			if (!_enabled || _objectParent == null)
			{
				Destroy(instantiated.gameObject);
				return;
			}

			instantiated.gameObject.SetActive(false);
			instantiated.ResetState();
			Stack<NetworkObject> cache = GetOrCreateCache(instantiated.SpawnableCollectionId, instantiated.PrefabId);
			instantiated.transform.SetParent(_objectParent);
			cache.Push(instantiated);
		}

		/// <summary>
		/// Instantiates a number of objects and adds them to the pool.
		/// </summary>
		/// <param name="prefab">Prefab to cache.</param>
		/// <param name="count">Quantity to spawn.</param>
		/// <param name="asServer">True if storing prefabs for the server collection. This is only applicable when using DualPrefabObjects.</param>
		public override void CacheObjects(NetworkObject prefab, int count, bool asServer)
		{
			if (!_enabled)
				return;
			if (count <= 0)
				return;
			if (prefab == null)
				return;
			if (prefab.PrefabId == NetworkObject.UNSET_PREFABID_VALUE)
			{
				NetworkManagerExtensions.LogError($"Pefab {prefab.name} has an invalid prefabId and cannot be cached.");
				return;
			}

			Stack<NetworkObject> cache = GetOrCreateCache(prefab.SpawnableCollectionId, prefab.PrefabId);
			for (int i = 0; i < count; i++)
			{
				NetworkObject nob = Instantiate(prefab);
				nob.gameObject.SetActive(false);
				cache.Push(nob);
			}
		}

		/// <summary>
		/// Clears pools destroying objects for all collectionIds
		/// </summary>
		public void ClearPool()
		{
			int count = _cache.Count;
			for (int i = 0; i < count; i++)
				ClearPool(i);
		}

		/// <summary>
		/// Clears a pool destroying objects for collectionId.
		/// </summary>
		/// <param name="collectionId">CollectionId to clear for.</param>
		public void ClearPool(int collectionId)
		{
			if (collectionId >= _cacheCount)
				return;

			Dictionary<int, Stack<NetworkObject>> dict = _cache[collectionId];
			foreach (Stack<NetworkObject> item in dict.Values)
			{
				while (item.Count > 0)
				{
					NetworkObject nob = item.Pop();
					if (nob != null)
						Destroy(nob.gameObject);
				}
			}

			dict.Clear();
		}

		/// <summary>
		/// Gets a cache for an id or creates one if does not exist.
		/// </summary>
		/// <param name="prefabId"></param>
		/// <returns></returns>
		private Stack<NetworkObject> GetOrCreateCache(int collectionId, int prefabId)
		{
			if (collectionId >= _cacheCount)
			{
				//Add more to the cache.
				while (_cache.Count <= collectionId)
				{
					Dictionary<int, Stack<NetworkObject>> dict = new Dictionary<int, Stack<NetworkObject>>();
					_cache.Add(dict);
				}
				_cacheCount = collectionId;
			}

			Dictionary<int, Stack<NetworkObject>> dictionary = _cache[collectionId];
			Stack<NetworkObject> cache;
			//No cache for prefabId yet, make one.
			if (!dictionary.TryGetValueIL2CPP(prefabId, out cache))
			{
				cache = new Stack<NetworkObject>();
				dictionary[prefabId] = cache;
			}
			return cache;
		}
	}


}