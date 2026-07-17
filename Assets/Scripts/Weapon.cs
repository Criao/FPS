using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ShootingMode
{
    Single,
    Auto
}

public class Weapon : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;

    [Header("Shooting Mode")]
    public ShootingMode currentShootingMode = ShootingMode.Single;
    [SerializeField] private KeyCode switchModeKey = KeyCode.B;

    [Header("Optional Visual Projectile")]
    [SerializeField] private bool spawnVisualProjectile;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private float bulletSpeed = 30f;
    [SerializeField] private float bulletLifetime = 3f;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 25;
    [SerializeField] private int totalMagazines = 3;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Hitscan")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float raycastDistance = 300f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private float autoEmptyMagazineSeconds = 8f;
    [SerializeField] private float spreadIntensity;

    [Header("Impact")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private float bulletHoleSize = 0.1f;
    [SerializeField] private float bulletHoleLifetime = 10f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TMP_FontAsset ammoFont;
    [SerializeField] private bool createAmmoUIIfMissing = true;
    [SerializeField] private Vector2 ammoUiOffset = new Vector2(-32f, 32f);
    [SerializeField] private int ammoFontSize = 28;

    private int currentAmmo;
    private int reserveAmmo;
    private float nextAutoShotTime;

    public int CurrentAmmo => currentAmmo;
    public int TotalAmmo => currentAmmo + reserveAmmo;
    public int RemainingMagazineCount => magazineSize <= 0 ? 0 : Mathf.CeilToInt((float)TotalAmmo / magazineSize);

    private float AutoFireInterval
    {
        get
        {
            if (magazineSize <= 1)
            {
                return 0.01f;
            }

            return Mathf.Max(0.01f, autoEmptyMagazineSeconds / (magazineSize - 1));
        }
    }

    private void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        magazineSize = Mathf.Max(1, magazineSize);
        totalMagazines = Mathf.Max(1, totalMagazines);
        autoEmptyMagazineSeconds = Mathf.Max(0.01f, autoEmptyMagazineSeconds);
        raycastDistance = Mathf.Max(1f, raycastDistance);

        currentAmmo = magazineSize;
        reserveAmmo = (totalMagazines - 1) * magazineSize;

        EnsureAmmoUI();
        UpdateAmmoUI();
    }

    private void Update()
    {
        HandleModeSwitch();
        HandleReload();
        HandleShooting();
    }

    private void HandleModeSwitch()
    {
        if (!Input.GetKeyDown(switchModeKey))
        {
            return;
        }

        currentShootingMode = currentShootingMode == ShootingMode.Single
            ? ShootingMode.Auto
            : ShootingMode.Single;

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
        switch (currentShootingMode)
        {
            case ShootingMode.Single:
                if (Input.GetButtonDown("Fire1"))
                {
                    TryShoot();
                }
                break;

            case ShootingMode.Auto:
                if (Input.GetButton("Fire1") && Time.time >= nextAutoShotTime)
                {
                    TryShoot();
                    nextAutoShotTime = Time.time + AutoFireInterval;
                }
                break;
        }
    }

    private bool TryShoot()
    {
        if (currentAmmo <= 0)
        {
            if (!TryReload())
            {
                Debug.Log("Out of ammo");
            }

            UpdateAmmoUI();
            return false;
        }

        currentAmmo--;
        FireHitscan();
        UpdateAmmoUI();

        return true;
    }

    private bool TryReload()
    {
        if (currentAmmo >= magazineSize || reserveAmmo <= 0)
        {
            UpdateAmmoUI();
            return false;
        }

        int neededAmmo = magazineSize - currentAmmo;
        int loadedAmmo = Mathf.Min(neededAmmo, reserveAmmo);

        currentAmmo += loadedAmmo;
        reserveAmmo -= loadedAmmo;

        UpdateAmmoUI();
        return true;
    }

    private void FireHitscan()
    {
        Ray aimRay = GetAimRay();
        Vector3 targetPoint = aimRay.origin + aimRay.direction * raycastDistance;

        if (Physics.Raycast(aimRay, out RaycastHit hit, raycastDistance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
            hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            CreateBulletHole(hit);

            if (hit.collider.CompareTag("Target"))
            {
                Debug.Log("Hit " + hit.collider.name);
            }
        }

        SpawnOptionalVisualProjectile(targetPoint);
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

    private void SpawnOptionalVisualProjectile(Vector3 targetPoint)
    {
        if (!spawnVisualProjectile || bulletPrefab == null || bulletSpawnPoint == null)
        {
            return;
        }

        Vector3 direction = (targetPoint - bulletSpawnPoint.position).normalized;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = bulletSpawnPoint.forward;
        }

        GameObject bullet = Instantiate(
            bulletPrefab,
            bulletSpawnPoint.position,
            Quaternion.LookRotation(direction));

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = direction * bulletSpeed;
        }

        Destroy(bullet, bulletLifetime);
    }

    private void CreateBulletHole(RaycastHit hit)
    {
        if (bulletHolePrefab == null)
        {
            return;
        }

        Quaternion hitRotation = Quaternion.LookRotation(hit.normal);
        GameObject bulletHole = Instantiate(
            bulletHolePrefab,
            hit.point + hit.normal * 0.001f,
            hitRotation);

        float randomSize = bulletHoleSize * Random.Range(0.8f, 1.2f);
        bulletHole.transform.localScale = Vector3.one * randomSize;
        bulletHole.transform.Rotate(hit.normal, Random.Range(0f, 360f), Space.World);
        bulletHole.transform.SetParent(hit.collider.transform);

        Destroy(bulletHole, bulletHoleLifetime);
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

        ammoText.text =
            $"MODE: {GetModeLabel()}\n" +
            $"AMMO: {currentAmmo} / {TotalAmmo}\n" +
            $"MAGS: {RemainingMagazineCount}";
    }

    private string GetModeLabel()
    {
        return currentShootingMode == ShootingMode.Auto ? "AUTO" : "SINGLE";
    }
}
