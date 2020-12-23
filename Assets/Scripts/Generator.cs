using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEditorInternal;
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

    // North, South, East, West, Up, Down
    private enum Direction { N, S, E, W, U, D }

    private struct RoomConnection {
        public Direction direction;
        public Room roomStart;
        public Room roomEnd;
        public bool isEntranceValid;
        public float entrancePosition;
    }

    private class Room {
        // Ensures there's a slight gap between bounds to make sure they don't intersect
        private const float BOUNDS_GAP = 0.001f;

        // The room's dimensions
        private Bounds bounds;

        // Any connections to other rooms this room has
        private readonly Dictionary<Direction, bool> directionBlocked = new Dictionary<Direction, bool> {
            { Direction.N, false },
            { Direction.S, false },
            { Direction.W, false },
            { Direction.E, false },
            { Direction.U, false },
            { Direction.D, false }
        };

        public int floor;

        public float W      => bounds.min.x - BOUNDS_GAP;
        public float E      => bounds.max.x + BOUNDS_GAP;
        public float N      => bounds.min.z - BOUNDS_GAP;
        public float S      => bounds.max.z + BOUNDS_GAP;
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
            bool successfulRoomPlaced;

            while (true) {
                successfulRoomPlaced = floor == 0 ? TryToAddRandomGroundFloorRoom() : TryToAddRandomUpperFloorRoom(floor);
                if (successfulRoomPlaced) break;
            }
        }

        public static void AddStairWellAndRoom(int floor) {
            Room start = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
            Room newRoom = new Room { 
                bounds = new Bounds(start.bounds.center, start.bounds.size),
                floor = floor + 1
            };

            rooms[floor + 1].Add(newRoom);

            start.directionBlocked[Direction.U] = true;
            newRoom.directionBlocked[ReverseDirection(Direction.D)] = true;

            connections[floor].Add(new RoomConnection {
                direction = Direction.U,
                roomStart = start,
                roomEnd = newRoom,
                isEntranceValid = true,
                //entrancePosition = 
            });
        }

        private static bool TryToAddRandomGroundFloorRoom() {
            int floor = 0;

            // Getting room and direction to branch off from
            Room adjacentRoom = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
            Direction dest = (Direction)RandomNumber(0, 3);

            // Preliminary checks
            if (adjacentRoom.IsDirectionBlocked(dest))   return false; // Checking if room already present in direction
            if (adjacentRoom.IsRoomTooCloseToSide(dest)) return false; // Checking if room is up against the sides of the plot in that direction

            float width, height, xPos, zPos;

            if (dest == Direction.W || dest == Direction.E) {
                height = adjacentRoom.Height;
                zPos = adjacentRoom.Z;

                if (dest == Direction.W) {
                    float maxRoomWidth = adjacentRoom.W - MaxRoomSize < 0 ? MinRoomSize : MaxRoomSize;

                    width = RandomNumber(MinRoomSize, maxRoomWidth);
                    xPos = adjacentRoom.W - width / 2;
                } else {
                    float maxRoomWidth = adjacentRoom.E + MaxRoomSize > GetPlotWidth() ? MinRoomSize : MaxRoomSize;

                    width = RandomNumber(MinRoomSize, maxRoomWidth);
                    xPos = adjacentRoom.E + width / 2;
                }
            } else {
                width = adjacentRoom.Width;
                xPos = adjacentRoom.X;

                if (dest == Direction.N) {
                    float maxRoomHeight = adjacentRoom.N - MaxRoomSize < 0 ? MinRoomSize : MaxRoomSize;

                    height = RandomNumber(MinRoomSize, maxRoomHeight);
                    zPos = adjacentRoom.N - height / 2;
                } else {
                    float maxRoomHeight = adjacentRoom.S + MaxRoomSize > GetPlotHeight() ? MinRoomSize : MaxRoomSize;

                    height = RandomNumber(MinRoomSize, maxRoomHeight);
                    zPos = adjacentRoom.S + height / 2;
                }
            }

            Room newRoom = new Room {
                floor = floor,
                bounds = new Bounds(new Vector3(xPos, floor, zPos), new Vector3(width, 0, height))
            };

            // Checking whether there are any intersecting rooms and cancelling room placement if so
            if (newRoom.IsIntersectingRoom()) return false;

            newRoom.GenerateConnections();
            rooms[floor].Add(newRoom);

            // Return success
            return true;
        }

        private static bool TryToAddRandomUpperFloorRoom(int floor) {
            
            
            return true;
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
                case Direction.W:
                    return W - MinRoomSize - 1 < 0 ? true : false;
                case Direction.E:
                    return E + MinRoomSize + 1 > GetPlotWidth() ? true : false;
                case Direction.N:
                    return N - MinRoomSize - 1 < 0 ? true : false;
                case Direction.S:
                    return S + MinRoomSize + 1 > GetPlotHeight() ? true : false;
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
                     if (FloatsEqual(nextRoom.S, N) && FloatLessNotEqual(nextRoom.W, E) && FloatLessNotEqual(W, nextRoom.E)) AddConnection(nextRoom, Direction.N);
                else if (FloatsEqual(nextRoom.N, S) && FloatLessNotEqual(nextRoom.W, E) && FloatLessNotEqual(W, nextRoom.E)) AddConnection(nextRoom, Direction.S);
                else if (FloatsEqual(nextRoom.E, W) && FloatLessNotEqual(nextRoom.N, S) && FloatLessNotEqual(N, nextRoom.S)) AddConnection(nextRoom, Direction.W);
                else if (FloatsEqual(nextRoom.W, E) && FloatLessNotEqual(nextRoom.N, S) && FloatLessNotEqual(N, nextRoom.S)) AddConnection(nextRoom, Direction.E);
            }
        }

        private void AddConnection(Room adjacentRoom, Direction dir) {

            float lowerBound;
            float upperBound;

            if (dir == Direction.N || dir == Direction.S) {
                lowerBound = W > adjacentRoom.W ? W : adjacentRoom.W;
                upperBound = E < adjacentRoom.E ? E : adjacentRoom.E;
            } else {
                lowerBound = N > adjacentRoom.N ? N : adjacentRoom.N;
                upperBound = S < adjacentRoom.S ? S : adjacentRoom.S;
            }

            bool isDoorValid = upperBound - lowerBound > 2;
            float doorPosition = RandomNumber(lowerBound + 1, upperBound - 1);

            directionBlocked[dir] = true;
            adjacentRoom.directionBlocked[ReverseDirection(dir)] = true;

            connections[floor].Add(new RoomConnection {
                direction = dir,
                roomStart = this,
                roomEnd = adjacentRoom,
                isEntranceValid = isDoorValid,
                entrancePosition = doorPosition
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

        PrintRooms(0);
    }

    void Update() {
        if (Input.GetKeyDown("space")) {
            UpdateSettings();

            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();
            PrintRooms(0);

        } else if (Input.GetKeyDown("m")) {
            ToggleUI();
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

    private void PrintRooms(int floor) {
        string room = "";

        for (int i = 0; i < rooms[0].Count; i++) {
            Room r = rooms[floor][i];
            room += "\n\nRoom " + (i + 1) + " - Height:" + r.Height + ", Width:" + r.Width
                 + "\nLeft:" + r.W
                 + ", Right:" + r.E
                 + ", Top:" + r.N
                 + ", Bottom:" + r.S
                 + "\nLeft:" + r.IsDirectionBlocked(Direction.W)
                 + ", Right:" + r.IsDirectionBlocked(Direction.E)
                 + ", Up:" + r.IsDirectionBlocked(Direction.N)
                 + ", Down:" + r.IsDirectionBlocked(Direction.S);
        }

        room += "\n\nConnection Size: " + connections[0].Count;

        Map.text = room;
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

        for (int floor = 1; floor < FloorNumber; floor++) {
            Room.AddStairWellAndRoom(floor - 1);

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
        if (RoofEnabled) PlaceRoofing();
        PlaceOutsideDoors(Settings.OutsideDoorNumber);

        for (int floor = 0; floor < FloorNumber; floor++) {
            for (int room = 0; room < rooms[floor].Count; room++) {
                Room r = rooms[floor][room];
                GameObject g = roomObjects[floor][room];

                PlaceRoomFloor(floor, room, r, g);
                PlaceRoomCorners(r, g);
                PlaceRoomWalls(r, g);
                PlaceRoomWindows(r, g);
            }

            PlaceFloorDoors(floor);
        }

    }

    private void PlaceRoomFloor(int floor, int room, Room r, GameObject g) {
        GameObject floorObj = Instantiate(Library.Floor, new Vector3(r.X, RoofHeight * floor, r.Z), Quaternion.identity, g.transform);
        floorObj.transform.localScale = new Vector3(r.Width, 0.2f + (room * 0.001f), r.Height);
    }

    private void PlaceRoomCorners(Room r, GameObject g) {
        float yPos = WallHeight + (RoofHeight * r.floor);

        PlaceCorner(new Vector3(r.W, yPos, r.N), g);
        PlaceCorner(new Vector3(r.E, yPos, r.N), g);
        PlaceCorner(new Vector3(r.W, yPos, r.S), g);
        PlaceCorner(new Vector3(r.E, yPos, r.S), g);
    }

    private void PlaceRoomWalls(Room r, GameObject g) {
        float yPos = WallHeight + (RoofHeight * r.floor);

        PlaceWall(new Vector3(r.X, yPos, r.N), Direction.N, r.Width, g);
        PlaceWall(new Vector3(r.X, yPos, r.S), Direction.S, r.Width, g);
        PlaceWall(new Vector3(r.E, yPos, r.Z), Direction.E, r.Height, g);
        PlaceWall(new Vector3(r.W, yPos, r.Z), Direction.W, r.Height, g);
    }

    private void PlaceRoomWindows(Room r, GameObject g) {
        float yPos = WallHeight + (RoofHeight * r.floor);

        if (r.IsDirectionBlocked(Direction.N) == false) PlaceWindow(new Vector3(r.W + RandomNumber(1, r.Width - 1), yPos, r.N), Direction.N, g);
        if (r.IsDirectionBlocked(Direction.S) == false) PlaceWindow(new Vector3(r.W + RandomNumber(1, r.Width - 1), yPos, r.S), Direction.S, g);
        if (r.IsDirectionBlocked(Direction.E) == false) PlaceWindow(new Vector3(r.E, yPos, r.N + RandomNumber(1, r.Height - 1)), Direction.E, g);
        if (r.IsDirectionBlocked(Direction.W) == false) PlaceWindow(new Vector3(r.W, yPos, r.N + RandomNumber(1, r.Height - 1)), Direction.W, g);
    }

    private void PlaceFloorDoors(int floor) {
        foreach (RoomConnection c in connections[floor]) {
            if (c.isEntranceValid) {
                GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.roomStart)];

                float yPos = WallHeight + (RoofHeight * floor);

                switch (c.direction) {
                    case Direction.N:
                        PlaceDoor(new Vector3(c.entrancePosition, yPos, c.roomStart.N), Direction.N, false, room);
                        break;
                    case Direction.S:
                        PlaceDoor(new Vector3(c.entrancePosition, yPos, c.roomStart.S), Direction.S, false, room);
                        break;
                    case Direction.E:
                        PlaceDoor(new Vector3(c.roomStart.E, yPos, c.entrancePosition), Direction.E, false, room);
                        break;
                    case Direction.W:
                        PlaceDoor(new Vector3(c.roomStart.W, yPos, c.entrancePosition), Direction.W, false, room);
                        break;
                }
            }
        }
    }

    private void PlaceOutsideDoors(int numberOfDoors) {
        int doorCount = 0;

        while (doorCount < numberOfDoors) {
            Room r = rooms[0][RandomNumber(0, rooms[0].Count - 1)];
            GameObject room = roomObjects[0][rooms[0].IndexOf(r)];

            if (r.IsDirectionBlocked(Direction.N) == false) {
                PlaceDoor(new Vector3(r.X, WallHeight, r.N), Direction.N, true, room);
                r.BlockDirection(Direction.N);
                doorCount++;
            } else if (r.IsDirectionBlocked(Direction.S) == false) {
                PlaceDoor(new Vector3(r.X, WallHeight, r.S), Direction.S, true, room);
                r.BlockDirection(Direction.S);
                doorCount++;
            } else if (r.IsDirectionBlocked(Direction.E) == false) {
                PlaceDoor(new Vector3(r.E, WallHeight, r.Z), Direction.E, true, room);
                r.BlockDirection(Direction.E);
                doorCount++;
            } else if (r.IsDirectionBlocked(Direction.W) == false) {
                PlaceDoor(new Vector3(r.W, WallHeight, r.Z), Direction.W, true, room);
                r.BlockDirection(Direction.W);
                doorCount++;
            }
        }
    }

    private void PlaceGrass() {
        GameObject grass = Instantiate(Library.Grass, new Vector3(PlotWidth / 2, -.1f, PlotHeight / 2), Quaternion.identity, Master);
        grass.transform.localScale = new Vector3(PlotWidth, 0.2f, PlotHeight);
    }

    private void PlaceCorner(Vector3 cornerPoint, GameObject room) {
        Instantiate(Library.Corner, cornerPoint, Quaternion.identity, room.transform);
    }

    private void PlaceWall(Vector3 position, Direction dirFacing, float length, GameObject room) {
        GameObject wall = Instantiate(Library.Wall, position, Rotation(dirFacing), room.transform);
        wall.transform.localScale = new Vector3(0.2f, 3f, length);
    }

    private void PlaceWindow(Vector3 position, Direction dirFacing, GameObject room) {
        Instantiate(Library.Window, position, Rotation(dirFacing), room.transform);
    }

    private void PlaceDoor(Vector3 position, Direction dirFacing, bool outside, GameObject room) {
        Instantiate(outside ? Library.OutsideDoor : Library.InsideDoor, position, Rotation(dirFacing), room.transform);
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

    private static bool FloatGreaterNotEqual(float float1, float float2) {
        return float1 > float2 && !FloatsEqual(float1, float2);
    }

    private static bool FloatLessNotEqual(float float1, float float2) {
        return float1 < float2 && !FloatsEqual(float1, float2);
    }

    private static Direction ReverseDirection(Direction dir) {
        switch (dir) {
            case Direction.N:
                return Direction.S;
            case Direction.S:
                return Direction.N;
            case Direction.E:
                return Direction.W;
            case Direction.W:
                return Direction.E;
            case Direction.U:
                return Direction.D;
            case Direction.D:
                return Direction.U;
            default:
                throw new InvalidEnumArgumentException();
        }
    }

    private static Quaternion Rotation(Direction dir) {
        switch (dir) {
            case Direction.N:
                return Quaternion.Euler(0, 270, 0);
            case Direction.S:
                return Quaternion.Euler(0, 90, 0);
            case Direction.E:
                return Quaternion.Euler(0, 180, 0);
            case Direction.W:
                return Quaternion.Euler(0, 0, 0);
            default:
                throw new InvalidEnumArgumentException();
        }
    }
}
