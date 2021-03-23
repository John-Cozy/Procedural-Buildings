using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour {
    [Header("Building Height")]
    public float WallHeight = 1.4f;
    public float RoofHeight = 3.0f;

    [Header("Plot & Building Size")]
    public int PlotHeight = 50;
    public int PlotWidth = 50;
    public int MinRoomSize = 4, MaxRoomSize = 15;
    public int RoomNumber = 4, FloorNumber = 2;

    [Header("Building Features")]
    public int OutsideDoorNum = 1;
    public int BalconyRoomNum = 1;
    public int RemoveRoomNum = 0;
    public int WindowNum = 1;

    [Header("Additional Settings")]
    public bool RoofEnabled = false;
    public bool RandomiseWindows = false;
    public bool PathfindDoors = true;

    public static Settings Singleton;

    void Start() {
        Singleton = this;
    }
}
