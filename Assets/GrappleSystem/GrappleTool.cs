using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class GrappleTool : MonoBehaviour
{
    public enum EMovementMode
    {
        ConstantTime,
        ConstantSpeed
    }

    [Header("Common Settings")]
    [SerializeField] EMovementMode MovementMode = EMovementMode.ConstantTime;
    [SerializeField] bool PermitByDefault = false;
    [SerializeField] float MaxRange = 50.0f;
    [SerializeField] LayerMask GrappleLayerMask = ~0;
    [SerializeField] float HaltDistance = 1.0f;

    [Header("Movement: Arcing")]
    [SerializeField] bool EnableArcing = false;
    [SerializeField] float ArcHeight = 2.0f;

    [Header("Movement Mode: Constant Time")]
    [SerializeField] float Movement_TimeToReach = 1.5f;

    [Header("Movement Mode: Constant Speed")]
    [SerializeField] float Movement_Speed = 5.0f;
    [SerializeField] float Movement_VerticalErrorScale = 0.2f;

    [Header("Events")]
    [SerializeField] UnityEvent OnBeganGrapple = new();
    [SerializeField] UnityEvent OnFinishedGrapple = new();
    [SerializeField] UnityEvent<bool, bool> OnUpdateGrappleStatus = new();

    Rigidbody LinkedRB;

    bool bIsGrappling = false;
    bool bReachedGrappleEnd = false;
    Vector3 GrappleStartLocation = Vector3.zero;
    float TimeGrappling = -1f;
    Vector3 EndPositionToLock = Vector3.zero;

    float CurrentTarget_ExpiryTime = -1f;
    GrappleSurface CurrentTarget_Surface = null;
    Vector3 CurrentTarget_Location = Vector3.zero;

    void Awake()
    {
        LinkedRB = GetComponent<Rigidbody>();
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        bool bMouseDownThisFrame = Input.GetMouseButtonDown(0);
        bool bMouseUpThisFrame = Input.GetMouseButtonUp(0);
        bool bMouseDown = Input.GetMouseButton(0);
#else
        bool bMouseDownThisFrame = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        bool bMouseUpThisFrame = UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame;
        bool bMouseDown = UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
#endif

        if (bMouseDownThisFrame && !bIsGrappling)
            AttemptToStartGrapple();
        else if (bMouseUpThisFrame && bIsGrappling)
            AttemptToStopGrapple();
        else if (bIsGrappling && bMouseDown && (CurrentTarget_ExpiryTime > 0f))
        {
            CurrentTarget_ExpiryTime -= Time.deltaTime;

            if (CurrentTarget_ExpiryTime <= 0f)
                AttemptToStopGrapple();
        }

        if (bIsGrappling)
            OnUpdateGrappleStatus.Invoke(true, false);
        else
        {
            GrappleSurface FoundSurface;
            Vector3 FoundLocation;

            bool bCouldGrapple = AttemptToFindGrappleTarget(out FoundSurface, out FoundLocation);

            OnUpdateGrappleStatus.Invoke(false, bCouldGrapple);
        }
    }

    void FixedUpdate()
    {
        if (bIsGrappling)
            Tick_Grapple(Time.deltaTime);
    }

    protected void AttemptToStartGrapple()
    {
        if (AttemptToFindGrappleTarget(out CurrentTarget_Surface, out CurrentTarget_Location))
        {
            if ((CurrentTarget_Surface != null) && CurrentTarget_Surface.HasTimeLimit)
                CurrentTarget_ExpiryTime = CurrentTarget_Surface.TimeLimit;
            else
                CurrentTarget_ExpiryTime = -1f;

            GrappleStartLocation = LinkedRB.position;
            TimeGrappling = 0f;
            bIsGrappling = true;
            bReachedGrappleEnd = false;

            OnBeganGrapple.Invoke();
        }
    }

    protected void AttemptToStopGrapple()
    {
        CurrentTarget_ExpiryTime = -1f;
        CurrentTarget_Surface = null;
        CurrentTarget_Location = Vector3.zero;
        TimeGrappling = -1f;

        bIsGrappling = false;
        bReachedGrappleEnd = false;

        OnFinishedGrapple.Invoke();
    }

    protected bool AttemptToFindGrappleTarget(out GrappleSurface OutFoundSurface, out Vector3 OutFoundGrappleLocation)
    {
        bool bTargetFound = false;
        OutFoundSurface = null;
        OutFoundGrappleLocation = Vector3.zero;

        Ray SceneRay = Camera.main.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));

        RaycastHit HitResult;
        if (Physics.Raycast(SceneRay, out HitResult, MaxRange, GrappleLayerMask, QueryTriggerInteraction.Ignore))
        {
            GrappleSurface FoundSurface = null;
            if (HitResult.collider.gameObject.TryGetComponent<GrappleSurface>(out FoundSurface))
            {
                if (FoundSurface.CanGrapple)
                {
                    bTargetFound = true;
                    OutFoundSurface = FoundSurface;
                }
            }
            else if (PermitByDefault)
                bTargetFound = true;
        }

        if (bTargetFound)
            OutFoundGrappleLocation = HitResult.point;

        return bTargetFound;
    }

    protected void Tick_Grapple(float InDeltaTime)
    {
        TimeGrappling += InDeltaTime;

        if (bReachedGrappleEnd)
        {
            LinkedRB.velocity = Vector3.zero;
            LinkedRB.MovePosition(EndPositionToLock);
            return;
        }

        Vector3 VectorToGrapplePoint = CurrentTarget_Location - LinkedRB.position;
        float DistanceToGrapplePointSq = VectorToGrapplePoint.sqrMagnitude;

        // can't move any closer?
        if (DistanceToGrapplePointSq <= (HaltDistance * HaltDistance))
        {
            LinkedRB.velocity = Vector3.zero;
            EndPositionToLock = LinkedRB.position;
            bReachedGrappleEnd = true;
            return;
        }

        if (MovementMode == EMovementMode.ConstantTime)
        {
            float Progress = Mathf.Clamp01(TimeGrappling / Movement_TimeToReach);

            float DesiredArcHeight = EnableArcing ? (ArcHeight * Mathf.Sin(Mathf.PI * Progress)) : 0f;

            Vector3 NewPosition = Vector3.Lerp(GrappleStartLocation, CurrentTarget_Location, Progress) +
                                  (Vector3.up * DesiredArcHeight);

            LinkedRB.MovePosition(NewPosition);
        }
        else if (MovementMode == EMovementMode.ConstantSpeed)
        {
            Vector3 VectorToCurrentPos = LinkedRB.position - GrappleStartLocation;
            float DistanceAlongVector = Vector3.Dot(VectorToCurrentPos, VectorToGrapplePoint.normalized);
            float Progress = Mathf.Clamp01(DistanceAlongVector /
                                           (CurrentTarget_Location - GrappleStartLocation).magnitude);

            Vector3 BasePoint = Vector3.Lerp(GrappleStartLocation, CurrentTarget_Location, Progress);

            float DesiredArcHeight = EnableArcing ? (ArcHeight * Mathf.Sin(Mathf.PI * Progress)) : 0f;
            float CurrentArcHeight = EnableArcing ? (Vector3.Dot(LinkedRB.position - BasePoint, Vector3.up)) : 0f;

            float VerticalSpeed = (DesiredArcHeight - CurrentArcHeight) * Movement_VerticalErrorScale;

            LinkedRB.velocity = VectorToGrapplePoint.normalized * Movement_Speed +
                                Vector3.up * VerticalSpeed;
        }
    }
}
