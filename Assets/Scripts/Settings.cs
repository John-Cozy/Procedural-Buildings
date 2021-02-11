using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour {
    public float WallHeight = 1.4f, RoofHeight = 3.0f;

    public int PlotHeight = 50, PlotWidth = 50;
    public int MinRoomSize = 4, MaxRoomSize = 15;
    public int RoomNumber = 4, FloorNumber = 2;

    public int OutsideDoorNumber = 1;
    public int BalconyRoomNumber = 1;
    public int WindowNumber = 1;

    public bool RoofEnabled = false;
    public bool RandomWindowNumber = false;
    public bool PathfindDoors = true;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        
    }
}
