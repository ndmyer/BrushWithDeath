using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class SkeletonProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0.01f)] private float speed = 6f;
    [SerializeField, Min(0.05f)] private float lifetime = 3f;
    [SerializeField] private bool destroyOnImpact = true;
    [SerializeField] private float rotationSpeedDegreesPerSecond;
    [SerializeField] private bool useBoomerangCircleMotion;
    [SerializeField, Min(0.05f)] private float boomerangRadius = 1.2f;
    [SerializeField, Min(0.05f)] private float boomerangDuration = 1f;
    [SerializeField] private bool boomerangClockwise;

    private GameObject owner;
    private Vector2 direction = Vector2.right;
    private float damage = 1f;
    private float lifetimeTimer;
    private float activeRotationSpeedDegreesPerSecond;
    private Vector2 launchDirection = Vector2.right;
    private Vector2 launchPosition;
    private Vector2 previousPosition;
    private float flightElapsedTime;
    private bool hasBoomerangHalfwayPoint;
    private Vector2 boomerangHalfwayPoint;

    private void Reset()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        lifetimeTimer = lifetime;
        activeRotationSpeedDegreesPerSecond = rotationSpeedDegreesPerSecond;
        launchDirection = direction;
        launchPosition = transform.position;
        previousPosition = launchPosition;
        flightElapsedTime = 0f;
    }

    public void Initialize(GameObject ownerObject, Vector2 moveDirection, float projectileSpeed, float projectileDamage)
    {
        Initialize(ownerObject, moveDirection, projectileSpeed, projectileDamage, null, rotationSpeedDegreesPerSecond, lifetime);
    }

    public void InitializeBoomerang(
        GameObject ownerObject,
        Vector2 moveDirection,
        float projectileSpeed,
        float projectileDamage,
        Vector2 halfwayPoint)
    {
        Initialize(ownerObject, moveDirection, projectileSpeed, projectileDamage, null, rotationSpeedDegreesPerSecond, lifetime);
        hasBoomerangHalfwayPoint = true;
        boomerangHalfwayPoint = halfwayPoint;
    }

    public void Initialize(
        GameObject ownerObject,
        Vector2 moveDirection,
        float projectileSpeed,
        float projectileDamage,
        Sprite overrideSprite,
        float projectileRotationSpeedDegreesPerSecond,
        float projectileLifetime)
    {
        owner = ownerObject;
        direction = moveDirection.sqrMagnitude > Mathf.Epsilon ? moveDirection.normalized : Vector2.right;
        speed = projectileSpeed;
        damage = projectileDamage;
        lifetimeTimer = projectileLifetime > 0f ? projectileLifetime : lifetime;
        activeRotationSpeedDegreesPerSecond = projectileRotationSpeedDegreesPerSecond;
        launchDirection = direction;
        launchPosition = transform.position;
        previousPosition = launchPosition;
        flightElapsedTime = 0f;
        hasBoomerangHalfwayPoint = false;
        boomerangHalfwayPoint = launchPosition;

        if (spriteRenderer != null && overrideSprite != null)
            spriteRenderer.sprite = overrideSprite;

        if (body != null)
            body.linearVelocity = direction * speed;
    }

    private void Update()
    {
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (Mathf.Abs(activeRotationSpeedDegreesPerSecond) > Mathf.Epsilon)
            transform.Rotate(0f, 0f, activeRotationSpeedDegreesPerSecond * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (useBoomerangCircleMotion)
        {
            TickBoomerangMotion();
            return;
        }

        if (body != null)
        {
            body.linearVelocity = direction * speed;
            return;
        }

        transform.position += (Vector3)(direction * speed * Time.fixedDeltaTime);
    }

    private void TickBoomerangMotion()
    {
        flightElapsedTime += Time.fixedDeltaTime;

        Vector2 newPosition = GetBoomerangPosition(flightElapsedTime);
        Vector2 motionDelta = newPosition - previousPosition;
        if (motionDelta.sqrMagnitude > Mathf.Epsilon)
            direction = motionDelta.normalized;

        if (body != null)
        {
            body.linearVelocity = motionDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            body.MovePosition(newPosition);
        }
        else
        {
            transform.position = newPosition;
        }

        previousPosition = newPosition;
    }

    private Vector2 GetBoomerangPosition(float elapsedTime)
    {
        float duration = Mathf.Max(0.05f, boomerangDuration);
        float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
        float signedAngle = normalizedTime * Mathf.PI * 2f * (boomerangClockwise ? -1f : 1f);

        if (hasBoomerangHalfwayPoint)
        {
            Vector2 centerFromTarget = (launchPosition + boomerangHalfwayPoint) * 0.5f;
            Vector2 startOffsetFromTarget = launchPosition - centerFromTarget;
            if (startOffsetFromTarget.sqrMagnitude > 0.0001f)
                return centerFromTarget + Rotate(startOffsetFromTarget, signedAngle);
        }

        Vector2 lateral = boomerangClockwise
            ? new Vector2(launchDirection.y, -launchDirection.x)
            : new Vector2(-launchDirection.y, launchDirection.x);

        Vector2 center = launchPosition + lateral * boomerangRadius;
        Vector2 startOffset = -lateral * boomerangRadius;
        return center + Rotate(startOffset, signedAngle);
    }

    private static Vector2 Rotate(Vector2 value, float angleRadians)
    {
        float sin = Mathf.Sin(angleRadians);
        float cos = Mathf.Cos(angleRadians);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null || IsOwnedCollider(other))
            return;

        if (TryGetInterface(other, out IDamageable damageable))
        {
            damageable.ReceiveDamage(damage, direction, owner != null ? owner : gameObject);

            if (destroyOnImpact)
                Destroy(gameObject);

            return;
        }

        if (!other.isTrigger && destroyOnImpact)
            Destroy(gameObject);
    }

    private bool IsOwnedCollider(Collider2D other)
    {
        if (owner == null)
            return false;

        return other.transform == owner.transform || other.transform.IsChildOf(owner.transform);
    }

    private static bool TryGetInterface<T>(Component source, out T value)
        where T : class
    {
        value = null;

        if (source == null)
            return false;

        value = source.GetComponent(typeof(T)) as T;
        if (value != null)
            return true;

        value = source.GetComponentInParent(typeof(T)) as T;
        if (value != null)
            return true;

        value = source.GetComponentInChildren(typeof(T)) as T;
        return value != null;
    }
}
