using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    [Header("Torch Flicker Settings")]
    public float minIntensity = 2.5f;
    public float maxIntensity = 4.2f;
    public float flickerSpeed = 8f;

    private Light pointLight;
    private float baseIntensity;

    void Start()
    {
        pointLight = GetComponentInChildren<Light>();
        if (pointLight != null)
        {
            baseIntensity = pointLight.intensity;
        }
    }

    void Update()
    {
        if (pointLight != null)
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
            pointLight.intensity = baseIntensity + noise * (maxIntensity - minIntensity);
        }
    }
}