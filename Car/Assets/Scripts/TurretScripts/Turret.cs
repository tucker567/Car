using UnityEditor;
using UnityEngine;


public class Turret : MonoBehaviour
{
    public Gun gun;
    public MountPoint mountPoint1;
    public MountPoint mountPoint2;
    public Transform target;

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
        // do nothing when no target
        if (!target) return;

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