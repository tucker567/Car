using UnityEngine;


public class Gun : MonoBehaviour
{
    public GameObject shotPrefab;
    public Transform gunPoint1;
    public Transform gunPoint2;
    public float fireRate;

    bool firing;
    float fireTimer;

    int gunPointIndex;

    void Update()
    {
        if (firing)
        {
            while (fireTimer >= 1 / fireRate)
            {
                SpawnShot();
                fireTimer -= 1 / fireRate;
            }

            fireTimer += Time.deltaTime;
            firing = false;
        }
        else
        {
            if (fireTimer < 1 / fireRate)
            {
                fireTimer += Time.deltaTime;
            }
            else
            {
                fireTimer = 1 / fireRate;
            }
        }
    }

    void SpawnShot()
    {
        Transform gunPoint = null;
        if (gunPointIndex == 0 && gunPoint1 != null)
        {
            gunPoint = gunPoint1;
        }
        else if (gunPointIndex == 1 && gunPoint2 != null)
        {
            gunPoint = gunPoint2;
        }

        if (gunPoint != null)
        {
            Instantiate(shotPrefab, gunPoint.position, gunPoint.rotation);
        }

        gunPointIndex = (gunPointIndex + 1) % 2;
    }

    public void Fire()
    {
        firing = true;
    }
}