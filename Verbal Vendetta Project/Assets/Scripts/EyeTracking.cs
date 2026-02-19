using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    [Header("Target & Orientation")]
    [Tooltip("The transform the eyes should look at. Automatically assigned to 'EyePoint' if found.")]
    [SerializeField] private Transform target;

    [Tooltip("The head bone/transform. Used to calculate relative look angles.")]
    public Transform headBone;

    [Header("Mesh")]
    public SkinnedMeshRenderer targetMesh;

    [Header("Settings")]
    [Tooltip("The angle (in degrees) at which the blend shape reaches 100% weight.")]
    public float angleLimit = 30f;
    [Tooltip("Smooth speed for eye movement.")]
    public float smoothSpeed = 25f;

    [Header("Blend Shape Names (CC Standard)")]
    // Left Eye
    public string L_LookUp = "Eye_Look_Up_L";
    public string L_LookDown = "Eye_Look_Down_L";
    public string L_LookLeft = "Eye_Look_Left_L";
    public string L_LookRight = "Eye_Look_Right_L";

    // Right Eye
    public string R_LookUp = "Eye_Look_Up_R";
    public string R_LookDown = "Eye_Look_Down_R";
    public string R_LookLeft = "Eye_Look_Left_R";
    public string R_LookRight = "Eye_Look_Right_R";

    [Header("Debug")]
    public bool showDebugRays = true;
    public bool testForceEyesUp = false;

    // Cache indices
    private int idx_L_Up, idx_L_Down, idx_L_Left, idx_L_Right;
    private int idx_R_Up, idx_R_Down, idx_R_Left, idx_R_Right;

    // Current Values for Smoothing
    private float currentPitch;
    private float currentYaw;

    void Start()
    {
        if (target == null)
        {
            GameObject eyePointObj = GameObject.Find("EyePoint");
            if (eyePointObj != null)
            {
                target = eyePointObj.transform;
                Debug.Log($"EyeTracking: Found and assigned target '{target.name}'");
            }
        }

        if (targetMesh == null) targetMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        if (headBone == null)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null) headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null) Debug.LogWarning("EyeTracking: Head bone not found/assigned. Calculations will be wrong.");
        }

        CacheBlendShapeIndices();
    }

    void CacheBlendShapeIndices()
    {
        if (targetMesh == null) return;
        Mesh m = targetMesh.sharedMesh;

        idx_L_Up = GetAndLogIndex(m, L_LookUp);
        idx_L_Down = GetAndLogIndex(m, L_LookDown);
        idx_L_Left = GetAndLogIndex(m, L_LookLeft);
        idx_L_Right = GetAndLogIndex(m, L_LookRight);

        idx_R_Up = GetAndLogIndex(m, R_LookUp);
        idx_R_Down = GetAndLogIndex(m, R_LookDown);
        idx_R_Left = GetAndLogIndex(m, R_LookLeft);
        idx_R_Right = GetAndLogIndex(m, R_LookRight);
    }

    int GetAndLogIndex(Mesh m, string name)
    {
        int index = m.GetBlendShapeIndex(name);
        if (index == -1) Debug.LogWarning($"EyeTracking: BlendShape '{name}' NOT found on mesh '{m.name}'");
        else Debug.Log($"EyeTracking: Found BlendShape '{name}' at index {index}");
        return index;
    }

    void LateUpdate()
    {
        if (target == null || targetMesh == null || headBone == null) return;

        // 1. Calculate Local Direction
        // Transform the target position into the Head's local space
        Vector3 targetLocalPos = headBone.InverseTransformPoint(target.position);
        
        // 2. Calculate Angles
        // Pitch: Angle in Y-Z plane (Rotation around X). Positive Y is Up.
        // Yaw: Angle in X-Z plane (Rotation around Y). Positive X is Right.
        
        // Use Atan2 for robust angles
        // Yaw: project to XZ plane (actually X and Z)
        float targetYaw = Mathf.Atan2(targetLocalPos.x, targetLocalPos.z) * Mathf.Rad2Deg;
        
        // Pitch: project to YZ plane. Negate because looking UP (+Y) requires negative rotation in Unity standard, 
        // BUT for blend shapes we just want the 'Up-ness'. 
        // Atan2(y, z) gives +Angle for +Y (Up).
        float targetPitch = Mathf.Atan2(targetLocalPos.y, targetLocalPos.z) * Mathf.Rad2Deg;

        // 3. Smooth Angles
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * smoothSpeed);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);

        ApplyEyeShapes(currentPitch, currentYaw);

        if (showDebugRays)
        {
            Debug.DrawRay(headBone.position, headBone.TransformDirection(new Vector3(Mathf.Sin(currentYaw * Mathf.Deg2Rad), Mathf.Sin(currentPitch * Mathf.Deg2Rad), 1)), Color.cyan);
            // Log occasionally or just inspect in debug mode
            // Debug.Log($"LocalPos: {targetLocalPos} | Pitch: {targetPitch:F1} | Yaw: {targetYaw:F1}");
        }
    }

    void ApplyEyeShapes(float pitch, float yaw)
    {
        if (testForceEyesUp)
        {
            SetWeight(idx_L_Up, 100f);
            SetWeight(idx_R_Up, 100f);
            
            SetWeight(idx_L_Down, 0f);
            SetWeight(idx_R_Down, 0f);
            SetWeight(idx_L_Left, 0f);
            SetWeight(idx_L_Right, 0f);
            SetWeight(idx_R_Left, 0f);
            SetWeight(idx_R_Right, 0f);
            return;
        }

        // Normalize weights (0 to 100) based on angleLimit
        
        // --- Vertical (Pitch) ---
        // Positive Pitch = Up
        float weightUp = Mathf.Clamp01(pitch / angleLimit) * 100f;
        float weightDown = Mathf.Clamp01(-pitch / angleLimit) * 100f;

        SetWeight(idx_L_Up, weightUp);
        SetWeight(idx_R_Up, weightUp);
        SetWeight(idx_L_Down, weightDown);
        SetWeight(idx_R_Down, weightDown);

        // --- Horizontal (Yaw) ---
        // Positive Yaw = Right
        float weightRight = Mathf.Clamp01(yaw / angleLimit) * 100f;
        float weightLeft = Mathf.Clamp01(-yaw / angleLimit) * 100f;

        SetWeight(idx_L_Left, weightLeft);
        SetWeight(idx_L_Right, weightRight);
        
        SetWeight(idx_R_Left, weightLeft);
        SetWeight(idx_R_Right, weightRight);
    }

    void SetWeight(int index, float value)
    {
        if (index != -1) targetMesh.SetBlendShapeWeight(index, value);
    }
}
