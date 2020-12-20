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

    private static List<GameObject>[] roomObjects;

    private bool showingMap = false;

    private enum Direction { LEFT, RIGHT, UP, DOWN }

    private struct RoomConnection {
        public Direction direction;
        public Room roomStart;
        public Room roomEnd;
        public bool isDoorValid;
        public float doorPosition;
    }

    private class Room {
        // Ensures there's a slight gap between bounds to make sure they don't intersect
        private const float BOUNDS_GAP = 0.001f;

        // The room's dimensions
        private Bounds bounds;

        // Any connections to other rooms the room has
        private readonly Dictionary<Direction, bool> directionBlocked = new Dictionary<Direction, bool> {
            { Direction.UP,    false },
            { Direction.DOWN,  false },
            { Direction.LEFT,  false },
            { Direction.RIGHT, false },
        };

        public int floor;

        public float Top    => bounds.min.z - BOUNDS_GAP;
        public float Bottom => bounds.max.z + BOUNDS_GAP;
        public float Left   => bounds.min.x - BOUNDS_GAP;
        public float Right  => bounds.max.x + BOUNDS_GAP;
        public float X      => bounds.center.x;
        public float Z      => bounds.center.z;
        public float Width  => bounds.size.x;
        public float Height => bounds.size.z;

        // - - - Adding Rooms - - - //

        public static void AddFirstRoom() {
            float xPos = GetPlotWidth()  / 2;
            float zPos = GetPlotHeight() / 2;
            float width =  RandomNumber(MinRoomSize, MaxRoomSize) - BOUNDS_GAP * 2;
            float height = RandomNumber(MinRoomSize, MaxRoomSize) - BOUNDS_GAP * 2;

            rooms[0].Add(new Room {
                floor = 0,
                bounds = new Bounds(new Vector3(xPos, 0, zPos), new Vector3(width, 0, height))
            });
        }

        public static void AddRandomRoom(int floor) {
            while (true) {
                // Getting room and direction to branch off from
                Room adjacentRoom = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
                Direction dest = (Direction) RandomNumber(0, 3);

                // Preliminary checks
                if (adjacentRoom.IsDirectionBlocked(dest)) continue; // Checking if room already present in direction
                if (adjacentRoom.IsRoomTooCloseToSide(dest))      continue; // Checking if room is up against the sides of the plot in that direction

                float width, height, xPos, zPos;

                if (dest == Direction.LEFT || dest == Direction.RIGHT) {
                    height = adjacentRoom.Height;
                    zPos   = adjacentRoom.Z;

                    if (dest == Direction.LEFT) {
                        float maxRoomWidth = adjacentRoom.Left - MaxRoomSize < 0 ? MinRoomSize : MaxRoomSize;

                        width = RandomNumber(MinRoomSize, maxRoomWidth);
                        xPos = adjacentRoom.Left - width / 2;
                    } else {
                        float maxRoomWidth = adjacentRoom.Right + MaxRoomSize > GetPlotWidth() ? MinRoomSize : MaxRoomSize;

                        width = RandomNumber(MinRoomSize, maxRoomWidth);
                        xPos = adjacentRoom.Right + width / 2;
                    }
                } else {
                    width  = adjacentRoom.Width;
                    xPos   = adjacentRoom.X;

                    if (dest == Direction.UP) {
                        float maxRoomHeight = adjacentRoom.Top - MaxRoomSize < 0 ? MinRoomSize : MaxRoomSize;

                        height = RandomNumber(MinRoomSize, maxRoomHeight);
                        zPos = adjacentRoom.Top - height / 2;
                    } else {
                        float maxRoomHeight = adjacentRoom.Bottom + MaxRoomSize > GetPlotHeight() ? MinRoomSize : MaxRoomSize;

                        height = RandomNumber(MinRoomSize, maxRoomHeight);
                        zPos = adjacentRoom.Bottom + height / 2;
                    }
                }

                Room newRoom = new Room {
                    floor = floor,
                    bounds = new Bounds(new Vector3(xPos, floor, zPos), new Vector3(width, 0, height))
                };
                
                // Checking whether there are any intersecting rooms and cancelling room placement if so
                if (newRoom.IsIntersectingRoom()) continue;

                newRoom.GenerateConnections();
                rooms[floor].Add(newRoom);

                break;
            }
        }

        // - - - Validation - - - //

        public bool IsDirectionBlocked(Direction dest) {
            return directionBlocked[dest];
        }

        public void BlockDirection(Direction dest) {
            directionBlocked[dest] = true;
        }

        public bool IsRoomTooCloseToSide(Direction dest) {
            switch (dest) {
                case Direction.LEFT:
                    return Left - MinRoomSize - 1 < 0 ? true : false;
                case Direction.RIGHT:
                    return Right + MinRoomSize + 1 > GetPlotWidth() ? true : false;
                case Direction.UP:
                    return Top - MinRoomSize - 1 < 0 ? true : false;
                case Direction.DOWN:
                    return Bottom + MinRoomSize + 1 > GetPlotHeight() ? true : false;
                default:
                    throw new InvalidEnumArgumentException("Direction not valid");
            }
        }

        public bool IsIntersectingRoom() {
            foreach (Room r in rooms[floor]) {
                if (bounds.Intersects(r.bounds)) return true;
            }

            return false;
        }

        // - - - Connections - - - //

        public void GenerateConnections() {
            foreach (Room nextRoom in rooms[floor]) {

                // Check that the walls are lined up and that the rooms are adjacent to one another
                     if (FloatsEqual(nextRoom.Bottom, Top) && nextRoom.Left < Right && Left < nextRoom.Right) AddConnection(nextRoom, Direction.UP);
                else if (FloatsEqual(nextRoom.Top, Bottom) && nextRoom.Left < Right && Left < nextRoom.Right) AddConnection(nextRoom, Direction.DOWN);
                else if (FloatsEqual(nextRoom.Right, Left) && nextRoom.Top < Bottom && Top < nextRoom.Bottom) AddConnection(nextRoom, Direction.LEFT);
                else if (FloatsEqual(nextRoom.Left, Right) && nextRoom.Top < Bottom && Top < nextRoom.Bottom) AddConnection(nextRoom, Direction.RIGHT);
                
            }
        }

        private void AddConnection(Room adjacentRoom, Direction dir) {

            float lowerBound;
            float upperBound;

            if (dir == Direction.UP || dir == Direction.DOWN) {
                lowerBound = Left > adjacentRoom.Left ? Left : adjacentRoom.Left;
                upperBound = Right < adjacentRoom.Right ? Right : adjacentRoom.Right;
            } else {
                lowerBound = Top > adjacentRoom.Top ? Top : adjacentRoom.Top;
                upperBound = Bottom < adjacentRoom.Bottom ? Bottom : adjacentRoom.Bottom;
            }

            bool isDoorValid = upperBound - lowerBound > 2;
            float doorPosition = RandomNumber(lowerBound + 1, upperBound - 1);

            directionBlocked[dir] = true;
            adjacentRoom.directionBlocked[ReverseDirection(dir)] = true;

            connections[floor].Add(new RoomConnection {
                direction = dir,
                roomStart = this,
                roomEnd = adjacentRoom,
                isDoorValid = isDoorValid,
                doorPosition = doorPosition
            });
        }

        private bool IsConnectionAlreadyAdded(Room room) {
            foreach (RoomConnection c in connections[floor]) {
                if ((this == c.roomStart && room == c.roomEnd) || (room == c.roomStart && this == c.roomEnd)) {
                    return true;
                }
            }

            return false;
        }
    }

    void Start() {
        UpdateSettings();

        GenerateAndPlaceRandomBuilding();
    }

    void Update() {
        if (Input.GetKeyDown("space")) {
            UpdateSettings();

            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";

            string room = "";

            for (int i = 0; i < rooms[0].Count; i++) {
                Room r = rooms[0][i];
                room += "\n\nRoom " + i + " - Height:" + r.Height + ", Width:" + r.Width
                     + "\nLeft:"   + r.Left
                     + ", Right:"  + r.Right
                     + ", Top:"    + r.Top
                     + ", Bottom:" + r.Bottom
                     + "\nLeft:"   + r.IsDirectionBlocked(Direction.LEFT) 
                     + ", Right:"  + r.IsDirectionBlocked(Direction.RIGHT) 
                     + ", Up:"     + r.IsDirectionBlocked(Direction.UP) 
                     + ", Down:"   + r.IsDirectionBlocked(Direction.DOWN);
            }

            room += "\n\nConnection Size: " + connections[0].Count;

            Map.text = room;

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
        roomObjects = new List<GameObject>[FloorNumber];
        connections = new List<RoomConnection>[FloorNumber];

        for (int floor = 0; floor < FloorNumber; floor++) {
            rooms[floor] = new List<Room>();
            roomObjects[floor] = new List<GameObject>();
            connections[floor] = new List<RoomConnection>();
        }

        Room.AddFirstRoom();
        roomObjects[0].Add(Instantiate(Library.Room, Master));

        for (int i = 1; i < RoomNumber; i++) {
            roomObjects[0].Add(Instantiate(Library.Room, Master));
            Room.AddRandomRoom(0);
        }

        for (int i = 1; i < FloorNumber; i++) {
            foreach (Room r in rooms[0]) {
                /*Room newRoom = new Room {
                    floor = i,
                    bounds = new Bounds()
                };

                newRoom.GenerateConnections();

                rooms[i].Add(newRoom);*/
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
        GameObject grass = Instantiate(Library.Grass, new Vector3(PlotWidth / 2, -.1f, PlotHeight / 2), Quaternion.identity, Master);
        grass.transform.localScale = new Vector3(PlotWidth, 0.2f, PlotHeight);
    }

    private void PlaceCorners() {
        for (int i = 0; i < FloorNumber; i++) {
            for (int j = 0; j < rooms[i].Count; j++) {
                Room r = rooms[i][j];
                PlaceCorner(new Vector3(r.Left,  WallHeight + (RoofHeight * i), r.Top),    roomObjects[i][j]);
                PlaceCorner(new Vector3(r.Right, WallHeight + (RoofHeight * i), r.Top),    roomObjects[i][j]);
                PlaceCorner(new Vector3(r.Left,  WallHeight + (RoofHeight * i), r.Bottom), roomObjects[i][j]);
                PlaceCorner(new Vector3(r.Right, WallHeight + (RoofHeight * i), r.Bottom), roomObjects[i][j]);
            }
        }
    }

    private void PlaceCorner(Vector3 cornerPoint, GameObject room) {
        Instantiate(Library.Corner, cornerPoint, Quaternion.identity, room.transform);
    }

    private void PlaceFloors() {
        for (int i = 0; i < FloorNumber; i++) {
            for (int j = 0; j < rooms[i].Count; j++) {
                Room r = rooms[i][j];

                GameObject floor = Instantiate(Library.Floor, new Vector3(r.X, RoofHeight * i, r.Z), Quaternion.identity, roomObjects[i][j].transform);
                floor.transform.localScale = new Vector3(r.Width, 0.2f + (j * 0.001f), r.Height);
            }
        }
    }

    private void PlaceWalls() {
        GameObject wall;

        for (int i = 0; i < FloorNumber; i++) {
            for (int j = 0; j < rooms[i].Count; j++) {
                Room r = rooms[i][j];

                wall = Instantiate(Library.Wall, new Vector3((r.Left + r.Right) / 2, WallHeight + (RoofHeight * i), r.Top), Quaternion.Euler(0, 270, 0), roomObjects[i][j].transform);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.Width);

                wall = Instantiate(Library.Wall, new Vector3(r.Right, WallHeight + (RoofHeight * i), (r.Top + r.Bottom) / 2), Quaternion.Euler(0, 180, 0), roomObjects[i][j].transform);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.Height);

                wall = Instantiate(Library.Wall, new Vector3((r.Left + r.Right) / 2, WallHeight + (RoofHeight * i), r.Bottom), Quaternion.Euler(0, 90, 0), roomObjects[i][j].transform);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.Width);

                wall = Instantiate(Library.Wall, new Vector3(r.Left, WallHeight + (RoofHeight * i), (r.Top + r.Bottom) / 2), Quaternion.Euler(0, 0, 0), roomObjects[i][j].transform);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.Height);
            }
        }
    }

    private void PlaceDoors() {
        Room start = rooms[0][0];

        PlaceOutsideDoors(2);

        for (int floor = 0; floor < FloorNumber; floor++) {
            foreach (RoomConnection c in connections[floor]) {
                if (c.isDoorValid) {
                    GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.roomStart)];

                    switch (c.direction) {
                        case Direction.LEFT:
                            PlaceDoor(new Vector3(c.roomStart.Left, WallHeight + (RoofHeight * floor), c.doorPosition), Quaternion.Euler(0, 180, 0), false, room);
                            break;
                        case Direction.RIGHT:
                            PlaceDoor(new Vector3(c.roomStart.Right, WallHeight + (RoofHeight * floor), c.doorPosition), Quaternion.Euler(0, 0, 0), false, room);
                            break;
                        case Direction.UP:
                            PlaceDoor(new Vector3(c.doorPosition, WallHeight + (RoofHeight * floor), c.roomStart.Top), Quaternion.Euler(0, 270, 0), false, room);
                            break;
                        case Direction.DOWN:
                            PlaceDoor(new Vector3(c.doorPosition, WallHeight + (RoofHeight * floor), c.roomStart.Bottom), Quaternion.Euler(0, 90, 0), false, room);
                            break;
                    }
                }
            }
        }
    }

    private void PlaceOutsideDoors(int numberOfDoors) {
        for (int i = 0; i < numberOfDoors; i++) {
            while (true) {
                Room start = rooms[0][RandomNumber(0, rooms[0].Count - 1)];
                GameObject room = roomObjects[0][rooms[0].IndexOf(start)];

                if (start.IsDirectionBlocked(Direction.UP) != true) {
                    PlaceDoor(new Vector3(start.Left + (start.Width / 2), WallHeight, start.Top), Quaternion.Euler(0, 270, 0), true, room);
                    start.BlockDirection(Direction.UP);
                    break;
                } else if (start.IsDirectionBlocked(Direction.DOWN) != true) {
                    PlaceDoor(new Vector3(start.Left + (start.Width / 2), WallHeight, start.Bottom), Quaternion.Euler(0, 90, 0), true, room);
                    start.BlockDirection(Direction.DOWN);
                    break;
                } else if (start.IsDirectionBlocked(Direction.LEFT) != true) {
                    PlaceDoor(new Vector3(start.Left, WallHeight, start.Top + (start.Height / 2)), Quaternion.Euler(0, 0, 0), true, room);
                    start.BlockDirection(Direction.LEFT);
                    break;
                } else if (start.IsDirectionBlocked(Direction.RIGHT) != true) {
                    PlaceDoor(new Vector3(start.Right, WallHeight, start.Top + (start.Height / 2)), Quaternion.Euler(0, 180, 0), true, room);
                    start.BlockDirection(Direction.RIGHT);
                    break;
                }
            }
        }
    }

    private void PlaceDoor(Vector3 position, Quaternion rotation, bool outside, GameObject room) {
        Instantiate(outside ? Library.OutsideDoor : Library.InsideDoor, position, rotation, room.transform);
    }

    private void PlaceWindows() {
        for (int i = 0; i < FloorNumber; i++) {
            for (int j = 0; j < rooms[i].Count; j++) {
                Room r = rooms[i][j];

                if (r.IsDirectionBlocked(Direction.LEFT) == false) 
                    PlaceWindow(new Vector3(r.Left, WallHeight + (RoofHeight * i), r.Top + RandomNumber(1, r.Height - 1)),   Quaternion.Euler(0, 0, 0), roomObjects[i][j]);
                if (r.IsDirectionBlocked(Direction.RIGHT) == false)  
                    PlaceWindow(new Vector3(r.Right, WallHeight + (RoofHeight * i), r.Top + RandomNumber(1, r.Height - 1)),  Quaternion.Euler(0, 180, 0), roomObjects[i][j]);
                if (r.IsDirectionBlocked(Direction.UP) == false)    
                    PlaceWindow(new Vector3(r.Left + RandomNumber(1, r.Width - 1), WallHeight + (RoofHeight * i), r.Top),    Quaternion.Euler(0, 90, 0), roomObjects[i][j]);
                if (r.IsDirectionBlocked(Direction.DOWN) == false) 
                    PlaceWindow(new Vector3(r.Left + RandomNumber(1, r.Width - 1), WallHeight + (RoofHeight * i), r.Bottom), Quaternion.Euler(0, 270, 0), roomObjects[i][j]);
            }
        }
    }

    private void PlaceWindow(Vector3 position, Quaternion rotation, GameObject room) {
        Instantiate(Library.Window, position, rotation, room.transform);
    }

    private void PlaceRoofing() {
        foreach (Room r in rooms[rooms.Length - 1]) {
            GameObject roof = Instantiate(Library.Floor, new Vector3(r.X, RoofHeight, r.Z), Quaternion.identity, Master);
            roof.transform.localScale = new Vector3(r.Width, 0.2f, r.Height);
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

    private static bool FloatsEqual(float float1, float float2) {
        return System.Math.Abs(float1 - float2) < 0.01f;
    }

    private static bool FloatGreaterOrEqual(float float1, float float2) {
        return float1 > float2 || FloatsEqual(float1, float2);
    }

    private static bool FloatLessOrEqual(float float1, float float2) {
        return float1 < float2 || FloatsEqual(float1, float2);
    }

    private static Direction ReverseDirection(Direction dir) {
        switch (dir) {
            case Direction.LEFT:
                return Direction.RIGHT;
            case Direction.RIGHT:
                return Direction.LEFT;
            case Direction.UP:
                return Direction.DOWN;
            case Direction.DOWN:
                return Direction.UP;
            default:
                throw new InvalidEnumArgumentException();
        }
    }
}
