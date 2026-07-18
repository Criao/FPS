using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum ShootingMode
{
    Single,
    Auto
}

public enum WeaponSlot
{
    Primary = 1,
    Secondary = 2
}

public class Weapon : MonoBehaviour
{
    private const string FirstPersonWeaponModelName = "FirstPersonGunModel";
    private const string MuzzleName = "Muzzle";
    private const int InitialShotTracerPoolSize = 16;
    private const int MaxShotTracerPoolSize = 48;
    private const int DefaultPrimaryMagazineSize = 25;
    private const int DefaultPrimaryTotalMagazines = 3;
    private const int DefaultSecondaryMagazineSize = 7;
    private const int DefaultSecondaryTotalMagazines = 3;
    private const float DefaultPrimaryReloadSeconds = 3.5f;
    private const float DefaultSecondaryReloadSeconds = 2f;
    private static readonly Vector3 EquippedModelLocalPosition = new Vector3(0.34f, -0.32f, 0.68f);
    private static readonly Vector3 EquippedModelLocalEulerAngles = new Vector3(-2f, -3f, 0f);

    [Header("References")]
    public Camera playerCamera;
    [SerializeField] private Transform bulletSpawnPoint;

    [Header("Weapon Slots")]
    [SerializeField] private KeyCode primarySlotKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode secondarySlotKey = KeyCode.Alpha2;
    [SerializeField] private int primaryMagazineSize = DefaultPrimaryMagazineSize;
    [SerializeField] private int primaryTotalMagazines = DefaultPrimaryTotalMagazines;
    [SerializeField] private int secondaryMagazineSize = DefaultSecondaryMagazineSize;
    [SerializeField] private int secondaryTotalMagazines = DefaultSecondaryTotalMagazines;
    [SerializeField] private float primaryReloadSeconds = DefaultPrimaryReloadSeconds;
    [SerializeField] private float secondaryReloadSeconds = DefaultSecondaryReloadSeconds;

    [Header("Shooting Mode")]
    public ShootingMode currentShootingMode = ShootingMode.Single;
    [SerializeField] private KeyCode switchModeKey = KeyCode.B;
    [SerializeField] private bool primaryAllowsAuto = true;

    [Header("Shot Feedback")]
    [SerializeField] private bool showShotTracer = true;
    [SerializeField] private Material shotTracerMaterial;
    [SerializeField] private float tracerDuration = 0.06f;
    [SerializeField] private float tracerStartWidth = 0.035f;
    [SerializeField] private float tracerEndWidth = 0.005f;
    [SerializeField] private Color tracerStartColor = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] private Color tracerEndColor = new Color(1f, 0.35f, 0.05f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioClip shotSound;
    [SerializeField] private AudioClip autoFireSound;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField] private AudioSource shotAudioSource;
    [SerializeField] private AudioSource autoFireAudioSource;
    [SerializeField] private AudioSource reloadAudioSource;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float autoFireVolume = 0.78f;
    [SerializeField, Range(0f, 1f)] private float reloadVolume = 0.75f;
    [SerializeField] private Vector2 shotPitchRange = new Vector2(0.96f, 1.04f);

    [Header("Ammo")]
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Hitscan")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float raycastDistance = 300f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private bool ignoreOwnColliders = true;
    [SerializeField] private Transform ignoredHitRoot;
    [SerializeField] private bool excludePlayerLayerFromHits = true;
    [SerializeField] private float autoEmptyMagazineSeconds = 6f;
    [SerializeField] private float spreadIntensity;

    [Header("Impact")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private bool useRoundBulletHoles = true;
    [SerializeField] private float bulletHoleSize = 0.1f;
    [SerializeField] private float minRoundBulletHoleSize = 0.035f;
    [SerializeField] private float maxRoundBulletHoleSize = 0.12f;
    [SerializeField] private float bulletHoleSurfaceOffset = 0.015f;
    [SerializeField] private float bulletHoleLifetime = 10f;
    [SerializeField] private float hitImpulse = 16f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TMP_FontAsset ammoFont;
    [SerializeField] private bool createAmmoUIIfMissing = true;
    [SerializeField] private Vector2 ammoUiOffset = new Vector2(-32f, 32f);
    [SerializeField] private int ammoFontSize = 28;

    private WeaponRuntimeState primaryWeapon;
    private WeaponRuntimeState secondaryWeapon;
    private WeaponRuntimeState activeWeapon;
    private WeaponSlot activeSlot = WeaponSlot.Secondary;
    private float nextAutoShotTime;
    private readonly RaycastHit[] raycastHitBuffer = new RaycastHit[32];
    private static Material fallbackTracerMaterial;
    private static Material fallbackBulletHoleMaterial;
    private static Transform shotTracerPoolRoot;
    private static int nextShotTracerReuseIndex;
    private static readonly List<PooledShotTracer> shotTracerPool = new List<PooledShotTracer>(MaxShotTracerPoolSize);

    public int CurrentAmmo => activeWeapon != null ? activeWeapon.CurrentAmmo : 0;
    public int TotalAmmo => activeWeapon != null ? activeWeapon.TotalAmmo : 0;
    public int RemainingMagazineCount => activeWeapon != null ? activeWeapon.RemainingMagazineCount : 0;

    private float AutoFireInterval
    {
        get
        {
            int activeMagazineSize = activeWeapon != null ? activeWeapon.MagazineSize : 1;
            if (activeMagazineSize <= 1)
            {
                return 0.01f;
            }

            return Mathf.Max(0.01f, autoEmptyMagazineSeconds / (activeMagazineSize - 1));
        }
    }

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        ApplyFixedWeaponSettings();
        autoEmptyMagazineSeconds = Mathf.Max(0.01f, autoEmptyMagazineSeconds);
        raycastDistance = Mathf.Max(1f, raycastDistance);
        ignoredHitRoot = ignoredHitRoot != null ? ignoredHitRoot : ResolveIgnoredHitRoot();
        EnsureWeaponAudioSources();
        InitializeWeaponSlots();

        if (useRoundBulletHoles)
        {
            BulletHoleDecal.Prewarm();
        }

        if (showShotTracer)
        {
            PrewarmShotTracers();
        }

        EnsureAmmoUI();
        UpdateAmmoUI();
    }

    private void ApplyFixedWeaponSettings()
    {
        primaryMagazineSize = DefaultPrimaryMagazineSize;
        primaryTotalMagazines = DefaultPrimaryTotalMagazines;
        primaryReloadSeconds = DefaultPrimaryReloadSeconds;

        secondaryMagazineSize = DefaultSecondaryMagazineSize;
        secondaryTotalMagazines = DefaultSecondaryTotalMagazines;
        secondaryReloadSeconds = DefaultSecondaryReloadSeconds;
    }

    private void Update()
    {
        HandleWeaponSlotSwitch();
        HandleModeSwitch();
        HandleReload();
        HandleShooting();
    }

    private void InitializeWeaponSlots()
    {
        Transform modelParent = playerCamera != null ? playerCamera.transform : transform;
        Transform secondaryModel = modelParent != null ? modelParent.Find(FirstPersonWeaponModelName) : null;
        Transform secondaryMuzzle = bulletSpawnPoint != null
            ? bulletSpawnPoint
            : FindChildRecursive(secondaryModel, MuzzleName);

        primaryWeapon = new WeaponRuntimeState(
            WeaponSlot.Primary,
            "SMG",
            primaryMagazineSize,
            primaryTotalMagazines,
            primaryReloadSeconds,
            primaryAllowsAuto);

        secondaryWeapon = new WeaponRuntimeState(
            WeaponSlot.Secondary,
            "PISTOL",
            secondaryMagazineSize,
            secondaryTotalMagazines,
            secondaryReloadSeconds,
            false)
        {
            IsUnlocked = true,
            Model = secondaryModel,
            Muzzle = secondaryMuzzle
        };

        TryEquipWeaponSlot(WeaponSlot.Secondary, false);
        Debug.Log($"Weapon slots initialized. Pistol ammo: {secondaryWeapon.CurrentAmmo}, reserve: {secondaryWeapon.ReserveAmmo}. SMG ammo: {primaryWeapon.CurrentAmmo}, reserve: {primaryWeapon.ReserveAmmo}.");
    }

    private void HandleWeaponSlotSwitch()
    {
        if (Input.GetKeyDown(primarySlotKey))
        {
            TryEquipWeaponSlot(WeaponSlot.Primary, true);
        }

        if (Input.GetKeyDown(secondarySlotKey))
        {
            TryEquipWeaponSlot(WeaponSlot.Secondary, true);
        }
    }

    private void HandleModeSwitch()
    {
        if (!Input.GetKeyDown(switchModeKey))
        {
            return;
        }

        if (activeWeapon == null)
        {
            return;
        }

        if (!activeWeapon.AllowsAuto)
        {
            activeWeapon.ShootingMode = ShootingMode.Single;
            currentShootingMode = ShootingMode.Single;
            Debug.Log("Current weapon only supports single fire.");
            UpdateAmmoUI();
            return;
        }

        activeWeapon.ShootingMode = activeWeapon.ShootingMode == ShootingMode.Single
            ? ShootingMode.Auto
            : ShootingMode.Single;
        currentShootingMode = activeWeapon.ShootingMode;

        Debug.Log("Switched shooting mode: " + GetModeLabel());
        UpdateAmmoUI();
    }

    private void HandleReload()
    {
        if (Input.GetKeyDown(reloadKey))
        {
            TryReload();
        }
    }

    private void HandleShooting()
    {
        if (activeWeapon == null)
        {
            return;
        }

        switch (activeWeapon.ShootingMode)
        {
            case ShootingMode.Single:
                if (Input.GetButtonDown("Fire1"))
                {
                    TryShoot();
                }
                break;

            case ShootingMode.Auto:
                if (activeWeapon.AllowsAuto && Input.GetButton("Fire1") && Time.time >= nextAutoShotTime)
                {
                    TryShoot();
                    nextAutoShotTime = Time.time + AutoFireInterval;
                }
                break;
        }
    }

    private bool TryShoot()
    {
        if (activeWeapon == null)
        {
            return false;
        }

        if (activeWeapon.IsReloading)
        {
            return false;
        }

        if (activeWeapon.CurrentAmmo <= 0)
        {
            if (!TryReload())
            {
                Debug.Log("Out of ammo");
            }

            UpdateAmmoUI();
            return false;
        }

        activeWeapon.CurrentAmmo--;
        PlayShotSound();
        FireHitscan();

        if (activeWeapon.CurrentAmmo <= 0 && activeWeapon.ReserveAmmo > 0)
        {
            TryReload();
        }

        UpdateAmmoUI();

        return true;
    }

    private bool TryReload()
    {
        return TryStartReload(activeWeapon);
    }

    private bool TryStartReload(WeaponRuntimeState weaponState)
    {
        if (weaponState == null ||
            weaponState.IsReloading ||
            weaponState.CurrentAmmo >= weaponState.MagazineSize ||
            weaponState.ReserveAmmo <= 0)
        {
            UpdateAmmoUI();
            return false;
        }

        weaponState.IsReloading = true;
        PlayReloadSound(weaponState);
        UpdateAmmoUI();
        StartCoroutine(ReloadAfterDelay(weaponState));
        return true;
    }

    private IEnumerator ReloadAfterDelay(WeaponRuntimeState weaponState)
    {
        yield return new WaitForSeconds(weaponState.ReloadSeconds);

        int neededAmmo = weaponState.MagazineSize - weaponState.CurrentAmmo;
        int loadedAmmo = Mathf.Min(neededAmmo, weaponState.ReserveAmmo);

        weaponState.CurrentAmmo += loadedAmmo;
        weaponState.ReserveAmmo -= loadedAmmo;
        weaponState.IsReloading = false;
        UpdateAmmoUI();
    }

    public void PickupPrimaryWeaponModel(GameObject hotUpdateModel, Transform muzzle)
    {
        if (hotUpdateModel == null)
        {
            return;
        }

        Transform modelParent = playerCamera != null ? playerCamera.transform : transform;
        if (primaryWeapon == null)
        {
            primaryWeapon = new WeaponRuntimeState(
                WeaponSlot.Primary,
                "SMG",
                primaryMagazineSize,
                primaryTotalMagazines,
                primaryReloadSeconds,
                primaryAllowsAuto);
        }

        if (primaryWeapon.Model != null && primaryWeapon.Model.gameObject != hotUpdateModel)
        {
            Destroy(primaryWeapon.Model.gameObject);
        }

        hotUpdateModel.transform.SetParent(modelParent, false);
        hotUpdateModel.transform.localPosition = EquippedModelLocalPosition;
        hotUpdateModel.transform.localRotation = Quaternion.Euler(EquippedModelLocalEulerAngles);
        hotUpdateModel.transform.localScale = Vector3.one;

        SetLayerRecursive(hotUpdateModel.transform, GetPlayerLayerOrCurrent(gameObject.layer));

        Transform muzzleTransform = muzzle != null ? muzzle : FindChildRecursive(hotUpdateModel.transform, MuzzleName);
        primaryWeapon.Model = hotUpdateModel.transform;
        primaryWeapon.Muzzle = muzzleTransform;
        primaryWeapon.IsUnlocked = true;
        primaryWeapon.ShootingMode = ShootingMode.Single;
        primaryWeapon.ConfigureAmmo(primaryMagazineSize, primaryTotalMagazines);

        TryEquipWeaponSlot(WeaponSlot.Primary, true);
    }

    private bool TryEquipWeaponSlot(WeaponSlot targetSlot, bool logIfUnavailable)
    {
        WeaponRuntimeState targetWeapon = GetWeaponState(targetSlot);
        if (targetWeapon == null || !targetWeapon.IsUnlocked)
        {
            if (logIfUnavailable)
            {
                Debug.Log(targetSlot == WeaponSlot.Primary
                    ? "Primary weapon slot is empty."
                    : "Secondary weapon slot is empty.");
            }

            UpdateAmmoUI();
            return false;
        }

        activeSlot = targetSlot;
        activeWeapon = targetWeapon;
        ApplyActiveWeapon();
        UpdateAmmoUI();
        return true;
    }

    private WeaponRuntimeState GetWeaponState(WeaponSlot slot)
    {
        return slot == WeaponSlot.Primary ? primaryWeapon : secondaryWeapon;
    }

    private void ApplyActiveWeapon()
    {
        SetWeaponModelActive(primaryWeapon, activeWeapon == primaryWeapon);
        SetWeaponModelActive(secondaryWeapon, activeWeapon == secondaryWeapon);

        if (activeWeapon == null)
        {
            bulletSpawnPoint = null;
            currentShootingMode = ShootingMode.Single;
            return;
        }

        if (!activeWeapon.AllowsAuto)
        {
            activeWeapon.ShootingMode = ShootingMode.Single;
        }

        bulletSpawnPoint = activeWeapon.Muzzle;
        currentShootingMode = activeWeapon.ShootingMode;
        nextAutoShotTime = 0f;
    }

    private static void SetWeaponModelActive(WeaponRuntimeState weaponState, bool isActive)
    {
        if (weaponState != null && weaponState.Model != null)
        {
            weaponState.Model.gameObject.SetActive(isActive);
        }
    }

    private sealed class WeaponRuntimeState
    {
        public WeaponRuntimeState(
            WeaponSlot slot,
            string displayName,
            int magazineSize,
            int totalMagazines,
            float reloadSeconds,
            bool allowsAuto)
        {
            Slot = slot;
            DisplayName = displayName;
            AllowsAuto = allowsAuto;
            ReloadSeconds = Mathf.Max(0.01f, reloadSeconds);
            ShootingMode = ShootingMode.Single;
            ConfigureAmmo(magazineSize, totalMagazines);
        }

        public WeaponSlot Slot { get; }
        public string DisplayName { get; }
        public bool AllowsAuto { get; }
        public bool IsUnlocked { get; set; }
        public Transform Model { get; set; }
        public Transform Muzzle { get; set; }
        public int MagazineSize { get; private set; }
        public int CurrentAmmo { get; set; }
        public int ReserveAmmo { get; set; }
        public float ReloadSeconds { get; }
        public bool IsReloading { get; set; }
        public ShootingMode ShootingMode { get; set; }
        public int TotalAmmo => CurrentAmmo + ReserveAmmo;
        public int RemainingMagazineCount => MagazineSize <= 0 ? 0 : Mathf.CeilToInt((float)TotalAmmo / MagazineSize);

        public void ConfigureAmmo(int magazineSize, int totalMagazines)
        {
            MagazineSize = Mathf.Max(1, magazineSize);
            int safeTotalMagazines = Mathf.Max(1, totalMagazines);
            CurrentAmmo = MagazineSize;
            ReserveAmmo = (safeTotalMagazines - 1) * MagazineSize;
            IsReloading = false;
        }
    }

    private void EnsureWeaponAudioSources()
    {
        Transform audioParent = playerCamera != null ? playerCamera.transform : transform;
        shotAudioSource = EnsureAudioSource(shotAudioSource, audioParent, "Single Shot Audio");
        autoFireAudioSource = EnsureAudioSource(autoFireAudioSource, audioParent, "Auto Fire Audio");
        reloadAudioSource = EnsureAudioSource(reloadAudioSource, audioParent, "Reload Audio");
    }

    private static AudioSource EnsureAudioSource(AudioSource source, Transform parent, string objectName)
    {
        if (source == null && parent != null)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
            {
                source = existing.GetComponent<AudioSource>();
            }
        }

        if (source == null)
        {
            GameObject audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(parent, false);
            audioObject.transform.localPosition = new Vector3(0.34f, -0.32f, 0.68f);
            audioObject.transform.localRotation = Quaternion.identity;
            audioObject.transform.localScale = Vector3.one;
            source = audioObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
        return source;
    }

    private void PlayShotSound()
    {
        EnsureWeaponAudioSources();

        bool useAutoSound = activeWeapon != null &&
                            activeWeapon.AllowsAuto &&
                            activeWeapon.ShootingMode == ShootingMode.Auto &&
                            autoFireSound != null;

        AudioSource targetSource = useAutoSound ? autoFireAudioSource : shotAudioSource;
        AudioClip targetClip = useAutoSound ? autoFireSound : shotSound;
        float targetVolume = useAutoSound ? autoFireVolume : shotVolume;

        if (targetSource == null || targetClip == null)
        {
            return;
        }

        float minPitch = Mathf.Min(shotPitchRange.x, shotPitchRange.y);
        float maxPitch = Mathf.Max(shotPitchRange.x, shotPitchRange.y);
        targetSource.pitch = Random.Range(minPitch, maxPitch);
        targetSource.PlayOneShot(targetClip, targetVolume);
    }

    private void PlayReloadSound(WeaponRuntimeState weaponState)
    {
        EnsureWeaponAudioSources();

        if (reloadAudioSource == null || reloadSound == null)
        {
            return;
        }

        reloadAudioSource.Stop();
        reloadAudioSource.pitch = weaponState != null && weaponState.Slot == WeaponSlot.Primary ? 0.92f : 1.04f;
        reloadAudioSource.PlayOneShot(reloadSound, reloadVolume);
    }

    private void FireHitscan()
    {
        Ray aimRay = GetAimRay();
        Vector3 targetPoint = aimRay.origin + aimRay.direction * raycastDistance;
        Vector3 shotOrigin = GetShotOrigin(aimRay);

        if (TryGetShootHit(aimRay, out RaycastHit hit))
        {
            targetPoint = hit.point;
            CreateBulletHole(hit);
            ApplyHitResponse(hit, aimRay.direction);

            if (hit.collider.CompareTag("Target"))
            {
                Debug.Log("Hit " + hit.collider.name);
            }
        }

        SpawnShotVisual(shotOrigin, targetPoint);
    }

    private bool TryGetShootHit(Ray aimRay, out RaycastHit closestHit)
    {
        closestHit = default;
        float closestDistance = float.MaxValue;
        int hitCount = Physics.RaycastNonAlloc(
            aimRay,
            raycastHitBuffer,
            raycastDistance,
            GetEffectiveHitMask(),
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = raycastHitBuffer[i];
            if (candidate.collider == null || IsIgnoredHit(candidate.collider))
            {
                continue;
            }

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                closestHit = candidate;
            }
        }

        return closestDistance < float.MaxValue;
    }

    private int GetEffectiveHitMask()
    {
        int mask = hitLayers.value;
        if (!excludePlayerLayerFromHits)
        {
            return mask;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            mask &= ~(1 << playerLayer);
        }

        return mask;
    }

    private bool IsIgnoredHit(Collider hitCollider)
    {
        if (!ignoreOwnColliders || hitCollider == null)
        {
            return false;
        }

        Transform rootToIgnore = ignoredHitRoot != null ? ignoredHitRoot : ResolveIgnoredHitRoot();
        if (IsSameOrChild(hitCollider.transform, transform) ||
            IsSameOrChild(hitCollider.transform, rootToIgnore))
        {
            return true;
        }

        return hitCollider.attachedRigidbody != null &&
            IsSameOrChild(hitCollider.attachedRigidbody.transform, rootToIgnore);
    }

    private Transform ResolveIgnoredHitRoot()
    {
        if (playerCamera != null)
        {
            return playerCamera.transform.root;
        }

        return transform.root != null ? transform.root : transform;
    }

    private static bool IsSameOrChild(Transform candidate, Transform possibleParent)
    {
        return candidate != null && possibleParent != null &&
            (candidate == possibleParent || candidate.IsChildOf(possibleParent));
    }

    private void ApplyHitResponse(RaycastHit hit, Vector3 shotDirection)
    {
        ShootableTarget shootableTarget = hit.collider.GetComponentInParent<ShootableTarget>();
        if (shootableTarget == null && IsTarget(hit.collider))
        {
            GameObject targetObject = hit.rigidbody != null
                ? hit.rigidbody.gameObject
                : hit.collider.gameObject;

            shootableTarget = targetObject.AddComponent<ShootableTarget>();
        }

        if (shootableTarget != null)
        {
            shootableTarget.TakeHit(damage, hit.point, shotDirection, hitImpulse);
            return;
        }

        hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        ApplyPhysicsImpulse(hit, shotDirection, hitImpulse);
    }

    private static bool IsTarget(Collider targetCollider)
    {
        if (targetCollider.CompareTag("Target"))
        {
            return true;
        }

        return targetCollider.attachedRigidbody != null &&
            targetCollider.attachedRigidbody.CompareTag("Target");
    }

    private static void ApplyPhysicsImpulse(RaycastHit hit, Vector3 shotDirection, float impulse)
    {
        if (hit.rigidbody == null || impulse <= 0f)
        {
            return;
        }

        hit.rigidbody.WakeUp();
        hit.rigidbody.AddForceAtPosition(shotDirection.normalized * impulse, hit.point, ForceMode.Impulse);
    }

    private Ray GetAimRay()
    {
        Transform aimTransform = playerCamera != null
            ? playerCamera.transform
            : bulletSpawnPoint != null ? bulletSpawnPoint : transform;

        Vector3 origin = playerCamera != null
            ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).origin
            : aimTransform.position;

        Vector3 direction = playerCamera != null
            ? playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).direction
            : aimTransform.forward;

        if (spreadIntensity > 0f)
        {
            direction += aimTransform.right * Random.Range(-spreadIntensity, spreadIntensity);
            direction += aimTransform.up * Random.Range(-spreadIntensity, spreadIntensity);
        }

        return new Ray(origin, direction.normalized);
    }

    private Vector3 GetShotOrigin(Ray aimRay)
    {
        if (bulletSpawnPoint != null)
        {
            return bulletSpawnPoint.position;
        }

        return aimRay.origin + aimRay.direction * 0.35f;
    }

    private void SpawnShotVisual(Vector3 shotOrigin, Vector3 targetPoint)
    {
        if (showShotTracer)
        {
            SpawnShotTracer(shotOrigin, targetPoint);
        }
    }

    private void SpawnShotTracer(Vector3 startPoint, Vector3 endPoint)
    {
        if ((endPoint - startPoint).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        PooledShotTracer tracer = GetPooledShotTracer();
        tracer.Show(
            startPoint,
            endPoint,
            Mathf.Max(0.001f, tracerStartWidth),
            Mathf.Max(0.001f, tracerEndWidth),
            shotTracerMaterial != null ? shotTracerMaterial : GetFallbackTracerMaterial(),
            tracerStartColor,
            tracerEndColor,
            Mathf.Max(0.01f, tracerDuration),
            shotTracerPoolRoot);
    }

    private static void PrewarmShotTracers()
    {
        EnsureShotTracerPoolRoot();

        while (shotTracerPool.Count < InitialShotTracerPoolSize)
        {
            CreatePooledShotTracer();
        }
    }

    private static PooledShotTracer GetPooledShotTracer()
    {
        EnsureShotTracerPoolRoot();

        for (int i = 0; i < shotTracerPool.Count; i++)
        {
            PooledShotTracer candidate = shotTracerPool[i];
            if (candidate != null && !candidate.gameObject.activeSelf)
            {
                return candidate;
            }
        }

        if (shotTracerPool.Count < MaxShotTracerPoolSize)
        {
            return CreatePooledShotTracer();
        }

        PooledShotTracer reused = shotTracerPool[nextShotTracerReuseIndex % shotTracerPool.Count];
        nextShotTracerReuseIndex++;
        return reused;
    }

    private static void EnsureShotTracerPoolRoot()
    {
        if (shotTracerPoolRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("Shot Tracer Pool");
        root.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(root);
        shotTracerPoolRoot = root.transform;
    }

    private static PooledShotTracer CreatePooledShotTracer()
    {
        GameObject tracerObject = new GameObject("Shot Tracer");
        tracerObject.hideFlags = HideFlags.HideInHierarchy;
        tracerObject.transform.SetParent(shotTracerPoolRoot, false);

        LineRenderer lineRenderer = tracerObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.numCapVertices = 2;
        lineRenderer.alignment = LineAlignment.View;

        PooledShotTracer tracer = tracerObject.AddComponent<PooledShotTracer>();
        tracer.Initialize(lineRenderer, shotTracerPoolRoot);
        tracerObject.SetActive(false);
        shotTracerPool.Add(tracer);

        return tracer;
    }

    private sealed class PooledShotTracer : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Transform poolRoot;
        private float releaseTime;

        public void Initialize(LineRenderer targetLineRenderer, Transform targetPoolRoot)
        {
            lineRenderer = targetLineRenderer;
            poolRoot = targetPoolRoot;
        }

        public void Show(Vector3 startPoint, Vector3 endPoint, float startWidth, float endWidth, Material material, Color startColor, Color endColor, float duration, Transform targetPoolRoot)
        {
            poolRoot = targetPoolRoot;
            releaseTime = Time.time + duration;

            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            gameObject.SetActive(true);
            transform.SetParent(poolRoot, false);

            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPoint);
            lineRenderer.SetPosition(1, endPoint);
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.numCapVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.material = material;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
        }

        private void LateUpdate()
        {
            if (Time.time < releaseTime)
            {
                return;
            }

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            transform.SetParent(poolRoot, false);
            gameObject.SetActive(false);
        }
    }

    private void CreateBulletHole(RaycastHit hit)
    {
        Vector3 safeNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.forward;
        float randomSize = GetBulletHoleDiameter();

        if (useRoundBulletHoles)
        {
            BulletHoleDecal.CreateRound(
                hit.point,
                safeNormal,
                randomSize,
                bulletHoleSurfaceOffset,
                hit.collider.transform,
                bulletHoleLifetime);
            return;
        }

        Quaternion hitRotation = Quaternion.LookRotation(safeNormal);
        Vector3 hitPosition = hit.point + safeNormal * Mathf.Max(0.001f, bulletHoleSurfaceOffset);

        GameObject bulletHole = bulletHolePrefab != null
            ? Instantiate(bulletHolePrefab, hitPosition, hitRotation)
            : CreateFallbackBulletHole(hitPosition, hitRotation);

        float rollDegrees = Random.Range(0f, 360f);
        BulletHoleDecal.AttachToSurface(
            bulletHole,
            hit.collider.transform,
            hit.point,
            safeNormal,
            randomSize,
            bulletHoleSurfaceOffset,
            rollDegrees);

        EnsureBulletHoleVisible(bulletHole);

        Destroy(bulletHole, bulletHoleLifetime);
    }

    private float GetBulletHoleDiameter()
    {
        float size = bulletHoleSize * Random.Range(0.8f, 1.2f);
        float minSize = Mathf.Max(0.01f, minRoundBulletHoleSize);
        float maxSize = Mathf.Max(minSize, maxRoundBulletHoleSize);
        return useRoundBulletHoles ? Mathf.Clamp(size, minSize, maxSize) : Mathf.Max(0.01f, size);
    }

    private static GameObject CreateFallbackBulletHole(Vector3 position, Quaternion rotation)
    {
        GameObject bulletHole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bulletHole.name = "Bullet Hole";
        bulletHole.transform.SetPositionAndRotation(position, rotation);

        Collider collider = bulletHole.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        MeshRenderer renderer = bulletHole.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = GetFallbackBulletHoleMaterial();
        }

        return bulletHole;
    }

    private static void EnsureBulletHoleVisible(GameObject bulletHole)
    {
        Renderer[] renderers = bulletHole.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            targetRenderer.enabled = true;
            targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            targetRenderer.receiveShadows = false;

            Material[] materials = targetRenderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material != null && material.HasProperty("_Cull"))
                {
                    material.SetFloat("_Cull", 0f);
                }
            }
        }
    }

    private static Material GetFallbackTracerMaterial()
    {
        if (fallbackTracerMaterial != null)
        {
            return fallbackTracerMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        fallbackTracerMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        return fallbackTracerMaterial;
    }

    private static Material GetFallbackBulletHoleMaterial()
    {
        if (fallbackBulletHoleMaterial != null)
        {
            return fallbackBulletHoleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        fallbackBulletHoleMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            color = Color.black
        };

        return fallbackBulletHoleMaterial;
    }

    private static int GetPlayerLayerOrCurrent(int currentLayer)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        return playerLayer >= 0 ? playerLayer : currentLayer;
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

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursive(child, layer);
        }
    }

    private void EnsureAmmoUI()
    {
        if (ammoText != null || !createAmmoUIIfMissing)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Ammo UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textObject = new GameObject("Ammo Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(canvasObject.transform, false);

        ammoText = textObject.GetComponent<TextMeshProUGUI>();
        if (ammoFont != null)
        {
            ammoText.font = ammoFont;
        }

        ammoText.alignment = TextAlignmentOptions.BottomRight;
        ammoText.fontSize = ammoFontSize;
        ammoText.color = Color.white;
        ammoText.raycastTarget = false;

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(2f, -2f);

        RectTransform rectTransform = ammoText.rectTransform;
        rectTransform.anchorMin = new Vector2(1f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(1f, 0f);
        rectTransform.anchoredPosition = ammoUiOffset;
        rectTransform.sizeDelta = new Vector2(360f, 120f);
    }

    private void UpdateAmmoUI()
    {
        if (ammoText == null)
        {
            return;
        }

        if (activeWeapon == null)
        {
            ammoText.text = "WEAPON: NONE\nMODE: SINGLE\nAMMO: 0 / 0\nMAGS: 0";
            return;
        }

        string slotLabel = activeSlot == WeaponSlot.Primary ? "1 PRIMARY" : "2 SECONDARY";
        ammoText.text =
            $"WEAPON: {slotLabel} {activeWeapon.DisplayName}\n" +
            $"MODE: {GetModeLabel()}\n" +
            $"AMMO: {CurrentAmmo} / {activeWeapon.MagazineSize}\n" +
            $"MAGS: {RemainingMagazineCount}";
    }

    private string GetModeLabel()
    {
        if (activeWeapon != null && activeWeapon.IsReloading)
        {
            return "RELOADING";
        }

        if (activeWeapon == null || !activeWeapon.AllowsAuto)
        {
            return "SINGLE";
        }

        return activeWeapon.ShootingMode == ShootingMode.Auto ? "AUTO" : "SINGLE";
    }
}
