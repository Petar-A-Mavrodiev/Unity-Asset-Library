using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// FPS-style interactor that raycasts from the camera and uses mouse input for grabbing objects.
/// Attach this to your FPS camera. Works with XRGrabInteractable objects.
/// </summary>
public class FPSGrabInteractor : XRBaseInputInteractor
{
    [Header("FPS Settings")]
    [SerializeField] float grabRange = 3f;
    [SerializeField] Transform holdPoint;
    [SerializeField] LayerMask interactableMask = -1;

    [Header("Input")]
    [SerializeField] InputActionReference selectActionReference;
    [SerializeField] bool toggleGrab = true;

    [Header("Line Renderer")]
    [SerializeField] bool showRaycastLine = true;
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] Vector3 lineOffset = new Vector3(0.2f, -0.1f, 0.3f);
    [SerializeField] float lineWidth = 0.01f;
    [SerializeField] Color lineColor = Color.cyan;
    [SerializeField] Color lineHitColor = Color.green;

    [Header("Throw Settings")]
    [SerializeField] bool throwOnDrop = true;
    [SerializeField] float throwForce = 5f;

    Camera mainCam;
    InputAction selectAction;
    bool useManualAction;
    bool toggleState;
    bool inputPressedLastFrame;

    public float GrabRange
    {
        get => grabRange;
        set
        {
            grabRange = value;
            UpdateLineRenderer();
        }
    }

    public bool ShowRaycastLine
    {
        get => showRaycastLine;
        set
        {
            showRaycastLine = value;
            if (lineRenderer != null)
                lineRenderer.enabled = value;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        mainCam = GetComponent<Camera>();
        if (mainCam == null)
            mainCam = Camera.main;

        // Use holdPoint as attach transform for grabbed objects
        if (holdPoint != null)
            attachTransform = holdPoint;

        // Use assigned action reference, or fallback to mouse left-click
        if (selectActionReference != null)
        {
            selectAction = selectActionReference.action;
            useManualAction = false;
        }
        else
        {
            selectAction = new InputAction("Select", InputActionType.Button, "<Mouse>/leftButton");
            useManualAction = true;
        }

        // Setup line renderer
        SetupLineRenderer();
    }

    void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.enabled = showRaycastLine;
    }

    void LateUpdate()
    {
        UpdateLineRenderer();
    }

    void UpdateLineRenderer()
    {
        if (lineRenderer == null || mainCam == null) return;

        lineRenderer.enabled = showRaycastLine && !toggleState; // Hide when holding object

        if (!lineRenderer.enabled) return;

        // Apply offset in local space
        Vector3 startPos = mainCam.transform.position + mainCam.transform.TransformDirection(lineOffset);
        Vector3 direction = mainCam.transform.forward;
        Vector3 endPos;
        bool hitInteractable = false;

        if (Physics.Raycast(mainCam.transform.position, direction, out RaycastHit hit, grabRange, interactableMask))
        {
            endPos = hit.point;
            hitInteractable = hit.collider.GetComponent<IXRInteractable>() != null;
        }
        else
        {
            endPos = mainCam.transform.position + direction * grabRange;
        }

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);

        // Change color based on hit
        Color currentColor = hitInteractable ? lineHitColor : lineColor;
        lineRenderer.startColor = currentColor;
        lineRenderer.endColor = currentColor;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (useManualAction)
            selectAction?.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (useManualAction)
            selectAction?.Disable();
        toggleState = false;
    }

    void OnDestroy()
    {
        if (useManualAction)
            selectAction?.Dispose();
    }

    public override void GetValidTargets(List<IXRInteractable> targets)
    {
        targets.Clear();

        if (mainCam == null) return;

        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, grabRange, interactableMask))
        {
            if (hit.collider.TryGetComponent<IXRInteractable>(out var interactable))
            {
                if (interactionManager != null && interactionManager.IsRegistered(interactable))
                {
                    targets.Add(interactable);
                }
            }
        }
    }

    public override bool isSelectActive
    {
        get
        {
            if (selectAction == null)
                return base.isSelectActive;

            bool pressed = selectAction.IsPressed();

            if (toggleGrab)
            {
                // Toggle mode: click to grab, click again to drop
                if (pressed && !inputPressedLastFrame)
                    toggleState = !toggleState;

                inputPressedLastFrame = pressed;
                return toggleState;
            }
            else
            {
                // Hold mode: hold to grab, release to drop
                return pressed;
            }
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        if (throwOnDrop && mainCam != null && args.interactableObject != null)
        {
            StartCoroutine(ApplyThrowForce(args.interactableObject.transform.gameObject));
        }
    }

    System.Collections.IEnumerator ApplyThrowForce(GameObject go)
    {
        // Wait for XRGrabInteractable to finish releasing
        yield return new WaitForFixedUpdate();

        if (go != null && go.TryGetComponent<Rigidbody>(out var rb))
        {
            Vector3 throwDirection = mainCam.transform.forward;
            rb.linearVelocity = throwDirection * throwForce;
        }
    }

    /// <summary>
    /// Creates a hold point if one wasn't assigned.
    /// </summary>
    public void CreateHoldPoint(float distance = 0.5f)
    {
        if (holdPoint == null)
        {
            var holdObj = new GameObject("HoldPoint");
            holdObj.transform.SetParent(transform);
            holdObj.transform.localPosition = new Vector3(0, 0, distance);
            holdObj.transform.localRotation = Quaternion.identity;
            holdPoint = holdObj.transform;
            attachTransform = holdPoint;
        }
    }

    void OnDrawGizmosSelected()
    {
        Camera cam = mainCam != null ? mainCam : GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cam.transform.position, cam.transform.forward * grabRange);

        if (holdPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(holdPoint.position, 0.05f);
        }
    }
}
