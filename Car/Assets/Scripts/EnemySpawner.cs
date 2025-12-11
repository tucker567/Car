using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject AiCarPrefab;
    public float spawnInterval = 5f;
    private float nextSpawnTime;

     public Transform player;
     public bool autoFindPlayer;
     public string playerTag = "Player";

     // Random spawn settings
     public float spawnRadius = 100f; // Radius around player to spawn
     public float heightOffset = 10f; // Spawn above player to avoid dunes
     public int carsPerWave = 1; // How many cars per interval
     public int Carsactive;
     public TMPro.TextMeshProUGUI carscounter;

     void Update()
     {
         // Late player spawn support
            if (player == null && autoFindPlayer)
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    player = tagged.transform;
                    Debug.Log("Spawner: Attached to player '" + player.name + "' via tag '" + playerTag + "'.");
                }
            }

         // Spawn AI cars at interval
         if (Time.time >= nextSpawnTime)    
            {
                SpawnAICars();
                nextSpawnTime = Time.time + spawnInterval;
            }
     }

    void SpawnAICars()
    {
        // Prefer spawning relative to player if available
        if (player != null)
        {
            for (int i = 0; i < Mathf.Max(1, carsPerWave); i++)
            {
                // Random point on XZ plane within radius
                var randomDir = Random.insideUnitCircle * spawnRadius;
                var spawnPos = new Vector3(player.position.x + randomDir.x,
                                           player.position.y + heightOffset,
                                           player.position.z + randomDir.y);

                // Face roughly towards player
                var lookDir = (player.position - spawnPos);
                lookDir.y = 0f;
                var rotation = lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir.normalized) : Quaternion.identity;

                Instantiate(AiCarPrefab, spawnPos, rotation);
                Carsactive++;
                carscounter.text = "Cars Active: " + Carsactive;
            }
            return;
        }

        // Fallback: spawn around this spawner's position if no player
        for (int i = 0; i < Mathf.Max(1, carsPerWave); i++)
        {
            var randomDir = Random.insideUnitCircle * spawnRadius;
            var basePos = transform.position;
            var spawnPos = new Vector3(basePos.x + randomDir.x,
                                       basePos.y + heightOffset,
                                       basePos.z + randomDir.y);

            var rotation = Quaternion.identity;
            Instantiate(AiCarPrefab, spawnPos, rotation);
        }
    }

    
}
