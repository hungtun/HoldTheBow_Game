using UnityEngine;

public class RoomMove : MonoBehaviour
{
    public Vector2 camereChange;
    public Vector3 playerChange;
    private CamMovement cam;
    void Start()
    {
        cam = Camera.main.GetComponent<CamMovement>();
        Debug.Log(cam);
    }

    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D other) 
    {
        if (other.CompareTag("Player"))
        {
            cam.minPosition += camereChange;
            cam.maxPosition += camereChange;
            other.transform.position += playerChange;

            Debug.Log($"Room Change {cam.minPosition} - {cam.maxPosition}");
        
        }
    }
}
