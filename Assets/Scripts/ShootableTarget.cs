using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ShootableTarget : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 25f;

    [Header("Hit Feedback")]
    [SerializeField] private Color hitFlashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    [Header("Physics")]
    [SerializeField] private bool addRigidbodyIfMissing = true;
    [SerializeField] private float downImpulseMultiplier = 1.8f;
    [SerializeField] private float torqueImpulse = 10f;

    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    private Rigidbody targetRigidbody;
    private Renderer[] renderers;
    private Coroutine flashRoutine;
    private float currentHealth;
    private bool isDown;

    private void Awake()
    {
        currentHealth = Mathf.Max(1f, maxHealth);
        targetRigidbody = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();

        if (targetRigidbody == null && addRigidbodyIfMissing)
        {
            targetRigidbody = gameObject.AddComponent<Rigidbody>();
        }
    }

    public void TakeDamage(float amount)
    {
        TakeHit(amount, transform.position + Vector3.up, -transform.forward, 0f);
    }

    public void TakeHit(float amount, Vector3 hitPoint, Vector3 shotDirection, float impactForce)
    {
        Vector3 direction = shotDirection.sqrMagnitude > 0.0001f
            ? shotDirection.normalized
            : transform.forward;

        if (isDown)
        {
            ApplyImpulse(hitPoint, direction, impactForce);
            return;
        }

        currentHealth -= Mathf.Max(0f, amount);
        Flash();

        bool shouldKnockDown = currentHealth <= 0f;
        float finalImpulse = shouldKnockDown ? impactForce * downImpulseMultiplier : impactForce;
        ApplyImpulse(hitPoint, direction, finalImpulse);

        if (shouldKnockDown)
        {
            KnockDown(direction);
        }
    }

    private void KnockDown(Vector3 shotDirection)
    {
        isDown = true;

        if (targetRigidbody == null)
        {
            Debug.Log("Target down: " + name);
            return;
        }

        targetRigidbody.isKinematic = false;
        targetRigidbody.useGravity = true;
        targetRigidbody.constraints = RigidbodyConstraints.None;

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, shotDirection);
        if (torqueAxis.sqrMagnitude < 0.0001f)
        {
            torqueAxis = transform.right;
        }

        targetRigidbody.AddTorque(torqueAxis.normalized * torqueImpulse, ForceMode.Impulse);
        Debug.Log("Target down: " + name);
    }

    private void ApplyImpulse(Vector3 hitPoint, Vector3 direction, float impulse)
    {
        if (targetRigidbody == null || impulse <= 0f)
        {
            return;
        }

        targetRigidbody.WakeUp();
        targetRigidbody.AddForceAtPosition(direction.normalized * impulse, hitPoint, ForceMode.Impulse);
    }

    private void Flash()
    {
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlashColor(hitFlashColor);
        yield return new WaitForSeconds(flashDuration);
        ClearFlashColor();
        flashRoutine = null;
    }

    private void ApplyFlashColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                continue;
            }

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            if (targetRenderer.sharedMaterial.HasProperty(BaseColorProperty))
            {
                propertyBlock.SetColor(BaseColorProperty, color);
            }

            if (targetRenderer.sharedMaterial.HasProperty(ColorProperty))
            {
                propertyBlock.SetColor(ColorProperty, color);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ClearFlashColor()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].SetPropertyBlock(null);
            }
        }
    }
}
