using UnityEngine;

public class Bounce : MonoBehaviour
{
    public float height = 1f;
    public float speed = 1f;
    
    private Vector3 startPosition;
    private float timeOffset;
    
    void Start()
    {
        startPosition = transform.position;
        // Random offset so cubes don't all bounce in sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void Update()
    {
        // Simple sine wave bounce
        float y = startPosition.y + Mathf.Sin((Time.time * speed) + timeOffset) * height;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);
    }
}
