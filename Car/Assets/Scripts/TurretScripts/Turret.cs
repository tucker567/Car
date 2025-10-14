using UnityEditor;
using UnityEngine;


public class Turret : MonoBehaviour
{
    public Gun gun;
    public MountPoint mountPoint1;
    public MountPoint mountPoint2;
    public Transform target;
    public float maxRange = 30f; // Add this line for max range

    private Quaternion turret1StartRot;
    private Quaternion turret2StartRot;

    void Awake()
    {
        if (mountPoint1 != null && mountPoint1.transform.childCount > 0)
            turret1StartRot = mountPoint1.transform.GetChild(0).rotation;
        if (mountPoint2 != null && mountPoint2.transform.childCount > 0)
            turret2StartRot = mountPoint2.transform.GetChild(0).rotation;
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!target) return;

        var dashLineSize = 2f;

        DrawMountPointGizmos(mountPoint1, dashLineSize);
        DrawMountPointGizmos(mountPoint2, dashLineSize);
#endif
    }

    void DrawMountPointGizmos(MountPoint mountPoint, float dashLineSize)
    {
#if UNITY_EDITOR
        if (mountPoint == null) return;
        var hardpoint = mountPoint.transform;
        var from = Quaternion.AngleAxis(-mountPoint.angleLimit / 2, hardpoint.up) * hardpoint.forward;
        var projection = Vector3.ProjectOnPlane(target.position - hardpoint.position, hardpoint.up);

        // projection line
        Handles.color = Color.white;
        Handles.DrawDottedLine(target.position, hardpoint.position + projection, dashLineSize);

        // do not draw target indicator when out of angle
        if (Vector3.Angle(hardpoint.forward, projection) > mountPoint.angleLimit / 2) return;

        // target line
        Handles.color = Color.red;
        Handles.DrawLine(hardpoint.position, hardpoint.position + projection);

        // range line
        Handles.color = Color.green;
        Handles.DrawWireArc(hardpoint.position, hardpoint.up, from, mountPoint.angleLimit, projection.magnitude);
        Handles.DrawSolidDisc(hardpoint.position + projection, hardpoint.up, .5f);
#endif
    }

    void Update()
    {
        bool targetInFOV1 = mountPoint1 != null &&
            Vector3.Angle(mountPoint1.transform.forward, target.position - mountPoint1.transform.position) <= mountPoint1.angleLimit / 2;
        bool targetInFOV2 = mountPoint2 != null &&
            Vector3.Angle(mountPoint2.transform.forward, target.position - mountPoint2.transform.position) <= mountPoint2.angleLimit / 2;

        bool canSeeTarget = target &&
            Vector3.Distance(transform.position, target.position) <= maxRange &&
            targetInFOV1 &&
            targetInFOV2 &&
            IsLineOfSightClear(mountPoint1) &&
            IsLineOfSightClear(mountPoint2);

        if (!canSeeTarget)
        {
            // Return turret children to their starting rotation
            if (mountPoint1 != null && mountPoint1.transform.childCount > 0)
                mountPoint1.transform.GetChild(0).rotation = Quaternion.RotateTowards(
                    mountPoint1.transform.GetChild(0).rotation, turret1StartRot, mountPoint1.turnSpeed * Time.deltaTime);

            if (mountPoint2 != null && mountPoint2.transform.childCount > 0)
                mountPoint2.transform.GetChild(0).rotation = Quaternion.RotateTowards(
                    mountPoint2.transform.GetChild(0).rotation, turret2StartRot, mountPoint2.turnSpeed * Time.deltaTime);

            return;
        }

        // do nothing when no target
        if (!target) return;

        // Check if target is within max range
        if (Vector3.Distance(transform.position, target.position) > maxRange)
            return;

        // Check line of sight for both mount points
        bool clearLineOfSight = true;
        clearLineOfSight &= IsLineOfSightClear(mountPoint1);
        clearLineOfSight &= IsLineOfSightClear(mountPoint2);

        // aim target
        var aimed = true;
        if (mountPoint1 != null && !mountPoint1.Aim(target.position))
        {
            aimed = false;
        }
        if (mountPoint2 != null && !mountPoint2.Aim(target.position))
        {
            aimed = false;
        }

        // shoot when aimed and line of sight is clear
        if (aimed && clearLineOfSight)
        {
            gun.Fire();
        }
    }

    bool IsLineOfSightClear(MountPoint mountPoint)
    {
        if (mountPoint == null || target == null) return true;
        var origin = mountPoint.transform.position;
        var destination = target.position;
        var direction = (destination - origin).normalized;
        var distance = Vector3.Distance(origin, destination);

        // Raycast only up to the target's position
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance))
        {
            // Only clear if the first thing hit is the target
            if (hit.transform != target)
                return false;
        }
        return true;
    }
}