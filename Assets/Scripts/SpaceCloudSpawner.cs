using UnityEngine;

public class SpaceCloudSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject cloudPrefab;
    
    [Header("Cloud Layers")]
    [SerializeField] private CloudLayer[] layers = new CloudLayer[3];
    
    private Transform cameraTransform;
    
    private void Start()
    {
        cameraTransform = Camera.main.transform;
        if (cameraTransform == null)
        {
            Debug.LogError("Main Camera not found!");
            return;
        }
        
        foreach (CloudLayer layer in layers)
        {
            if (layer != null)
            {
                SpawnLayer(layer);
            }
        }
    }
    
    private void SpawnLayer(CloudLayer layer)
    {
        layer.clouds = new SpaceCloud[layer.cloudCount];
        layer.cloudPositions = new Vector3[layer.cloudCount];
        
        GameObject layerParent = new GameObject(layer.layerName);
        layerParent.transform.SetParent(transform);
        layerParent.transform.localPosition = Vector3.zero;
        
        for (int i = 0; i < layer.cloudCount; i++)
        {
            Vector3 spawnPos = GetValidSpawnPosition(layer, i, true);
            layer.cloudPositions[i] = spawnPos;
            
            GameObject cloudObj = Instantiate(cloudPrefab, spawnPos, Quaternion.identity, layerParent.transform);
            cloudObj.name = $"{layer.layerName}_Cloud_{i}";
            
            float randomSize = Random.Range(layer.sizeRange.x, layer.sizeRange.y);
            cloudObj.transform.localScale = Vector3.one * randomSize;
            
            float cloudSpeed = layer.baseSpeed * Random.Range(layer.speedVariation.x, layer.speedVariation.y);
            float rotationSpeed = layer.enableRotation ? Random.Range(layer.rotationSpeedRange.x, layer.rotationSpeedRange.y) : 0f;
            Vector3 moveDirection = -cameraTransform.forward;

            SpaceCloud cloud = cloudObj.AddComponent<SpaceCloud>();
            cloud.Initialize(cloudSpeed, moveDirection, layer.enableRotation, rotationSpeed);

            // SetMaterial creates the instance, owns its lifetime, and triggers fade-in
            if (layer.materials != null && layer.materials.Length > 0)
            {
                Material randomMaterial = layer.materials[Random.Range(0, layer.materials.Length)];
                cloud.SetMaterial(randomMaterial);
            }
            
            layer.clouds[i] = cloud;
        }
    }
    
    private Vector3 GetValidSpawnPosition(CloudLayer layer, int currentIndex, bool initialSpawn)
    {
        Vector3 position = Vector3.zero;
        bool validPosition = false;
        int maxAttempts = 30;
        int attempts = 0;
        
        while (!validPosition && attempts < maxAttempts)
        {
            position = GetRandomSpawnPosition(layer, initialSpawn);
            validPosition = true;
            
            for (int i = 0; i < currentIndex; i++)
            {
                float distance = Vector3.Distance(position, layer.cloudPositions[i]);
                if (distance < layer.minDistanceBetweenClouds)
                {
                    validPosition = false;
                    break;
                }
            }
            
            attempts++;
        }
        
        return position;
    }
    
    private Vector3 GetRandomSpawnPosition(CloudLayer layer, bool initialSpawn)
    {
        float distance = initialSpawn 
            ? Random.Range(layer.spawnDistanceMin, layer.spawnDistanceMax) 
            : Random.Range(layer.spawnDistanceMax * 0.8f, layer.spawnDistanceMax);
        
        Vector3 forward = cameraTransform.forward;
        Vector3 basePos = cameraTransform.position + forward * distance;
        
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(0f, layer.spawnRadiusXY);
        
        Vector3 right = cameraTransform.right;
        Vector3 up = cameraTransform.up;
        
        Vector3 randomOffset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
        
        return basePos + randomOffset;
    }
    
    private void Update()
    {
        if (cameraTransform == null) return;
        
        foreach (CloudLayer layer in layers)
        {
            if (layer == null || layer.clouds == null) continue;
            
            UpdateLayer(layer);
        }
    }
    
    private void UpdateLayer(CloudLayer layer)
    {
        for (int i = 0; i < layer.clouds.Length; i++)
        {
            SpaceCloud cloud = layer.clouds[i];
            if (cloud == null) continue;
            
            float distanceAlongForward = Vector3.Dot(
                cloud.transform.position - cameraTransform.position,
                cameraTransform.forward
            );
            
            if (distanceAlongForward < layer.despawnDistanceBehind)
            {
                Vector3 newPos = GetValidSpawnPosition(layer, i, false);
                layer.cloudPositions[i] = newPos;
                cloud.transform.position = newPos;
                
                float randomSize = Random.Range(layer.sizeRange.x, layer.sizeRange.y);
                cloud.transform.localScale = Vector3.one * randomSize;
                
                float cloudSpeed = layer.baseSpeed * Random.Range(layer.speedVariation.x, layer.speedVariation.y);
                float rotationSpeed = layer.enableRotation ? Random.Range(layer.rotationSpeedRange.x, layer.rotationSpeedRange.y) : 0f;
                
                cloud.SetSpeed(cloudSpeed);
                cloud.SetDirection(-cameraTransform.forward);
                cloud.SetRotation(layer.enableRotation, rotationSpeed);

                // SetMaterial handles material instance lifecycle and triggers fade-in
                if (layer.materials != null && layer.materials.Length > 0)
                {
                    Material randomMaterial = layer.materials[Random.Range(0, layer.materials.Length)];
                    cloud.SetMaterial(randomMaterial);
                }
            }
            else
            {
                layer.cloudPositions[i] = cloud.transform.position;
            }
        }
    }
}
