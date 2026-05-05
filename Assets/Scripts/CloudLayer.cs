using UnityEngine;

[System.Serializable]
public class CloudLayer
{
    [Header("Layer Identity")]
    public string layerName = "Layer";
    
    [Header("Spawn Settings")]
    public int cloudCount = 15;
    public float spawnDistanceMin = 50f;
    public float spawnDistanceMax = 150f;
    public float spawnRadiusXY = 60f;
    public float minDistanceBetweenClouds = 15f;
    
    [Header("Cloud Size")]
    public Vector2 sizeRange = new Vector2(10f, 40f);
    
    [Header("Movement")]
    public float baseSpeed = 15f;
    public Vector2 speedVariation = new Vector2(0.8f, 1.2f);
    public float despawnDistanceBehind = -30f;
    
    [Header("Rotation")]
    public bool enableRotation = true;
    public Vector2 rotationSpeedRange = new Vector2(5f, 15f);
    
    [Header("Materials")]
    public Material[] materials;
    
    [HideInInspector]
    public SpaceCloud[] clouds;
    [HideInInspector]
    public Vector3[] cloudPositions;
}
