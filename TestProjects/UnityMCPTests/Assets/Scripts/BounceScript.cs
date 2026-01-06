using UnityEngine;

public class BounceScript : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Tooltip("Maximum height the object will bounce to")]
    public float bounceHeight = 2f;
    
    [Tooltip("Speed of the bounce animation")]
    public float bounceSpeed = 2f;
    
    private Vector3 startPosition;
    private float timeOffset;
    
    void Start()
    {
        startPosition = transform.position;
        // Random offset so spheres don't all bounce in sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void Update()
    {
        // Simple sine wave bounce
        float yOffset = Mathf.Sin((Time.time * bounceSpeed) + timeOffset) * bounceHeight;
        transform.position = startPosition + new Vector3(0, yOffset, 0);
    }
}
