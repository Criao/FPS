using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FPSGame.Core;

namespace FPSGame.HotUpdate
{
    public class HotUpdateWeaponPickupLoader : MonoBehaviour
    {
        private const string FpsSceneName = "FPS";
        private const string BundleName = "submachine_gun.unity3d";
        private const string AssetName = "HotUpdateSubmachineGun";
        private const string PickupObjectName = "HotUpdateSubmachineGun_Pickup";
        private const string EquippedObjectName = "HotUpdateSubmachineGun_Equipped";
        private const int RequiredInstalledBuildNumber = 6;
        private const float SpawnDistance = 2.2f;
        private const float GroundSnapHeight = 0.55f;
        private const float PickupDistance = 2.5f;
        private const KeyCode PickupKey = KeyCode.F;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateLoader(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateLoader(scene);
        }

        private static void TryCreateLoader(Scene scene)
        {
            if (scene.name != FpsSceneName || FindObjectOfType<HotUpdateWeaponPickupLoader>() != null)
            {
                return;
            }

            GameObject loaderObject = new GameObject("HotUpdateWeaponPickupLoader");
            loaderObject.AddComponent<HotUpdateWeaponPickupLoader>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(0.2f);

            Weapon weapon = FindObjectOfType<Weapon>();
            if (weapon == null)
            {
                Debug.LogWarning("Hot update weapon pickup skipped because Weapon was not found in the FPS scene.");
                yield break;
            }

            if (!IsRequiredUpdateInstalled())
            {
                Debug.Log($"Hot update weapon pickup requires installed build {RequiredInstalledBuildNumber}.");
                yield break;
            }

            if (GameObject.Find(PickupObjectName) != null || GameObject.Find(EquippedObjectName) != null)
            {
                yield break;
            }

            yield return AssetBundleManager.Instance.Initialize();

            GameObject weaponPrefab = null;
            yield return AssetBundleManager.Instance.LoadAsset<GameObject>(
                BundleName,
                AssetName,
                loadedPrefab => weaponPrefab = loadedPrefab);

            if (weaponPrefab == null)
            {
                Debug.LogWarning($"Hot update submachine gun not loaded. Bundle: {BundleName}, Asset: {AssetName}");
                yield break;
            }

            SpawnPickup(weaponPrefab, weapon);
        }

        private static void SpawnPickup(GameObject weaponPrefab, Weapon weapon)
        {
            Transform reference = GetPlayerReference(weapon);
            Vector3 forward = GetHorizontalForward(reference);
            Vector3 spawnPosition = reference.position + forward * SpawnDistance + Vector3.up * GroundSnapHeight;

            if (Physics.Raycast(spawnPosition + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 5f, ~0, QueryTriggerInteraction.Ignore))
            {
                spawnPosition = groundHit.point + Vector3.up * GroundSnapHeight;
            }

            GameObject pickup = Instantiate(weaponPrefab, spawnPosition, Quaternion.LookRotation(forward, Vector3.up));
            pickup.name = PickupObjectName;
            pickup.transform.localScale = Vector3.one;

            HotUpdateWeaponPickup pickupComponent = pickup.AddComponent<HotUpdateWeaponPickup>();
            pickupComponent.Initialize(weapon, PickupKey, PickupDistance, EquippedObjectName);

            Debug.Log("Hot update submachine gun pickup loaded from AssetBundle.");
        }

        private static bool IsRequiredUpdateInstalled()
        {
            ConfigManager configManager = ConfigManager.Instance;
            return configManager != null &&
                configManager.AppVersion != null &&
                configManager.AppVersion.buildNumber >= RequiredInstalledBuildNumber;
        }

        private static Transform GetPlayerReference(Weapon weapon)
        {
            if (weapon != null && weapon.playerCamera != null)
            {
                return weapon.playerCamera.transform;
            }

            return weapon != null ? weapon.transform : Camera.main != null ? Camera.main.transform : null;
        }

        private static Vector3 GetHorizontalForward(Transform reference)
        {
            Vector3 forward = reference != null ? reference.forward : Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                return Vector3.forward;
            }

            return forward.normalized;
        }
    }

    public class HotUpdateWeaponPickup : MonoBehaviour
    {
        private const string MuzzleName = "Muzzle";
        private const string PromptObjectName = "PickupPrompt";

        private Weapon weapon;
        private KeyCode pickupKey;
        private float pickupDistance;
        private string equippedObjectName;
        private Transform muzzle;
        private GameObject promptObject;
        private bool isPickedUp;

        public void Initialize(Weapon targetWeapon, KeyCode key, float distance, string equipName)
        {
            weapon = targetWeapon;
            pickupKey = key;
            pickupDistance = Mathf.Max(0.5f, distance);
            equippedObjectName = equipName;
            muzzle = FindChildRecursive(transform, MuzzleName);

            EnsurePickupTrigger();
            CreatePrompt();
        }

        private void Update()
        {
            if (isPickedUp || weapon == null)
            {
                return;
            }

            Transform reference = GetPlayerReference();
            float pickupDistanceSqr = pickupDistance * pickupDistance;
            bool isInRange = (reference.position - transform.position).sqrMagnitude <= pickupDistanceSqr;

            if (promptObject != null && promptObject.activeSelf != isInRange)
            {
                promptObject.SetActive(isInRange);
            }

            if (isInRange)
            {
                UpdatePromptBillboard();
            }

            if (isInRange && Input.GetKeyDown(pickupKey))
            {
                Equip();
            }
        }

        private void Equip()
        {
            if (isPickedUp)
            {
                return;
            }

            isPickedUp = true;

            if (promptObject != null)
            {
                Destroy(promptObject);
            }

            RemovePickupPhysics();
            gameObject.name = equippedObjectName;
            weapon.PickupPrimaryWeaponModel(gameObject, muzzle);
            enabled = false;
        }

        private void EnsurePickupTrigger()
        {
            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = pickupDistance;
        }

        private void CreatePrompt()
        {
            promptObject = new GameObject(PromptObjectName);
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            promptObject.transform.localScale = Vector3.one;

            TextMesh promptText = promptObject.AddComponent<TextMesh>();
            promptText.text = "Press F";
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.fontSize = 48;
            promptText.characterSize = 0.035f;
            promptText.color = new Color(0.9f, 1f, 1f, 1f);

            promptObject.SetActive(false);
        }

        private void UpdatePromptBillboard()
        {
            if (promptObject == null)
            {
                return;
            }

            Camera camera = weapon != null && weapon.playerCamera != null ? weapon.playerCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 direction = promptObject.transform.position - camera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                promptObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void RemovePickupPhysics()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Destroy(rigidbodies[i]);
            }
        }

        private Transform GetPlayerReference()
        {
            if (weapon != null && weapon.playerCamera != null)
            {
                return weapon.playerCamera.transform;
            }

            return weapon != null ? weapon.transform : transform;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
