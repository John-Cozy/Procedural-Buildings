using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class Generator : MonoBehaviour {

    public Text Map;
    public Text Stats;
    public CanvasGroup UI;
    public Transform Plot;

    private static PrefabLibrary Library;
    private static Settings Settings;

    private static List<Room>[] rooms;
    private static List<RoomConnection>[] connections;

    private static List<GameObject>[] roomObjects;
    private static GameObject[] floors;

    private bool showingMap = false;

    // North, South, East, West, Up, Down
    private enum Direction { N, S, E, W, U, D }

    private struct RoomConnection {
        public Direction direction;
        public Room roomStart;
        public Room roomEnd;
        public bool isEntranceValid;
        public Vector3 entrancePosition;
        public Quaternion entranceRotation;
    }

    private class Room {
        // Ensures there's a slight gap between bounds to make sure they don't intersect
        private const float BOUNDS_GAP = 0.001f;

        // The room's dimensions
        private Bounds bounds;

        // Any connections to other rooms this room has
        private readonly Dictionary<Direction, bool> isDirectionBlocked = new Dictionary<Direction, bool> {
            { Direction.N, false },
            { Direction.S, false },
            { Direction.W, false },
            { Direction.E, false },
            { Direction.U, false },
            { Direction.D, false }
        };

        public int Floor;

        public bool IsBalconyRoom = false;

        public float W      => bounds.min.x - BOUNDS_GAP;
        public float E      => bounds.max.x + BOUNDS_GAP;
        public float N      => bounds.min.z - BOUNDS_GAP;
        public float S      => bounds.max.z + BOUNDS_GAP;
        public float X      => bounds.center.x;
        public float Z      => bounds.center.z;
        public float Width  => bounds.size.x;
        public float Height => bounds.size.z;

        // - - - Adding Rooms - - - //

        private static void AddRoom(Room room) {
            rooms[room.Floor].Add(room);

            roomObjects[room.Floor].Add(Instantiate(Library.RoomHolder, floors[room.Floor].transform));
        }

        public static void AddFirstRoom() {
            float xPos = GetPlotWidth()  / 2;
            float zPos = GetPlotHeight() / 2;
            float width  = RandomNumber(Settings.MinRoomSize, Settings.MaxRoomSize) - BOUNDS_GAP * 2;
            float height = RandomNumber(Settings.MinRoomSize, Settings.MaxRoomSize) - BOUNDS_GAP * 2;

            AddRoom(new Room {
                Floor = 0,
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

        public static void AddStairwellAndRoom(int floor) {
            bool successfulRoomPlaced;

            while (true) {
                successfulRoomPlaced = TryToAddStairwellAndRoom(floor);
                if (successfulRoomPlaced) break;
            }
        }

        public static void AddRemainingRoomsOnFloor(int floor) {
            foreach (Room r in rooms[floor - 1]) {
                if (r.isDirectionBlocked[Direction.U] == false) {
                    Room newRoom = new Room {
                        Floor = floor,
                        bounds = new Bounds(new Vector3(r.X, floor, r.Z), new Vector3(r.Width, 0, r.Height))
                    };

                    newRoom.GenerateConnections();

                    newRoom.isDirectionBlocked[Direction.D] = true;
                    r.isDirectionBlocked[Direction.U] = true;

                    AddRoom(newRoom);
                }
            }
        }

        public static void MakeBalconyRoomsOnFloor(int floor, int numberOfRooms) {
            for (int i = 0; i < numberOfRooms; i++) {
                if (TryToChangeRandomRoomToBalcony(floor) != true) i--;
            }
        }

        private static bool TryToAddRandomGroundFloorRoom() {
            int floor = 0;

            // Getting room and direction to branch off from
            Room adjacentRoom = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
            Direction dest = (Direction) RandomNumber(0, 3);

            // Preliminary checks
            if (adjacentRoom.IsDirectionBlocked(dest))   return false; // Checking if room already present in direction
            if (adjacentRoom.IsRoomTooCloseToSide(dest)) return false; // Checking if room is up against the sides of the plot in that direction

            float width, height, xPos, zPos;

            if (dest == Direction.W || dest == Direction.E) {
                height = adjacentRoom.Height;
                zPos = adjacentRoom.Z;

                if (dest == Direction.W) {
                    float maxRoomWidth = adjacentRoom.W - Settings.MaxRoomSize < 0 ? Settings.MinRoomSize : Settings.MaxRoomSize;

                    width = RandomNumber(Settings.MinRoomSize, maxRoomWidth);
                    xPos = adjacentRoom.W - width / 2;
                } else {
                    float maxRoomWidth = adjacentRoom.E + Settings.MaxRoomSize > GetPlotWidth() ? Settings.MinRoomSize : Settings.MaxRoomSize;

                    width = RandomNumber(Settings.MinRoomSize, maxRoomWidth);
                    xPos = adjacentRoom.E + width / 2;
                }
            } else {
                width = adjacentRoom.Width;
                xPos = adjacentRoom.X;

                if (dest == Direction.N) {
                    float maxRoomHeight = adjacentRoom.N - Settings.MaxRoomSize < 0 ? Settings.MinRoomSize : Settings.MaxRoomSize;

                    height = RandomNumber(Settings.MinRoomSize, maxRoomHeight);
                    zPos = adjacentRoom.N - height / 2;
                } else {
                    float maxRoomHeight = adjacentRoom.S + Settings.MaxRoomSize > GetPlotHeight() ? Settings.MinRoomSize : Settings.MaxRoomSize;

                    height = RandomNumber(Settings.MinRoomSize, maxRoomHeight);
                    zPos = adjacentRoom.S + height / 2;
                }
            }

            Room newRoom = new Room {
                Floor = floor,
                bounds = new Bounds(new Vector3(xPos, floor, zPos), new Vector3(width, 0, height))
            };

            // Checking whether there are any intersecting rooms and cancelling room placement if so
            if (newRoom.IsIntersectingRoom()) return false;

            newRoom.GenerateConnections();
            AddRoom(newRoom);

            // Return success
            return true;
        }

        private static bool TryToAddStairwellAndRoom(int floor) {
            Room start = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];
            Direction dir;

            if (start.IsAllDirectionsBlocked()) return false;

            dir = start.GetFreeWallDirection();
            
            Vector3 stairPosition = Position(dir, 0, start, 1);
            Quaternion stairRotation = Rotation(dir) * Quaternion.Euler(0, 90, 0);
            
            start.isDirectionBlocked[dir] = true;

            Room newRoom = new Room {
                bounds = new Bounds(new Vector3(start.X, Settings.RoofHeight * (floor + 1), start.Z), start.bounds.size),
                Floor = floor + 1
            };

            AddRoom(newRoom);

            start.isDirectionBlocked[Direction.U] = true;
            newRoom.isDirectionBlocked[Direction.D] = true;

            connections[floor].Add(new RoomConnection {
                direction = Direction.U,
                roomStart = start,
                roomEnd = newRoom,
                isEntranceValid = true,
                entrancePosition = stairPosition,
                entranceRotation = stairRotation
            });

            return true;
        }

        private static bool TryToAddRandomUpperFloorRoom(int floor) {
            
            return false; 
        }

        private static bool TryToChangeRandomRoomToBalcony(int floor) {
            Room r = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];

            foreach (RoomConnection c in connections[floor - 1]) {
                if (c.roomEnd == r || r.IsBalconyRoom && r.IsAllDirectionsBlocked() != true) return false;
            }

            r.IsBalconyRoom = true;

            return true;
        }

        // - - - Validation - - - //

        public bool IsDirectionBlocked(Direction dest) {
            return isDirectionBlocked[dest];
        }

        public void BlockDirection(Direction dest) {
            isDirectionBlocked[dest] = true;
        }

        public bool IsRoomTooCloseToSide(Direction dest) {
            switch (dest) {
                case Direction.W:
                    return W - Settings.MinRoomSize - 1 < 0 ? true : false;
                case Direction.E:
                    return E + Settings.MinRoomSize + 1 > GetPlotWidth() ? true : false;
                case Direction.N:
                    return N - Settings.MinRoomSize - 1 < 0 ? true : false;
                case Direction.S:
                    return S + Settings.MinRoomSize + 1 > GetPlotHeight() ? true : false;
                default:
                    throw new InvalidEnumArgumentException("Direction not valid");
            }
        }

        public bool IsIntersectingRoom() {
            foreach (Room r in rooms[Floor]) {
                if (bounds.Intersects(r.bounds)) return true;
            }

            return false;
        }

        // - - - Connections - - - //

        private void GenerateConnections() {
            foreach (Room nextRoom in rooms[Floor]) {

                // Check that the walls are lined up and that the rooms are adjacent to one another
                     if (FloatsEqual(nextRoom.S, N) && FloatLessNotEqual(nextRoom.W, E) && FloatLessNotEqual(W, nextRoom.E)) AddDoorConnection(nextRoom, Direction.N);
                else if (FloatsEqual(nextRoom.N, S) && FloatLessNotEqual(nextRoom.W, E) && FloatLessNotEqual(W, nextRoom.E)) AddDoorConnection(nextRoom, Direction.S);
                else if (FloatsEqual(nextRoom.E, W) && FloatLessNotEqual(nextRoom.N, S) && FloatLessNotEqual(N, nextRoom.S)) AddDoorConnection(nextRoom, Direction.W);
                else if (FloatsEqual(nextRoom.W, E) && FloatLessNotEqual(nextRoom.N, S) && FloatLessNotEqual(N, nextRoom.S)) AddDoorConnection(nextRoom, Direction.E);
            }
        }

        private void AddDoorConnection(Room adjacentRoom, Direction dir) {

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
            Vector3 doorPosition = new Vector3();
            Quaternion doorRotation = Rotation(dir);

            if (isDoorValid) {
                float betweenBounds = RandomNumber(lowerBound + 1, upperBound - 1);
                float yPos = Settings.WallHeight + (Settings.RoofHeight * Floor);

                doorPosition = Position(dir, yPos, betweenBounds, this);
            }

            isDirectionBlocked[dir] = true;
            adjacentRoom.isDirectionBlocked[ReverseDirection(dir)] = true;

            RoomConnection connection = new RoomConnection {
                direction = dir,
                roomStart = this,
                roomEnd = adjacentRoom,
                isEntranceValid = isDoorValid,
                entrancePosition = doorPosition,
                entranceRotation = doorRotation
            };

            connections[Floor].Add(connection);

        }

        private bool IsConnectionAlreadyAdded(Room room) {
            foreach (RoomConnection c in connections[Floor]) {
                if ((this == c.roomStart && room == c.roomEnd) || (room == c.roomStart && this == c.roomEnd)) {
                    return true;
                }
            }

            return false;
        }

        private void RemoveRoomConnections() {
            for (int room = 0; room < connections[Floor].Count; room++) {
                RoomConnection c = connections[Floor][room];
                
                if (this == c.roomEnd || this == c.roomStart) { 
                    connections[Floor].Remove(c);
                    room--;
                }
            }
        }
        
        // - - - Walls - - - //

        public float GetWallPosition(Direction dir) {
            switch (dir) {
                case Direction.N:
                    return N;
                case Direction.S:
                    return S;
                case Direction.E:
                    return E;
                case Direction.W:
                    return W;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        public Direction GetFreeWallDirection() {
            if (IsDirectionBlocked(Direction.N) == false) {
                return Direction.N;
            } else if (IsDirectionBlocked(Direction.S) == false) {
                return Direction.S;
            } else if (IsDirectionBlocked(Direction.E) == false) {
                return Direction.E;
            } else if (IsDirectionBlocked(Direction.W) == false) {
                return Direction.W;
            } else {
                throw new System.Exception("No Free Walls");
            }
        }

        public bool IsAllDirectionsBlocked() {
            return isDirectionBlocked[Direction.N]
                && isDirectionBlocked[Direction.S]
                && isDirectionBlocked[Direction.E]
                && isDirectionBlocked[Direction.W];
        }
    }

    void Awake() {
        Library = GetComponent<PrefabLibrary>();
        Settings = GetComponent<Settings>();
    }

    void Start() {
        GenerateAndPlaceRandomBuilding();

        PrintRooms(0);
    }

    void Update() {
        if (Input.GetKeyDown("space")) {
            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();
            PrintRooms(1);

        } else if (Input.GetKeyDown("m")) {
            ToggleUI();
        } else if (Input.GetKeyDown("h")) {
            var stopwatch = new System.Diagnostics.Stopwatch();

            stopwatch.Start();
            for (int i = 0; i < 30; i++) GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";
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

    private void PrintRooms(int floor) {
        string rooms = "";

        for (int room = 0; room < Generator.rooms[floor].Count; room++) {
            Room r = Generator.rooms[floor][room];
            rooms += "\n\nRoom " + (room + 1) + " - Height:" + r.Height + ", Width:" + r.Width
                 + "\nW:" + r.W
                 + ", E:" + r.E
                 + ", N:" + r.N
                 + ", S:" + r.S
                 + "\nW:" + r.IsDirectionBlocked(Direction.W)
                 + ", E:" + r.IsDirectionBlocked(Direction.E)
                 + ", N:" + r.IsDirectionBlocked(Direction.N)
                 + ", S:" + r.IsDirectionBlocked(Direction.S);
        }

        rooms += "\n\nConnection Size: " + connections[0].Count;

        Map.text = rooms;
    }

    private void GenerateAndPlaceRandomBuilding() {
        DeleteChildren();
        GenerateRooms();
        PlaceFloorPlan();
    }

    private void DeleteChildren() {
        foreach (Transform child in Plot) {
            Destroy(child.gameObject);
        }
    }

    private void GenerateRooms() {
        rooms = new List<Room>[Settings.FloorNumber];
        roomObjects = new List<GameObject>[Settings.FloorNumber];
        connections = new List<RoomConnection>[Settings.FloorNumber];
        floors = new GameObject[Settings.FloorNumber];

        for (int floor = 0; floor < Settings.FloorNumber; floor++) {
            rooms[floor] = new List<Room>();
            roomObjects[floor] = new List<GameObject>();
            connections[floor] = new List<RoomConnection>();
            floors[floor] = Instantiate(Library.FloorHolder, Plot);
        }

        Room.AddFirstRoom();

        for (int i = 1; i < Settings.RoomNumber; i++) {
            Room.AddRandomRoom(0);
        }

        for (int floor = 1; floor < Settings.FloorNumber; floor++) {
            Room.AddStairwellAndRoom(floor - 1);
            Room.AddRemainingRoomsOnFloor(floor);
            Room.MakeBalconyRoomsOnFloor(floor, Settings.BalconyRoomNumber);
        }
    }

    private void PlaceFloorPlan() {
        PlaceGrass();
        PlaceOutsideDoors(Settings.OutsideDoorNumber);

        for (int floor = 0; floor < Settings.FloorNumber; floor++) {
            for (int room = 0; room < rooms[floor].Count; room++) {
                Room r = rooms[floor][room];
                GameObject g = roomObjects[floor][room];

                if (r.IsBalconyRoom == false) {
                    PlaceRoomFloor(r, g, floor, room);
                    if (Settings.RoofEnabled && r.IsDirectionBlocked(Direction.U) == false) PlaceRoomRoof(r, g, floor, room);
                    PlaceRoomCorners(r, g);
                    PlaceRoomWalls(r, g);
                    PlaceRoomWindows(r, g);
                } else {
                    PlaceRoomFloor(r, g, floor, room);
                    PlaceRoomRailingCorners(r, g);
                    PlaceRoomRailing(r, g);
                }
            }

            PlaceFloorDoorsAndStairs(floor);
        }
    }

    private void PlaceRoomFloor(Room r, GameObject g, int floor, int room) {
        GameObject floorObj = Instantiate(Library.Floor, new Vector3(r.X, Settings.RoofHeight * floor, r.Z), Quaternion.identity, g.transform);
        floorObj.transform.localScale = new Vector3(r.Width, 0.2f + (room * 0.001f), r.Height);
    }

    private void PlaceRoomRoof(Room r, GameObject g, int floor, int room) {
        GameObject roofObj = Instantiate(Library.Roof, new Vector3(r.X, Settings.RoofHeight * (floor + 1), r.Z), Quaternion.identity, g.transform);
        roofObj.transform.localScale = new Vector3(r.Width, 0.2f + (room * 0.001f), r.Height);
    }

    private void PlaceRoomCorners(Room r, GameObject g) {
        float yPos = Settings.WallHeight + (Settings.RoofHeight * r.Floor);

        PlaceCorner(new Vector3(r.W, yPos, r.N), g);
        PlaceCorner(new Vector3(r.E, yPos, r.N), g);
        PlaceCorner(new Vector3(r.W, yPos, r.S), g);
        PlaceCorner(new Vector3(r.E, yPos, r.S), g);
    }

    private void PlaceRoomWalls(Room r, GameObject g) {
        float yPos = Settings.WallHeight + (Settings.RoofHeight * r.Floor);

        PlaceWall(new Vector3(r.X, yPos, r.N), Direction.N, r.Width,  g);
        PlaceWall(new Vector3(r.X, yPos, r.S), Direction.S, r.Width,  g);
        PlaceWall(new Vector3(r.E, yPos, r.Z), Direction.E, r.Height, g);
        PlaceWall(new Vector3(r.W, yPos, r.Z), Direction.W, r.Height, g);
    }

    private void PlaceRoomWindows(Room r, GameObject g) {
        float yPos = Settings.WallHeight + (Settings.RoofHeight * r.Floor);

        int windowNumberX = Settings.WindowNumber < r.Width  ? Settings.WindowNumber : (int) r.Width  - 1;
        int windowNumberZ = Settings.WindowNumber < r.Height ? Settings.WindowNumber : (int) r.Height - 1;

        if (Settings.RandomWindowNumber) {
            windowNumberX = RandomNumber(1, windowNumberX);
            windowNumberZ = RandomNumber(1, windowNumberZ);
        }

        float widthIncrement =  r.Width  / windowNumberX;
        float heightIncrement = r.Height / windowNumberZ;

        for (int i = 0; i < windowNumberX; i++) {
            float windowPosition = widthIncrement * i + widthIncrement / 2;

            if (r.IsDirectionBlocked(Direction.N) == false) PlaceWindow(new Vector3(r.W + windowPosition, yPos, r.N), Direction.N, g);
            if (r.IsDirectionBlocked(Direction.S) == false) PlaceWindow(new Vector3(r.W + windowPosition, yPos, r.S), Direction.S, g);
        }

        for (int i = 0; i < windowNumberZ; i++) {
            float windowPositionH = heightIncrement * i + heightIncrement / 2;

            if (r.IsDirectionBlocked(Direction.E) == false) PlaceWindow(new Vector3(r.E, yPos, r.N + windowPositionH), Direction.E, g);
            if (r.IsDirectionBlocked(Direction.W) == false) PlaceWindow(new Vector3(r.W, yPos, r.N + windowPositionH), Direction.W, g);
        }
    }

    private void PlaceRoomRailing(Room r, GameObject g) {
        float yPos = Settings.RoofHeight * r.Floor;

        PlaceRailing(new Vector3(r.X, yPos, r.N), Direction.N, r.Width, g);
        PlaceRailing(new Vector3(r.X, yPos, r.S), Direction.S, r.Width, g);
        PlaceRailing(new Vector3(r.E, yPos, r.Z), Direction.E, r.Height, g);
        PlaceRailing(new Vector3(r.W, yPos, r.Z), Direction.W, r.Height, g);
    }

    private void PlaceRoomRailingCorners(Room r, GameObject g) {
        float yPos = Settings.WallHeight / 2 + (Settings.RoofHeight * r.Floor);

        PlaceRailingCorner(new Vector3(r.W, yPos, r.N), g);
        PlaceRailingCorner(new Vector3(r.E, yPos, r.N), g);
        PlaceRailingCorner(new Vector3(r.W, yPos, r.S), g);
        PlaceRailingCorner(new Vector3(r.E, yPos, r.S), g);
    }

    private void PlaceFloorDoorsAndStairs(int floor) {
        foreach (RoomConnection c in connections[floor]) {
            if (c.isEntranceValid && c.direction != Direction.U) {
                GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.roomStart)];

                PlaceDoor(c.entrancePosition, c.entranceRotation, false, room);

            } else if (c.isEntranceValid && c.direction == Direction.U) {
                GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.roomStart)];

                PlaceStairs(c.entrancePosition, c.entranceRotation, room);
            }
        }
    }

    private void PlaceOutsideDoors(int numberOfDoors) {
        int doorCount = 0;

        while (doorCount < numberOfDoors) {
            Room r = rooms[0][RandomNumber(0, rooms[0].Count - 1)];
            GameObject room = roomObjects[0][rooms[0].IndexOf(r)];
            Direction dir;

            if (r.IsAllDirectionsBlocked()) continue;

            dir = r.GetFreeWallDirection();

            PlaceDoor(Position(dir, Settings.WallHeight, r), Rotation(dir), true, room);
            r.BlockDirection(dir);
            doorCount++;
        }
    }

    private void PlaceGrass() {
        GameObject grass = Instantiate(Library.Grass, new Vector3(GetPlotWidth() / 2, -.1f, GetPlotHeight() / 2), Quaternion.identity, Plot);
        grass.transform.localScale = new Vector3(GetPlotWidth(), 0.2f, GetPlotHeight());
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

    private void PlaceDoor(Vector3 position, Quaternion rotation, bool outside, GameObject room) {
        Instantiate(outside ? Library.OutsideDoor : Library.InsideDoor, position, rotation, room.transform);
    }
    
    private void PlaceStairs(Vector3 position, Quaternion rotation, GameObject room) {
        Instantiate(Library.Stairs, position, rotation, room.transform);
    }

    private void PlaceRailing(Vector3 position, Direction dirFacing, float length, GameObject room) {
        GameObject railing = Instantiate(Library.Railing, position, Rotation(dirFacing), room.transform);
        railing.transform.localScale = new Vector3(1f, 1f, length);
    }

    private void PlaceRailingCorner(Vector3 cornerPoint, GameObject room) {
        GameObject corner = Instantiate(Library.Corner, cornerPoint, Quaternion.identity, room.transform);
        corner.transform.localScale = new Vector3(.3f, 1.5f, .3f);
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
        return Settings.PlotWidth;
    }

    private static int GetPlotHeight() {
        return Settings.PlotHeight;
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

    private static Vector3 Position(Direction dir, float yPos, Room room) {
        switch (dir) {
            case Direction.N:
                return new Vector3(room.X, yPos, room.N);
            case Direction.S:
                return new Vector3(room.X, yPos, room.S);
            case Direction.E:
                return new Vector3(room.E, yPos, room.Z);
            case Direction.W:
                return new Vector3(room.W, yPos, room.Z);
            default:
                throw new InvalidEnumArgumentException();
        }
    }

    private static Vector3 Position(Direction dir, float yPos, Room room, float closerToCenter) {
        switch (dir) {
            case Direction.N:
                return new Vector3(room.X, yPos, room.N + closerToCenter);
            case Direction.S:
                return new Vector3(room.X, yPos, room.S - closerToCenter);
            case Direction.E:
                return new Vector3(room.E - closerToCenter, yPos, room.Z);
            case Direction.W:
                return new Vector3(room.W + closerToCenter, yPos, room.Z);
            default:
                throw new InvalidEnumArgumentException();
        }
    }

    private static Vector3 Position(Direction dir, float yPos, float posOnWall, Room room) {
        switch (dir) {
            case Direction.N:
                return new Vector3(posOnWall, yPos, room.N);
            case Direction.S:
                return new Vector3(posOnWall, yPos, room.S);
            case Direction.E:
                return new Vector3(room.E, yPos, posOnWall);
            case Direction.W:
                return new Vector3(room.W, yPos, posOnWall);
            default:
                throw new InvalidEnumArgumentException();
        }
    }
}
