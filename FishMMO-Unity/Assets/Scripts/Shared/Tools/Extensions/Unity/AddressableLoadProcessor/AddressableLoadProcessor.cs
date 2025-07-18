using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace FishMMO.Shared
{
	public static class AddressableLoadProcessor
	{
		private static AddressableLoadHelper helper;

		static AddressableLoadProcessor()
		{
			if (helper == null)
			{
				GameObject helperObj = new GameObject("AddressableLoadHelper");
				helper = helperObj.AddComponent<AddressableLoadHelper>();
			}
		}

		public class AddressableLoadHelper : MonoBehaviour
		{
			void Awake()
			{
				DontDestroyOnLoad(this.gameObject);
			}

			void OnDestroy()
			{
				AddressableLoadProcessor.ReleaseAllAssets();
			}
		}

		/// <summary>
		/// Currently loaded prefab assets.
		/// </summary>
		private static Dictionary<object, AsyncOperationHandle<GameObject>> loadedPrefabs = new Dictionary<object, AsyncOperationHandle<GameObject>>();
		private static Dictionary<object, AsyncOperationHandle<GameObject>> currentPrefabOperations = new Dictionary<object, AsyncOperationHandle<GameObject>>();

		/// <summary>
		/// The currently loaded assets.
		/// </summary>
		private static Dictionary<AddressableAssetKey, AsyncOperationHandle<IList<UnityEngine.Object>>> loadedAssets = new Dictionary<AddressableAssetKey, AsyncOperationHandle<IList<UnityEngine.Object>>>();
		/// <summary>
		/// The currently loaded scenes.
		/// </summary>
		private static Dictionary<string, AsyncOperationHandle<SceneInstance>> loadedScenes = new Dictionary<string, AsyncOperationHandle<SceneInstance>>();
		/// <summary>
		/// The load request operations.
		/// </summary>
		private static HashSet<AddressableAssetKey> operationQueue = new HashSet<AddressableAssetKey>();
		/// <summary>
		/// The scene load request operations.
		/// </summary>
		private static HashSet<AddressableSceneLoadData> sceneOperationQueue = new HashSet<AddressableSceneLoadData>();
		/// <summary>
		/// The current operation queue. These operations are waiting to complete.
		/// </summary>
		private static Dictionary<AddressableAssetKey, AsyncOperationHandle> currentOperations = new Dictionary<AddressableAssetKey, AsyncOperationHandle>();
		/// <summary>
		/// The current scene operation queue. These operations are waiting to complete.
		/// </summary>
		private static Dictionary<AddressableSceneLoadData, AsyncOperationHandle<SceneInstance>> currentSceneOperations = new Dictionary<AddressableSceneLoadData, AsyncOperationHandle<SceneInstance>>();

		/// <summary>
		/// Reports the current Load Queue progress.
		/// </summary>
		public static Action<float> OnProgressUpdate;
		/// <summary>
		/// Invoked when an Addressable (non scene) is loaded.
		/// </summary>
		public static Action<UnityEngine.Object> OnAddressableLoaded;
		/// <summary>
		/// Invoked when an Addressable (non scene) is unloaded.
		/// </summary>
		public static Action<UnityEngine.Object> OnAddressableUnloaded;
		/// <summary>
		/// Invoked when an Addressable Scene is loaded.
		/// </summary>
		public static Action<Scene> OnSceneLoaded;
		/// <summary>
		/// Invoked when an Addressable Scene is unloaded.
		/// </summary>
		public static Action<string> OnSceneUnloaded;

		private static float assetsProcessedSoFar;
		private static bool isProcessingQueue = false;

		public static float CurrentProgress
		{
			get
			{
				return (assetsProcessedSoFar > 0) ? assetsProcessedSoFar / RemainingAssetsToLoad : 0;
			}
		}

		public static float RemainingAssetsToLoad
		{
			get
			{
				return operationQueue.Count + sceneOperationQueue.Count + currentOperations.Count + currentSceneOperations.Count;
			}
		}

		// Enqueue a single label (string)
		public static void EnqueueLoad(string label, Addressables.MergeMode mergeMode = Addressables.MergeMode.None)
		{
			AddressableAssetKey assetKey = new AddressableAssetKey(new List<string>() { label, }, mergeMode);
			if (!operationQueue.Contains(assetKey) && !currentOperations.ContainsKey(assetKey) && !loadedAssets.ContainsKey(assetKey))
			{
				//Log.Debug($"Enqueued: {assetKey}");
				operationQueue.Add(assetKey);
			}
		}

		// Enqueue multiple labels (IEnumerable<string>)
		public static void EnqueueLoad(IEnumerable<string> labels, Addressables.MergeMode mergeMode = Addressables.MergeMode.None)
		{
			if (labels == null || !labels.Any())
			{
				return;
			}
			foreach (var label in labels)
			{
				EnqueueLoad(label, mergeMode);
			}
		}

		// Enqueue a label with a key (KeyValuePair<string, string>)
		public static void EnqueueLoad(string label, string key, Addressables.MergeMode mergeMode = Addressables.MergeMode.Intersection)
		{
			AddressableAssetKey assetKey = new AddressableAssetKey(new List<string>() { label, key, }, mergeMode);
			if (!operationQueue.Contains(assetKey) && !currentOperations.ContainsKey(assetKey) && !loadedAssets.ContainsKey(assetKey))
			{
				//Log.Debug($"Enqueued: {assetKey}");
				operationQueue.Add(assetKey);
			}
		}

		// Enqueue multiple label-key pairs (IEnumerable<KeyValuePair<string, string>>)
		public static void EnqueueLoad(IEnumerable<KeyValuePair<string, string>> labels, Addressables.MergeMode mergeMode = Addressables.MergeMode.Intersection)
		{
			if (labels == null || !labels.Any())
			{
				return;
			}
			foreach (var pair in labels)
			{
				EnqueueLoad(pair.Key, pair.Value, mergeMode);
			}
		}

		// Enqueue a single scene (string)
		public static void EnqueueLoad(AddressableSceneLoadData sceneLoadData)
		{
			if (!sceneOperationQueue.Contains(sceneLoadData) && !currentSceneOperations.ContainsKey(sceneLoadData) && !loadedScenes.ContainsKey(sceneLoadData.SceneName))
			{
				//Log.Debug($"Enqueued: {sceneLoadData.SceneName}");
				AsyncOperationHandle<SceneInstance> operation = LoadSceneByLabelAsync(sceneLoadData);
				currentSceneOperations.Add(sceneLoadData, operation);
			}
		}

		public static void EnqueueLoad(IEnumerable<AddressableSceneLoadData> sceneLoadDatas)
		{
			if (sceneLoadDatas == null || !sceneLoadDatas.Any())
			{
				return;
			}
			foreach (var sceneLoadData in sceneLoadDatas)
			{
				EnqueueLoad(sceneLoadData); // Reusing the single enqueue method
			}
		}

		public static void BeginProcessQueue()
		{
			if (isProcessingQueue)
			{
				return;
			}

			// Make sure the remaining assets to load are greater than 0
			if (RemainingAssetsToLoad > 0)
			{
				//Log.Debug($"BeginProcessQueue");
				isProcessingQueue = true;
				helper.StartCoroutine(ProcessLoadQueue());
			}
			// The queue was empty so notify that everything is done.
			else
			{
				OnProgressUpdate?.Invoke(1f);
			}
		}

		public static IEnumerator ProcessLoadQueue()
		{
			// Reset and report that the progress is currently zero.
			OnProgressUpdate?.Invoke(0f);
			assetsProcessedSoFar = 0;

			while (RemainingAssetsToLoad > 0)
			{
				// Process assets from the operationQueue if any are present
				while (operationQueue.Count > 0)
				{
					var assetKey = operationQueue.First(); // Take the first asset in the queue
					operationQueue.Remove(assetKey); // Remove it from the queue

					Log.Debug($"Loading: {assetKey}");

					AsyncOperationHandle<IList<UnityEngine.Object>> operation = LoadAssetsAsync(assetKey);
					currentOperations.Add(assetKey, operation);

					// Yield after starting the operation so the next frame can process other queued items
					yield return operation;
				}

				// Process scenes from the sceneOperationQueue if any are present
				while (sceneOperationQueue.Count > 0)
				{
					var sceneAssetKey = sceneOperationQueue.First(); // Take the first scene in the queue
					sceneOperationQueue.Remove(sceneAssetKey); // Remove it from the queue

					Log.Debug($"Scene Loading: {sceneAssetKey.SceneName}");

					AsyncOperationHandle<SceneInstance> operation = LoadSceneByLabelAsync(sceneAssetKey);
					currentSceneOperations.Add(sceneAssetKey, operation);

					// Yield after starting the operation so the next frame can process other queued items
					yield return operation;
				}

				// Yield back to Unity for the next frame to ensure the queue can be checked again
				yield return null;
			}

			//Log.Debug($"Processing Completed: {assetsProcessedSoFar} - {RemainingAssetsToLoad}");

			// Mark that the queue processing is complete
			isProcessingQueue = false;

			// Ensure progress is reported as completed.
			OnProgressUpdate?.Invoke(1f);
		}

		public static void LoadPrefabAsync(AssetReference assetReference, Action<GameObject> onLoadComplete)
		{
			if (assetReference == null || !assetReference.RuntimeKeyIsValid())
			{
				onLoadComplete?.Invoke(null);
				return;
			}

			object key = assetReference.RuntimeKey;

			if (loadedPrefabs.TryGetValue(key, out AsyncOperationHandle<GameObject> completedHandle))
			{
				if (completedHandle.Status == AsyncOperationStatus.Succeeded)
				{
					onLoadComplete?.Invoke(completedHandle.Result);
					return;
				}
			}

			if (currentPrefabOperations.TryGetValue(key, out AsyncOperationHandle<GameObject> existingHandle))
			{
				if (existingHandle.IsValid())
				{
					if (existingHandle.IsDone)
					{
						//Log.Debug($"Reusing completed existing handle for {key}");
						if (existingHandle.Status == AsyncOperationStatus.Succeeded)
						{
							onLoadComplete?.Invoke(existingHandle.Result);
						}
						else
						{
							onLoadComplete?.Invoke(null);
						}
						return;
					}
					else
					{
						//Log.Debug($"Attaching new callback to in-progress load for {key}");
						existingHandle.Completed += (op) =>
						{
							if (op.Status == AsyncOperationStatus.Succeeded)
							{
								onLoadComplete?.Invoke(op.Result);
							}
							else
							{
								onLoadComplete?.Invoke(null);
							}
						};
						return;
					}
				}
				else
				{
					Log.Warning($"Found invalid handle for {key} in currentPrefabOperations. Removing and restarting load.");
					currentPrefabOperations.Remove(key);
				}
			}

			AsyncOperationHandle<GameObject> newHandle = assetReference.LoadAssetAsync<GameObject>();

			currentPrefabOperations.Add(key, newHandle);

			newHandle.Completed += (op) =>
			{
				if (op.Status == AsyncOperationStatus.Succeeded)
				{
					Log.Debug($"New Load Complete: {op.Result.name}");
					loadedPrefabs[key] = op;
					onLoadComplete?.Invoke(op.Result);
				}
				else
				{
					Log.Error($"Failed to load addressable {key}: {op.OperationException?.Message}");
					if (loadedPrefabs.ContainsKey(key))
					{
						loadedPrefabs.Remove(key);
					}
					onLoadComplete?.Invoke(null);
				}

				currentPrefabOperations.Remove(key);
			};
		}

		public static void UnloadPrefab(AssetReference assetReference)
		{
			if (assetReference == null) return;

			object key = assetReference.RuntimeKey;

			// Ensure the asset is not currently loading
			if (currentPrefabOperations.ContainsKey(key))
			{
				Log.Warning($"Attempted to unload prefab {key} while it's still loading. Unload will be attempted after current load finishes.");
				return;
			}

			if (loadedPrefabs.TryGetValue(key, out var assetHandle))
			{
				if (assetHandle.IsValid())
				{
					Log.Debug($"Unloading prefab: {(assetHandle.Result != null ? assetHandle.Result.name : key)}");
					Addressables.Release(assetHandle);
					loadedPrefabs.Remove(key);
					Log.Debug($"Prefab with key {key} has been unloaded.");
				}
				else
				{
					Log.Warning($"Asset handle for {key} is invalid. Removing from loadedPrefabs.");
					loadedPrefabs.Remove(key);
				}
			}
			else
			{
				Log.Warning($"Prefab with key {key} not found in loaded prefabs. Not currently managed by this processor or already unloaded.");
			}
		}

		// Load assets for a specific label
		private static AsyncOperationHandle<IList<UnityEngine.Object>> LoadAssetsAsync(AddressableAssetKey assetkey)
		{
			if (assetkey == null || assetkey.Keys == null || assetkey.Keys.Count < 1)
			{
				return default;
			}

			var handle = Addressables.LoadAssetsAsync<UnityEngine.Object>(assetkey.Keys, null, assetkey.MergeMode, false);
			handle.Completed += (op) =>
			{
				if (op.Status == AsyncOperationStatus.Succeeded)
				{
					currentOperations.Remove(assetkey);

					foreach (var asset in op.Result)
					{
						OnAddressableLoaded?.Invoke(asset);

						//Log.Debug($"Load Complete: {asset.name}");
					}

					if (!loadedAssets.ContainsKey(assetkey))
					{
						loadedAssets.Add(assetkey, op);
					}
				}
				else if (op.Status == AsyncOperationStatus.Failed)
				{
					Log.Error($"Failed to load Addressable: {assetkey.Keys}");
					op.Release();
				}

				assetsProcessedSoFar++;
				UpdateProgress();
			};
			return handle;
		}

		// Method to load the scene with a specific label
		private static AsyncOperationHandle<SceneInstance> LoadSceneByLabelAsync(AddressableSceneLoadData sceneLoadData)
		{
			if (loadedScenes.ContainsKey(sceneLoadData.SceneName))
			{
				return default;
			}

			var handle = Addressables.LoadSceneAsync(sceneLoadData.SceneName, sceneLoadData.LoadSceneMode, sceneLoadData.ActivateOnLoad);
			handle.Completed += (op) =>
			{
				if (op.Status == AsyncOperationStatus.Succeeded)
				{
					Scene loadedScene = op.Result.Scene;

					if (loadedScenes.ContainsKey(loadedScene.name))
					{
						op.Release();
						return;
					}

					currentSceneOperations.Remove(sceneLoadData);

					//Log.Debug($"Scene Load Complete: {sceneLoadData.SceneName}");

					loadedScenes.Add(loadedScene.name, op);
					sceneLoadData.OnSceneLoaded?.Invoke(loadedScene);
					OnSceneLoaded?.Invoke(loadedScene);
				}
				else if (op.Status == AsyncOperationStatus.Failed)
				{
					Log.Error($"Failed to load scene Addressable: {sceneLoadData.SceneName}");
					op.Release();
				}

				assetsProcessedSoFar++;
				UpdateProgress();
			};

			return handle;
		}

		/// <summary>
		/// Unload a specific asset by its key.
		/// </summary>
		public static void UnloadAssetByKey(AddressableAssetKey assetKey)
		{
			// Check if the asset is loaded (exists in the loadedAssets dictionary)
			if (loadedAssets.TryGetValue(assetKey, out var assetHandle))
			{
				// Ensure the asset handle is valid
				if (assetHandle.IsValid())
				{
					// Iterate over the assets and release each one
					foreach (var asset in assetHandle.Result)
					{
						// Trigger the callback for unloading the asset
						OnAddressableUnloaded?.Invoke(asset);

						// Log the unloading action for debugging purposes
						Log.Debug($"Unloading asset: {asset.name}");
					}

					// Release the asset handle and remove it from the loadedAssets dictionary
					Addressables.Release(assetHandle);
					loadedAssets.Remove(assetKey);

					Log.Debug($"Asset with key {assetKey} has been unloaded.");
				}
				else
				{
					Log.Error($"Asset handle for {assetKey} is invalid.");
				}
			}
			else
			{
				// Log if the asset key was not found in the loaded assets dictionary
				Log.Warning($"Asset with key {assetKey} not found in loaded assets.");
			}
		}

		public static void UnloadSceneByLabelAsync(List<string> sceneNames)
		{
			foreach (string sceneName in sceneNames)
			{
				UnloadSceneByLabelAsync(sceneName);
			}
		}

		public static void UnloadSceneByLabelAsync(List<AddressableSceneLoadData> sceneLoadData)
		{
			foreach (AddressableSceneLoadData scene in sceneLoadData)
			{
				UnloadSceneByLabelAsync(scene.SceneName);
			}
		}

		public static void UnloadSceneByLabelAsync(string sceneName)
		{
			// Check if the scene is already loaded
			if (!loadedScenes.TryGetValue(sceneName, out var handle))
			{
				//Log.Warning($"Scene {sceneName} not found in loaded scenes.");
				return;
			}

			// Unload the scene asynchronously
			AsyncOperationHandle unloadHandle = Addressables.UnloadSceneAsync(handle, true);

			unloadHandle.Completed += (op) =>
			{
				if (op.Status == AsyncOperationStatus.Succeeded)
				{
					// Remove the scene from the loadedScenes dictionary
					loadedScenes.Remove(sceneName);

					// Try to release the cached handle.
					if (handle.IsValid())
					{
						Addressables.Release(handle);
					}

					//Log.Debug($"AddressableLoadProcessor Successfully unloaded scene {sceneName}");

					// Invoke the unload callback
					OnSceneUnloaded?.Invoke(sceneName);
				}
				else if (op.Status == AsyncOperationStatus.Failed)
				{
					// Log an error if unloading the scene failed
					Log.Error($"Failed to unload scene Addressable: {sceneName}");
				}
			};
		}

		private static void UpdateProgress()
		{
			float progress = CurrentProgress;
			if (progress < 1.0f)
			{
				OnProgressUpdate?.Invoke(progress);
			}
		}

		// Release all loaded assets
		public static void ReleaseAllAssets()
		{
			if (helper != null)
			{
				helper.StopAllCoroutines();
			}

			foreach (var sceneHandle in loadedScenes.Values)
			{
				if (sceneHandle.IsValid())
				{
					OnSceneUnloaded?.Invoke(sceneHandle.Result.Scene.name);

					Addressables.Release(sceneHandle);
				}
			}
			loadedScenes.Clear();

			foreach (var assetList in loadedAssets.Values)
			{
				if (assetList.IsValid())
				{
					foreach (var asset in assetList.Result)
					{
						OnAddressableUnloaded?.Invoke(asset);
					}
					Addressables.Release(assetList);
				}
			}
			loadedAssets.Clear();

			foreach (var prefabHandle in loadedPrefabs.Values)
			{
				if (prefabHandle.IsValid())
				{
					Addressables.Release(prefabHandle);
				}
			}
			loadedPrefabs.Clear();
		}
	}
}