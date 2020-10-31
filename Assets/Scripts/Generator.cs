using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Generator : MonoBehaviour
{
    public GameObject WallPrefab;
    public GameObject WindowPrefab;
    public GameObject DoorPrefab;
    public GameObject FloorPrefab;
    public GameObject CornerPrefab;

    public Transform Master;
    public Text Textbox;

    public int MaxHeight = 20, MaxWidth = 20;

    private char[,] floorPlan;
    private int height, width;

    // Start is called before the first frame update
    void Start()
    {
        GenerateRandom();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("space")) {
            GenerateRandom();
        }
    }

    private void GenerateRandom() {
        DeleteChildren();

        height = RandomNumber(5, MaxHeight);
        width = RandomNumber(5, MaxWidth);

        floorPlan = new char[height, width];

        GenerateFloor();
        GenerateWalls();
        GenerateDoors();
        GenerateWindows();

        PrintFloorPlan();

        PlaceFloors();
        PlaceCorners();
        PlaceWalls();
    }

    private void GenerateWalls() {
        for (int i = 0; i < width; i++) {
            floorPlan[0, i] = 'X';
            floorPlan[height-1, i] = 'X';
        }

        for (int i = 0; i < height; i++) {
            floorPlan[i, 0] = 'X';
            floorPlan[i, width-1] = 'X';
        }

        floorPlan[0, 0] = 'C';
        floorPlan[0, width-1] = 'C';
        floorPlan[height-1, 0] = 'C';
        floorPlan[height-1, width-1] = 'C';
    }

    private void GenerateFloor() {
        for (int i = 0; i < height; i++) {
            for (int j = 0; j < width; j++) {
                floorPlan[i, j] = 'F';
            }
        }
    }

    private void GenerateDoors() {
        int wall = RandomNumber(1, 4);
        if (wall == 1) {
            floorPlan[0, RandomNumber(1, width - 2)] = 'D';
        } else if (wall == 2) {
            floorPlan[height - 1, RandomNumber(1, width - 2)] = 'D';
        } else if (wall == 3) {
            floorPlan[RandomNumber(1, height - 2), 0] = 'D';
        } else {
            floorPlan[RandomNumber(1, height - 2), width - 1] = 'D';
        }
    }

    private void GenerateWindows() {
        for (int i = 1; i < width - 1; i++) {
            if (RandomBool(.15f)) floorPlan[0, i] = 'W';
            if (RandomBool(.15f)) floorPlan[height - 1, i] = 'W';
        }
        for (int i = 1; i < height - 1; i++) {
            if (RandomBool(.15f)) floorPlan[i, 0] = 'W';
            if (RandomBool(.15f)) floorPlan[i, width - 1] = 'W';
        }
    }

    private void PrintFloorPlan() {
        string plan = "";
        for (int i = 0; i < height; i++) {
            for (int j = 0; j < width; j++) {
                plan += floorPlan[i, j] + " ";
            }
            plan += "\n";
        }

        Textbox.text = plan;
    }

    private void PlaceCorners() {
        Vector3 position = new Vector3(-(float)height / 2, 1.4f, -(float)width / 2);
        Quaternion rotation = Quaternion.identity;
        Instantiate(CornerPrefab, position, rotation, Master);

        position += new Vector3(height, 0, 0);
        Instantiate(CornerPrefab, position, rotation, Master);

        position += new Vector3(0, 0, width);
        Instantiate(CornerPrefab, position, rotation, Master);

        position += new Vector3(-height, 0, 0);
        Instantiate(CornerPrefab, position, rotation, Master);
    }

    private void PlaceWalls() {
        Vector3 position = new Vector3(-(float) height/2, 1.4f, -(float) width / 2) + Vector3.forward / 2;
        Quaternion rotation = Quaternion.identity;

        for (int i = 0; i < width; i++) {
            PlaceWall(ref position, ref rotation, Vector3.forward, floorPlan[0, i]);
        }

        position += new Vector3(.5f, 0, -.5f);
        rotation *= Quaternion.Euler(0, 90, 0);

        for (int i = 0; i < height; i++) {
            PlaceWall(ref position, ref rotation, Vector3.right, floorPlan[i, width - 1]);
        }

        position += new Vector3(-.5f, 0, -.5f);
        rotation *= Quaternion.Euler(0, 90, 0);

        for (int i = width - 1; i > -1; i--) {
            PlaceWall(ref position, ref rotation, -Vector3.forward, floorPlan[height - 1, i]);
        }

        position += new Vector3(-.5f, 0, .5f);
        rotation *= Quaternion.Euler(0, 90, 0);

        for (int i = height - 1; i > -1; i--) {
            PlaceWall(ref position, ref rotation, -Vector3.right, floorPlan[i, 0]);
        }
    }

    private Vector3 PlaceWall(ref Vector3 position, ref Quaternion rotation, Vector3 direction, char wallType) {

        if (wallType == 'X' || wallType == 'C') {
            Instantiate(WallPrefab, position, rotation, Master);
        } else if (wallType == 'W') {
            Instantiate(WindowPrefab, position, rotation, Master);
        } else if (wallType == 'D') {
            Instantiate(DoorPrefab, position, rotation, Master);
        }

        position += direction;
        return position;
    }

    private void PlaceFloors() {
        FloorPrefab.transform.localScale = new Vector3(height, 0.2f, width);
        Instantiate(FloorPrefab, new Vector3(), Quaternion.identity, Master);
    }

    private bool RandomBool() {
        if (RandomNumber(1, 2) == 1) {
            return true;
        } else {
            return false;
        }
    }

    private bool RandomBool(float successChance) {
        if (RandomNumber(0f, 1f) < successChance) {
            return true;
        } else {
            return false;
        }
    }

    private int RandomNumber(int min, int max) {
        return UnityEngine.Random.Range(min, max + 1);
    }

    private float RandomNumber(float min, float max) {
        return UnityEngine.Random.Range(min, max);
    }

    private void DeleteChildren() {
        foreach (Transform child in Master) {
            Destroy(child.gameObject);
        }
    }
}
