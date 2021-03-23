using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UserControls : MonoBehaviour {
    public Text Map;
    public Text Stats;
    public CanvasGroup UI;

    public Transform MainCamera;

    public float Radius = 2.0f;
    public float RadiusSpeed = 0.5f;
    public float RotationSpeed = 80.0f;

    private int currentFloor;
    private bool showingMap;
    private Vector3 DesiredPosition;
    private Vector3 Center;

    void Start() {
        Generator.Singleton.GenerateAndPlaceRandomBuilding();
        currentFloor = Generator.GetFloorNumber();

        Center = transform.position + new Vector3(Settings.Singleton.PlotWidth / 2, 0, Settings.Singleton.PlotHeight / 2);

        MainCamera.position = (MainCamera.position - Center).normalized * Radius + Center;
    }

    void Update() {
        OrbitPlot();
        FloorControls();
        if (Input.GetKeyDown("space")) {
            var stopwatch = new System.Diagnostics.Stopwatch();

            Generator.ResetID();

            stopwatch.Start();
            Generator.Singleton.GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            currentFloor = Generator.GetFloorNumber();

        } else if (Input.GetKeyDown("m")) {
            ToggleUI();
        } else if (Input.GetKeyDown("h")) {
            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            for (int i = 0; i < 30; i++) Generator.Singleton.GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";
        }
    }

    private void OrbitPlot() {
        MainCamera.RotateAround(Center, Vector3.up, RotationSpeed * Time.deltaTime);
        DesiredPosition = (MainCamera.position - Center).normalized * Radius + Center;
        MainCamera.position = Vector3.MoveTowards(MainCamera.position, DesiredPosition, Time.deltaTime * RadiusSpeed);
        MainCamera.LookAt(Center);
    }


    private void FloorControls() {
        if (Input.GetKeyDown(KeyCode.DownArrow) && currentFloor != -1) {
            GameObject[] roofs = GameObject.FindGameObjectsWithTag("Roof");

            foreach (GameObject r in roofs) {
                if (r.name == "Floor " + (currentFloor - 1) + " Roof") {
                    r.transform.position += new Vector3(0, 0, 1000);
                }
            }

            GameObject floor = GameObject.Find("Floor " + currentFloor);
            if (floor) floor.transform.position += new Vector3(0, 0, 1000);

            currentFloor--;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) && currentFloor != Generator.GetFloorNumber()) {
            GameObject[] roofs = GameObject.FindGameObjectsWithTag("Roof");

            foreach (GameObject r in roofs) {
                if (r.name == "Floor " + (currentFloor) + " Roof") {
                    r.transform.position -= new Vector3(0, 0, 1000);
                }
            }

            GameObject floor = GameObject.Find("Floor " + (currentFloor + 1));
            if (floor) floor.transform.position -= new Vector3(0, 0, 1000);

            currentFloor++;
        }
    }

    private void ToggleUI() {
        if (showingMap) {
            UI.alpha = 0f;
            UI.blocksRaycasts = false;
            showingMap = false;
        } else {
            UI.alpha = 1f;
            UI.blocksRaycasts = true;
            showingMap = true;
        }
    }
}
