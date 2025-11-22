using UnityEngine;

public class CursorPrefab : MonoBehaviour
{
public GameObject cursorPrefab;
    private GameObject spawnedCursor;
    public float distanceFromCamera = 10f;

    void Start()
    {
        Debug.Log("Script đã chạy Start()");
        Cursor.visible = false;

        spawnedCursor = Instantiate(cursorPrefab);
        spawnedCursor.transform.localScale = Vector3.one * 0.5f;
    }

    void Update()
    {
        Debug.Log("Update đang chạy");

        if (Camera.main == null)
        {
            Debug.LogError("Không tìm thấy MainCamera!");
            return;
        }

        Vector3 mousePos = Input.mousePosition;
        mousePos.z = distanceFromCamera;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);

        Debug.Log("Vị trí chuột: " + worldPos);
        spawnedCursor.transform.position = worldPos;
    }
}
