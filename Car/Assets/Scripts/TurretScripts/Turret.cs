using UnityEditor;
using UnityEngine;
using System.Linq;

public class Turret : MonoBehaviour
{
    public Gun gun;
    public MountPoint mountPoint1;
    public MountPoint mountPoint2;
    public Transform target;
    public float maxRange = 30f;
    public bool isPlayerTurret = false; // Add this

    private Quaternion turret1StartRot;
    private Quaternion turret2StartRot;
    private MarkerManager markerManager;

    void Awake()
    {
        if (mountPoint1 != null && mountPoint1.transform.childCount > 0)
            turret1StartRot = mountPoint1.transform.GetChild(0).localRotation; // Use localRotation
        if (mountPoint2 != null && mountPoint2.transform.childCount > 0)
            turret2StartRot = mountPoint2.transform.GetChild(0).localRotation; // Use localRotation

        // Find MarkerManager in the scene (modern Unity API)
        markerManager = Object.FindAnyObjectByType<MarkerManager>();
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
        if (isPlayerTurret)
        {
            // Try to target marked AI car with lowest marker number
            Transform bestTarget = null;
            if (markerManager != null)
            {
                // markerOrder is oldest to newest, so first is lowest marker number
                foreach (var car in markerManager.MarkerOrder)
                {
                    if (IsValidTarget(car))
                    {
                        bestTarget = car;
                        break;
                    }
                }
            }

            // If no marked car, fallback to any visible AI car
            if (bestTarget == null)
            {
                var aiCars = GameObject.FindGameObjectsWithTag("AICar");
                foreach (var aiCar in aiCars)
                {
                    var carTransform = aiCar.transform;
                    if (IsValidTarget(carTransform))
                    {
                        bestTarget = carTransform;
                        break;
                    }
                }
            }

            target = bestTarget;
        }

        bool targetInFOV1 = mountPoint1 != null &&
            target != null &&
            Vector3.Angle(mountPoint1.transform.forward, target.position - mountPoint1.transform.position) <= mountPoint1.angleLimit / 2;
        bool targetInFOV2 = mountPoint2 != null &&
            target != null &&
            Vector3.Angle(mountPoint2.transform.forward, target.position - mountPoint2.transform.position) <= mountPoint2.angleLimit / 2;

        bool canSeeTarget = target &&
            Vector3.Distance(transform.position, target.position) <= maxRange &&
            targetInFOV1 &&
            targetInFOV2 &&
            IsLineOfSightClear(mountPoint1) &&
            IsLineOfSightClear(mountPoint2);

        if (!canSeeTarget)
        {
            // Return turret children to their starting local rotation
            if (mountPoint1 != null && mountPoint1.transform.childCount > 0)
                mountPoint1.transform.GetChild(0).localRotation = Quaternion.RotateTowards(
                    mountPoint1.transform.GetChild(0).localRotation, turret1StartRot, mountPoint1.turnSpeed * Time.deltaTime);

            if (mountPoint2 != null && mountPoint2.transform.childCount > 0)
                mountPoint2.transform.GetChild(0).localRotation = Quaternion.RotateTowards(
                    mountPoint2.transform.GetChild(0).localRotation, turret2StartRot, mountPoint2.turnSpeed * Time.deltaTime);

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

    bool IsValidTarget(Transform car)
    {
        if (car == null) return false;
        float dist = Vector3.Distance(transform.position, car.position);
        if (dist > maxRange) return false;
        bool fov1 = mountPoint1 == null || Vector3.Angle(mountPoint1.transform.forward, car.position - mountPoint1.transform.position) <= mountPoint1.angleLimit / 2;
        bool fov2 = mountPoint2 == null || Vector3.Angle(mountPoint2.transform.forward, car.position - mountPoint2.transform.position) <= mountPoint2.angleLimit / 2;
        bool los1 = IsLineOfSightClear(mountPoint1, car);
        bool los2 = IsLineOfSightClear(mountPoint2, car);
        return fov1 && fov2 && los1 && los2;
    }

    bool IsLineOfSightClear(MountPoint mountPoint)
    {
        return IsLineOfSightClear(mountPoint, target);
    }

    bool IsLineOfSightClear(MountPoint mountPoint, Transform tgt)
    {
        if (mountPoint == null || tgt == null) return true;
        var origin = mountPoint.transform.position;
        var destination = tgt.position;
        var direction = (destination - origin).normalized;
        var distance = Vector3.Distance(origin, destination);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance))
        {
            if (hit.transform != tgt)
                return false;
        }
        return true;
    }
}