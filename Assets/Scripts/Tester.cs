using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tester : MonoBehaviour
{
    public GameObject Cube;

    // Start is called before the first frame update
    void Start()
    {
        Bounds bounds1 = new Bounds(new Vector3(0, 0, 0), new Vector3(0.99f, 0, 0.99f));
        GameObject cube1 = Instantiate(Cube);
        cube1.transform.position = bounds1.center;
        cube1.transform.localScale = bounds1.size;

        Bounds bounds2 = new Bounds(new Vector3(1, 0, 1), new Vector3(0.99f, 0, 0.99f));
        GameObject cube2 = Instantiate(Cube);
        cube2.transform.position = bounds2.center;
        cube2.transform.localScale = bounds2.size;

        Debug.Log("Intesect? " + bounds1.Intersects(bounds2));
        Debug.Log("Extents " + bounds1.extents);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
