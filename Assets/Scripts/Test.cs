using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
  [SerializeField] private GameObject enemyPrefab;
  [SerializeField] private int maxEnemies = 10;
  [SerializeField] private float spawnInterval = 2f;

  private int currentEnemies = 0;
  private float timer = 0f;

  void Update()
  {
    timer += Time.deltaTime;

    if (timer >= spawnInterval)
    {
      GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
      currentEnemies = enemies.Length;
      if (currentEnemies < maxEnemies)
      {
        Vector3 spawnPos = new Vector3(Random.Range(-5f, 5f), 0f, 0f);
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
      }

      timer = 0f;
    }
  }
}