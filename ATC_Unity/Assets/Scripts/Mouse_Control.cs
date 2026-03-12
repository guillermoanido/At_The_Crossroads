using UnityEngine;

public class Mouse_Control : MonoBehaviour

{
    [SerializeField] private Camera mainCamera;
    private GameObject draggedObject;
    private Vector3 offset;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Debug.Log("DragManager started");
    }

    void Update()
    {
        // Left click down
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Left click detected");
            TryPickUpObject();
        }

        // Left click held
        if (Input.GetMouseButton(0) && draggedObject != null)
        {
            DragObject();
        }

        // Left click released
        if (Input.GetMouseButtonUp(0) && draggedObject != null)
        {
            Debug.Log("Released: " + draggedObject.name);
            draggedObject = null;
        }
    }

    void TryPickUpObject()
    {
        // Get mouse position in world space
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -mainCamera.transform.position.z; // Distance from camera
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        // Cast a ray at that position (2D)
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        if (hit.collider != null)
        {
            Debug.Log("Hit object: " + hit.collider.gameObject.name);
            draggedObject = hit.collider.gameObject;
            offset = draggedObject.transform.position - worldPos;
        }
        else
        {
            Debug.Log("No object hit");
        }
    }

    void DragObject()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);

        draggedObject.transform.position = worldPos + offset;
    }
}