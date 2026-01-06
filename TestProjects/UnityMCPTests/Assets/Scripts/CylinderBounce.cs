using UnityEngine;

public class CylinderBounce : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Tooltip("Maximum height/distance the object will bounce")]
    public float height = 1f;
    
    [Tooltip("Speed of the bounce animation")]
    public float speed = 1f;
    
    [Tooltip("Direction vector for the bounce movement")]
    public Vector3 direction = Vector3.up;
    
    private Vector3 startPosition;
    private float timeOffset;
    
    void Start()
    {
        startPosition = transform.position;
        // Random offset so cylinders don't all bounce in sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
        
        // Normalize direction to ensure consistent movement
        if (direction.magnitude > 0.01f)
        {
            direction = direction.normalized;
        }
        else
        {
            direction = Vector3.up;
        }
    }
    
    void Update()
    {
        // Sine wave bounce in the specified direction
        float bounceAmount = Mathf.Sin((Time.time * speed) + timeOffset) * height;
        transform.position = startPosition + (direction * bounceAmount);
    }
}
