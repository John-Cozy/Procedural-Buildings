using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Generator : MonoBehaviour {

    public PrefabLibrary Library;
    public Settings Settings;

    public Transform Master;
    public Text Map;
    public Text Stats;
    public CanvasGroup UI;

    private static float WallHeight = 1.4f;
    private static float RoofHeight = 3.0f;

    private static int PlotHeight = 50, PlotWidth = 50;
    private static int MinRoomSize = 4, MaxRoomSize = 15;
    private static int RoomNumber = 4, FloorNumber = 2;

    private static bool RoofEnabled = false;

    private static List<Room>[] rooms;
    private static List<RoomConnection>[] connections;

    private bool showingMap = false;
    private enum Direction { LEFT, RIGHT, UP, DOWN }

    private struct RoomConnection {
        public Direction direction;
        public Room roomStart;
        public Room roomEnd;
        public float doorPosition;
    }

    private class Room {
        public int floor;

        public float xPos, yPos;
        public float width, height;
        public float left, right, top, bottom;

        public bool leftR = false, rightR = false, topR = false, bottomR = false;

        public static void AddRandomRoom(int floor) {
            while (true) {
                // Getting room and direction to branch off from
                Room adjacentRoom = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
                Direction dest = (Direction) RandomNumber(0, 3);

                // Preliminary checks
                if (adjacentRoom.RoomAlreadyInDirection(dest)) continue;  // Checking if room already present in direction
                if (adjacentRoom.RoomTooCloseToSide(dest)) continue;      // Checking if room is up against the sides of the plot in that direction

                float width, height, left, right, top, bottom;
                float xPos, yPos;

                bool leftR = false, rightR = false, topR = false, bottomR = false;

                if (dest == Direction.LEFT || dest == Direction.RIGHT) {
                    height = adjacentRoom.height;
                    yPos = adjacentRoom.yPos;

                    top = adjacentRoom.top;
                    bottom = adjacentRoom.bottom;

                    if (dest == Direction.LEFT) {
                        width = RandomNumber(MinRoomSize, adjacentRoom.left - MaxRoomSize < 0 ? adjacentRoom.left : RandomNumber(MinRoomSize, MaxRoomSize));
                        xPos = adjacentRoom.left - (width / 2);

                        left = adjacentRoom.left - width;
                        right = adjacentRoom.left;

                        adjacentRoom.leftR = true;
                        rightR = true;
                    } else {
                        width = RandomNumber(MinRoomSize, adjacentRoom.right + MaxRoomSize > GetPlotWidth() ? GetPlotWidth() - adjacentRoom.right : RandomNumber(MinRoomSize, MaxRoomSize));
                        xPos = adjacentRoom.right + (width / 2);

                        left = adjacentRoom.right;
                        right = adjacentRoom.right + width;

                        adjacentRoom.rightR = true;
                        leftR = true;
                    }
                } else {
                    width = adjacentRoom.width;
                    xPos = adjacentRoom.xPos;

                    left = adjacentRoom.left;
                    right = adjacentRoom.right;

                    if (dest == Direction.UP) {
                        height = RandomNumber(MinRoomSize, adjacentRoom.top - MaxRoomSize < 0 ? adjacentRoom.top : MaxRoomSize);
                        yPos = adjacentRoom.top - (height / 2);

                        top = adjacentRoom.top - height;
                        bottom = adjacentRoom.top;

                        adjacentRoom.topR = true;
                        bottomR = true;
                    } else {
                        height = RandomNumber(MinRoomSize, adjacentRoom.bottom + MaxRoomSize > GetPlotHeight() ? GetPlotHeight() - adjacentRoom.bottom : RandomNumber(MinRoomSize, MaxRoomSize));
                        yPos = adjacentRoom.bottom + (height / 2);

                        top = adjacentRoom.bottom;
                        bottom = adjacentRoom.bottom + height;

                        adjacentRoom.bottomR = true;
                        topR = true;
                    }
                }

                Room newRoom = new Room {
                    xPos = xPos,
                    yPos = yPos,
                    width = width,
                    height = height,
                    floor = floor,

                    left = left,
                    right = right,
                    top = top,
                    bottom = bottom,

                    leftR = leftR,
                    rightR = rightR,
                    topR = topR,
                    bottomR = bottomR
                };

                newRoom.GenerateConnections();

                rooms[floor].Add(newRoom);

                break;
            }
        }

        public bool RoomAlreadyInDirection(Direction dest) {
            switch (dest) {
                case Direction.LEFT:
                    return leftR;
                case Direction.RIGHT:
                    return rightR;
                case Direction.UP:
                    return topR;
                case Direction.DOWN:
                    return bottomR;
            }

            return false;
        }

        public bool RoomTooCloseToSide(Direction dest) {
            switch (dest) {
                case Direction.LEFT:
                    return left - MinRoomSize - 1 < 0 ? true : false;
                case Direction.RIGHT:
                    return right + MinRoomSize + 1 > GetPlotWidth() ? true : false;
                case Direction.UP:
                    return top - MinRoomSize - 1 < 0 ? true : false;
                case Direction.DOWN:
                    return bottom + MinRoomSize + 1 > GetPlotHeight() ? true : false;
                default:
                    throw new InvalidEnumArgumentException("Direction not valid");
            }
        }

        public void GenerateConnections() {
            foreach (Room nextRoom in rooms[floor]) {
                // Check that the walls are lined up and that the rooms are adjacent to one another
                if (nextRoom.bottom == top && nextRoom.right > left && right > nextRoom.left) {
                    float doorPosition = GenerateDoorPosition(left > nextRoom.left ? left : nextRoom.left, right < nextRoom.right ? right : nextRoom.right);

                    topR = true;
                    nextRoom.bottomR = true;

                    connections[floor].Add(new RoomConnection {
                        direction = Direction.UP,
                        roomStart = this,
                        roomEnd = nextRoom,
                        doorPosition = doorPosition
                    });

                } else if (nextRoom.top == bottom && nextRoom.right > left && right > nextRoom.left) {
                    float doorPosition = GenerateDoorPosition(left > nextRoom.left ? left : nextRoom.left, right < nextRoom.right ? right : nextRoom.right);

                    bottomR = true;
                    nextRoom.topR = true;

                    connections[floor].Add(new RoomConnection {
                        direction = Direction.DOWN,
                        roomStart = this,
                        roomEnd = nextRoom,
                        doorPosition = doorPosition
                    });

                } else if (nextRoom.right == left && nextRoom.top < bottom && top < nextRoom.bottom) {
                    float doorPosition = GenerateDoorPosition(top > nextRoom.top ? top : nextRoom.top, bottom < nextRoom.bottom ? bottom : nextRoom.bottom);

                    leftR = true;
                    nextRoom.rightR = true;

                    connections[floor].Add(new RoomConnection {
                        direction = Direction.LEFT,
                        roomStart = this,
                        roomEnd = nextRoom,
                        doorPosition = doorPosition
                    });

                } else if (nextRoom.left == right && nextRoom.top < bottom && top < nextRoom.bottom) {
                    float doorPosition = GenerateDoorPosition(top > nextRoom.top ? top : nextRoom.top, bottom < nextRoom.bottom ? bottom : nextRoom.bottom);

                    rightR = true;
                    nextRoom.leftR = true;

                    connections[floor].Add(new RoomConnection {
                        direction = Direction.RIGHT,
                        roomStart = this,
                        roomEnd = nextRoom,
                        doorPosition = doorPosition
                    });
                }
            }
        }

        private bool IsConnectionAlreadyAdded(Room room) {
            foreach (RoomConnection c in connections[floor]) {
                if ((this == c.roomStart && room == c.roomEnd) || (room == c.roomStart && this == c.roomEnd)) {
                    return true;
                }
            }

            return false;
        }

        private float GenerateDoorPosition(float room1Side, float room2Side) {
            return RandomNumber(room1Side + 1, room2Side - 1);
        }
    }

    // Start is called before the first frame update
    void Start() {
        UpdateSettings();

        GenerateAndPlaceRandomBuilding();
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("space")) {
            UpdateSettings();

            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";
        } else if (Input.GetKeyDown("m")) {
            if (showingMap) {
                UI.alpha = 0f;
                UI.blocksRaycasts = false;
                showingMap = false;
            } else {
                UI.alpha = 1f;
                UI.blocksRaycasts = true;
                showingMap = true;
            }
        } else if (Input.GetKeyDown("h")) {
            UpdateSettings();

            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            for (int i = 0; i < 30; i++) GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";
        }
    }

    private void UpdateSettings() {
        WallHeight = Settings.WallHeight;
        RoofHeight = Settings.RoofHeight;
        PlotHeight = Settings.PlotHeight;
        PlotWidth = Settings.PlotWidth;
        MinRoomSize = Settings.MinRoomSize;
        MaxRoomSize = Settings.MaxRoomSize;
        RoomNumber = Settings.RoomNumber;
        FloorNumber = Settings.FloorNumber;
        RoofEnabled = Settings.RoofEnabled;
    }

    private void GenerateAndPlaceRandomBuilding() {
        DeleteChildren();
        GenerateRooms();
        PlaceFloorPlan();
    }

    private void DeleteChildren() {
        foreach (Transform child in Master) {
            Destroy(child.gameObject);
        }
    }

    private void GenerateRooms() {
        rooms = new List<Room>[FloorNumber];
        connections = new List<RoomConnection>[FloorNumber];

        for (int floor = 0; floor < FloorNumber; floor++) {
            rooms[floor] = new List<Room>();
            connections[floor] = new List<RoomConnection>();
        }

        float xPos = GetPlotWidth() / 2;
        float yPos = GetPlotHeight() / 2;
        float width =  RandomNumber(MinRoomSize, MaxRoomSize);
        float height = RandomNumber(MinRoomSize, MaxRoomSize);

        rooms[0].Add(new Room {
            xPos = xPos,
            yPos = yPos,
            width = width,
            height = height,
            floor = 0,

            left =   xPos - (width / 2),
            right =  xPos + (width / 2),
            top =    yPos - (height / 2),
            bottom = yPos + (height / 2)
        });

        for (int i = 1; i < RoomNumber; i++) {
            Room.AddRandomRoom(0);
        }

        for (int i = 1; i < FloorNumber; i++) {
            foreach (Room r in rooms[0]) {
                Room newRoom = new Room {
                    xPos = r.xPos,
                    yPos = r.yPos,
                    width = r.width,
                    height = r.height,
                    floor = i,

                    left = r.left,
                    right = r.right,
                    top = r.top,
                    bottom = r.bottom,

                    leftR = r.leftR,
                    rightR = r.rightR,
                    topR = r.topR,
                    bottomR = r.bottomR
                };

                newRoom.GenerateConnections();

                rooms[i].Add(newRoom);
            }
        }
    }

    private void PlaceFloorPlan() {
        PlaceGrass();
        PlaceFloors();
        PlaceCorners();
        PlaceWalls();
        PlaceDoors();
        PlaceWindows();
        if (RoofEnabled) PlaceRoofing();
    }

    private void PlaceGrass() {
        GameObject grass = Instantiate(Library.Grass, new Vector3(PlotHeight / 2, -.1f, PlotWidth / 2), Quaternion.identity, Master);
        grass.transform.localScale = new Vector3(PlotHeight, 0.2f, PlotWidth);
    }

    private void PlaceCorners() {
        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                PlaceCorner(new Vector3(r.top,    WallHeight + (RoofHeight * i), r.left));
                PlaceCorner(new Vector3(r.top,    WallHeight + (RoofHeight * i), r.right));
                PlaceCorner(new Vector3(r.bottom, WallHeight + (RoofHeight * i), r.left));
                PlaceCorner(new Vector3(r.bottom, WallHeight + (RoofHeight * i), r.right));
            }
        }
    }

    private void PlaceCorner(Vector3 cornerPoint) {
        Instantiate(Library.Corner, cornerPoint, Quaternion.identity, Master);
    }

    private void PlaceFloors() {
        for (int i = 0; i < FloorNumber; i++) {
            for (int j = 0; j < rooms[i].Count; j++) {
                Room r = rooms[i][j];

                GameObject floor = Instantiate(Library.Floor, new Vector3(r.yPos, RoofHeight * i, r.xPos), Quaternion.identity, Master);
                floor.transform.localScale = new Vector3(r.height, 0.2f + (j * 0.001f), r.width);
            }
        }
    }

    private void PlaceWalls() {
        GameObject wall;

        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                wall = Instantiate(Library.Wall, new Vector3(r.top, WallHeight + (RoofHeight * i), (r.left + r.right) / 2), Quaternion.Euler(0, 0, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.width);

                wall = Instantiate(Library.Wall, new Vector3((r.top + r.bottom) / 2, WallHeight + (RoofHeight * i), r.right), Quaternion.Euler(0, 90, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.height);

                wall = Instantiate(Library.Wall, new Vector3(r.bottom, WallHeight + (RoofHeight * i), (r.left + r.right) / 2), Quaternion.Euler(0, 180, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.width);

                wall = Instantiate(Library.Wall, new Vector3((r.top + r.bottom) / 2, WallHeight + (RoofHeight * i), r.left), Quaternion.Euler(0, 270, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.height);
            }
        }
    }

    private void PlaceDoors() {
        Room start = rooms[0][0];

        if (start.RoomAlreadyInDirection(Direction.UP) != true) {
            PlaceDoor(new Vector3(start.top, WallHeight, start.left + (start.width / 2)), Quaternion.Euler(0, 0, 0), true);
        } else if (start.RoomAlreadyInDirection(Direction.DOWN) != true) {
            PlaceDoor(new Vector3(start.bottom, WallHeight, start.left + (start.width / 2)), Quaternion.Euler(0, 180, 0), true);
        } else if (start.RoomAlreadyInDirection(Direction.LEFT) != true) {
            PlaceDoor(new Vector3(start.top + (start.height / 2), WallHeight, start.left), Quaternion.Euler(0, 270, 0), true);
        } else if (start.RoomAlreadyInDirection(Direction.RIGHT) != true) {
            PlaceDoor(new Vector3(start.top + (start.height / 2), WallHeight, start.right), Quaternion.Euler(0, 90, 0), true);
        }

        for (int floor = 0; floor < FloorNumber; floor++) {
            foreach (RoomConnection c in connections[floor]) {
                switch (c.direction) {
                    case Direction.LEFT:
                        PlaceDoor(new Vector3(c.doorPosition, WallHeight + (RoofHeight * floor), c.roomStart.left), Quaternion.Euler(0, 270, 0), false);
                        break;
                    case Direction.RIGHT:
                        PlaceDoor(new Vector3(c.doorPosition, WallHeight + (RoofHeight * floor), c.roomStart.right), Quaternion.Euler(0, 90, 0), false);
                        break;
                    case Direction.UP:
                        PlaceDoor(new Vector3(c.roomStart.top, WallHeight + (RoofHeight * floor), c.doorPosition), Quaternion.Euler(0, 0, 0), false);
                        break;
                    case Direction.DOWN:
                        PlaceDoor(new Vector3(c.roomStart.bottom, WallHeight + (RoofHeight * floor), c.doorPosition), Quaternion.Euler(0, 180, 0), false);
                        break;
                }
            }
        }
    }

    private void PlaceDoor(Vector3 position, Quaternion rotation, bool outside) {
        Instantiate(outside ? Library.OutsideDoor : Library.InsideDoor, position, rotation, Master);
    }

    private void PlaceWindows() {
        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                if (!r.leftR)   PlaceWindow(new Vector3(r.top + RandomNumber(1, r.height - 1), WallHeight + (RoofHeight * i), r.left),   Quaternion.Euler(0, 270, 0));
                if (!r.rightR)  PlaceWindow(new Vector3(r.top + RandomNumber(1, r.height - 1), WallHeight + (RoofHeight * i), r.right),  Quaternion.Euler(0, 90, 0));
                if (!r.topR)    PlaceWindow(new Vector3(r.top,    WallHeight + (RoofHeight * i), r.left + RandomNumber(1, r.width - 1)), Quaternion.Euler(0, 0, 0));
                if (!r.bottomR) PlaceWindow(new Vector3(r.bottom, WallHeight + (RoofHeight * i), r.left + RandomNumber(1, r.width - 1)), Quaternion.Euler(0, 180, 0));
            }
        }
    }

    private void PlaceWindow(Vector3 position, Quaternion rotation) {
        Instantiate(Library.Window, position, rotation, Master);
    }

    private void PlaceRoofing() {
        foreach (Room r in rooms[rooms.Length - 1]) {
            GameObject roof = Instantiate(Library.Floor, new Vector3(r.yPos, RoofHeight, r.xPos), Quaternion.identity, Master);
            roof.transform.localScale = new Vector3(r.height, 0.2f, r.width);
        }
    }

    private static bool RandomBool(float successChance) {
        if (RandomNumber(0f, 1f) < successChance) {
            return true;
        } else {
            return false;
        }
    }

    public static int RandomNumber(int min, int max) {
        return Random.Range(min, max + 1);
    }

    private static float RandomNumber(float min, float max) {
        return Random.Range(min, max);
    }

    private static int GetPlotWidth() {
        return PlotWidth;
    }

    private static int GetPlotHeight() {
        return PlotHeight;
    }
}
