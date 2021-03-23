using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

public class Generator : MonoBehaviour {

    public Transform Plot;

    public static Generator Singleton;

    private static PrefabLibrary Library;
    private static Settings Settings;

    private static List<Room>[] rooms;
    private static List<RoomConnection>[] connections;

    private static List<GameObject>[] roomObjects;
    private static GameObject[] floors;

    private static List<Roof> roofs;

    private static int currentID = 0;

    // North, South, East, West, Up, Down
    private enum Direction { N, S, E, W, U, D }

    private class RoomConnection {
        public string     ID;
        public Direction  Direction;
        public Room       RoomStart;
        public Room       RoomEnd;
        public bool       IsEntranceValid;
        public Vector3    EntrancePosition;
        public Quaternion EntranceRotation;
    }

    private class Roof {
        public Quaternion Rotation;
        public Vector3    Position;
        public Vector3    Scale;
        public int        Floor;
    }

    private class Room {
        // Ensures there's a slight gap between bounds to make sure they don't intersect
        private const float BOUNDS_GAP = 0.001f;

        // The room's dimensions
        private Bounds bounds;

        // Any walls that have something blocking them
        private readonly Dictionary<Direction, bool> isDirectionBlocked = new Dictionary<Direction, bool> {
            { Direction.N, false },
            { Direction.S, false },
            { Direction.W, false },
            { Direction.E, false },
            { Direction.U, false },
            { Direction.D, false }
        };

        public string ID;
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

            GameObject g = Instantiate(Library.RoomHolder, floors[room.Floor].transform);
            g.name = room.ID;

            roomObjects[room.Floor].Add(g);
        }

        public static void AddFirstRoom() {
            float xPos = GetPlotWidth()  / 2;
            float zPos = GetPlotHeight() / 2;
            float width  = RandomNumber(Settings.MinRoomSize, Settings.MaxRoomSize) - BOUNDS_GAP * 2;
            float height = RandomNumber(Settings.MinRoomSize, Settings.MaxRoomSize) - BOUNDS_GAP * 2;

            AddRoom(new Room {
                ID = NewID(),
                Floor = 0,
                bounds = new Bounds(new Vector3(xPos, 0, zPos), new Vector3(width, 0, height))
            });
        }

        public static void AddRandomGroundRoom() {
            bool successfulRoomPlaced;

            int count = 0;
            while (true) {
                successfulRoomPlaced = TryToAddRandomGroundFloorRoom();
                if (successfulRoomPlaced) break;

                count++;
                if (count == 1000) {
                    Debug.Log("Error in adding stairwell");
                }
            }
        }

        public static void AddStairwellAndRoom(int floor) {
            bool successfulRoomPlaced;

            int count = 0;
            while (true) {
                successfulRoomPlaced = TryToAddStairwellAndRoom(floor);
                if (successfulRoomPlaced) break;

                count++;
                if (count == 1000) {
                    Debug.Log("Error in adding stairwell");
                }
            }
        }

        public static void AddRemainingRoomsOnFloor(int floor) {
            for (int i = 0; i < rooms[floor - 1].Count - Settings.RemoveRoomNum; i++) {
                Room r = rooms[floor - 1][i];

                if (r.isDirectionBlocked[Direction.U] == false) {
                    Room newRoom = new Room {
                        ID = NewID(),
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
            int count = 0;
            for (int i = 0; i < numberOfRooms; i++) {
                if (TryToChangeRandomRoomToBalcony(floor) != true) i--;
                count++;
                if (count == 1000) {
                    Debug.Log("Error in making balcony place");
                }
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
                ID = NewID(),
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
                ID = NewID(),
                Floor = floor + 1,
                bounds = new Bounds(new Vector3(start.X, Settings.RoofHeight * (floor + 1), start.Z), start.bounds.size)
            };

            AddRoom(newRoom);

            start.isDirectionBlocked[Direction.U] = true;
            newRoom.isDirectionBlocked[Direction.D] = true;

            connections[floor].Add(new RoomConnection {
                ID = NewID(),
                Direction = Direction.U,
                RoomStart = start,
                RoomEnd = newRoom,
                IsEntranceValid = true,
                EntrancePosition = stairPosition,
                EntranceRotation = stairRotation
            });

            return true;
        }

        private static bool TryToChangeRandomRoomToBalcony(int floor) {
            Room r = rooms[floor][RandomNumber(0, rooms[floor].Count - 1)];

            foreach (RoomConnection c in connections[floor - 1]) {
                if (c.RoomEnd == r || r.IsBalconyRoom && r.IsAllDirectionsBlocked() != true) return false;
            }

            r.BlockDirection(Direction.U);
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
                     if (Equal(nextRoom.S, N) && LessNotEqual(nextRoom.W, E) && LessNotEqual(W, nextRoom.E)) AddDoorConnection(nextRoom, Direction.N);
                else if (Equal(nextRoom.N, S) && LessNotEqual(nextRoom.W, E) && LessNotEqual(W, nextRoom.E)) AddDoorConnection(nextRoom, Direction.S);
                else if (Equal(nextRoom.E, W) && LessNotEqual(nextRoom.N, S) && LessNotEqual(N, nextRoom.S)) AddDoorConnection(nextRoom, Direction.W);
                else if (Equal(nextRoom.W, E) && LessNotEqual(nextRoom.N, S) && LessNotEqual(N, nextRoom.S)) AddDoorConnection(nextRoom, Direction.E);
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
                ID = NewID(),
                Direction = dir,
                RoomStart = this,
                RoomEnd = adjacentRoom,
                IsEntranceValid = isDoorValid,
                EntrancePosition = doorPosition,
                EntranceRotation = doorRotation
            };

            connections[Floor].Add(connection);

        }

        private bool IsConnectionAlreadyAdded(Room room) {
            foreach (RoomConnection c in connections[Floor]) {
                if ((this == c.RoomStart && room == c.RoomEnd) || (room == c.RoomStart && this == c.RoomEnd)) {
                    return true;
                }
            }

            return false;
        }

        private void RemoveRoomConnections() {
            for (int room = 0; room < connections[Floor].Count; room++) {
                RoomConnection c = connections[Floor][room];
                
                if (this == c.RoomEnd || this == c.RoomStart) { 
                    connections[Floor].Remove(c);
                    room--;
                }
            }
        }

        // - - - Door Placements - - - //

        public List<RoomConnection> GetDoorConnections() {
            List<RoomConnection> connectedRooms = new List<RoomConnection>();

            foreach (RoomConnection c in connections[Floor]) {
                if (c.Direction != Direction.U && c.IsEntranceValid) {
                    if (c.RoomStart == this || c.RoomEnd == this) {
                        connectedRooms.Add(c);
                    }
                }
            }

            return connectedRooms;
        }

        public static void PathFindDoorPlacements(int floor) {
            List<string> visitedRooms = new List<string>();
            List<string> traversedConnections = new List<string>();

            // Getting first room
            Room currentRoom = rooms[floor][0];

            int count = 0;

            // Cycling through until all rooms are explored
            while (visitedRooms.Count < rooms[floor].Count) {
                if (!visitedRooms.Contains(currentRoom.ID)) visitedRooms.Add(currentRoom.ID);

                List<RoomConnection> connectedRooms = currentRoom.GetDoorConnections();

                foreach (RoomConnection c in connectedRooms) {
                    Room adjacent = c.RoomStart == currentRoom ? c.RoomEnd : c.RoomStart;

                    // If other room has been visited & connection not already traversed, remove it
                    if (visitedRooms.Contains(adjacent.ID) && !traversedConnections.Contains(c.ID)) {
                        c.IsEntranceValid = false;
                    }
                }

                // Selecting random connection
                int randomConnection = RandomNumber(0, connectedRooms.Count - 1);
                RoomConnection connection = connectedRooms[randomConnection];

                // Marking connection traversed and moving to new room
                if (!traversedConnections.Contains(connection.ID)) traversedConnections.Add(connection.ID);
                currentRoom = connection.RoomStart == currentRoom ? connection.RoomEnd : connection.RoomStart;

                count++;
                if (count == 1000) {
                    Debug.Log("Error in Door placements");
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
        Singleton = this;
    }

    // - - - Roof Placements - - - //

    private static void GenerateRoofing() {
        List<Room> roomsToBeRoofed = new List<Room>();

        // Get rooms that need a roof
        for (int i = 0; i < rooms.Length; i++) {
            foreach (Room r in rooms[i]) {
                if (r.IsDirectionBlocked(Direction.U) != true) {
                    roomsToBeRoofed.Add(r);
                }
            }
        }

        int count = 0;

        // Loop through rooms to be roofed and add relevant roofing
        while (roomsToBeRoofed.Count > 0) {
            Room r = roomsToBeRoofed[0];

            // Check whether it's not on the top floor
            if (r.Floor < rooms.Length - 1) {
                Roof roof = GetStandardRoofForRoom(r);
                roofs.Add(roof);

                roomsToBeRoofed.RemoveAt(0);

            // Roof on the top floor
            } else {
                List<Room> roomsPartOfRoof = new List<Room>();

                roomsToBeRoofed.Remove(r);
                roomsPartOfRoof.Add(r);

                Direction roofDirection = Direction.U;

                foreach (RoomConnection c in r.GetDoorConnections()) {
                    Room adj = c.RoomStart == r ? c.RoomEnd : c.RoomStart;

                    if (Equal(r.Width, adj.Width) || Equal(r.Height, adj.Height)) {
                        if (roomsToBeRoofed.Contains(adj)) {
                            roomsToBeRoofed.Remove(adj);
                            roomsPartOfRoof.Add(adj);

                            roofDirection = c.RoomStart == r ? c.Direction : ReverseDirection(c.Direction);

                            IsRoomSameWidthOrHeightInDirection(adj, roomsToBeRoofed, roomsPartOfRoof, roofDirection);
                            break;
                        }
                    }
                }

                // No adjacent rooms found with same width/height that need roofing
                if (roomsPartOfRoof.Count <= 1) {
                    Roof roof = GetStandardRoofForRoom(r);
                    roofs.Add(roof);

                } else {
                    bool horizontal = roofDirection == Direction.E || roofDirection == Direction.W ? true : false;
                    //bool ascending = r.GetWallPosition(roofDirection) > r.GetWallPosition(ReverseDirection(roofDirection));

                    float top = roomsPartOfRoof[0].GetWallPosition(ReverseDirection(roofDirection));
                    float bottom = roomsPartOfRoof[roomsPartOfRoof.Count - 1].GetWallPosition(roofDirection);

                    // Sorting position
                    Vector3 position = new Vector3() {
                        x = horizontal ? (top + bottom) / 2 : r.X,
                        z = horizontal ? r.Z : (top + bottom) / 2,
                        y = Settings.RoofHeight * (r.Floor + 1)
                    };

                    // Sorting scale
                    float length = 0;

                    foreach (Room room in roomsPartOfRoof) {
                        length += horizontal ? room.Width : room.Height;
                    }

                    Vector3 scale = new Vector3() {
                        x = horizontal ? r.Height : r.Width,
                        y = 1,
                        z = length
                    };

                    // Sorting rotation
                    Quaternion rotation = Rotation(roofDirection) * Quaternion.Euler(0,90,0);

                    Roof roof = new Roof {
                        Position = position,
                        Rotation = rotation,
                        Scale    = scale,
                        Floor    = r.Floor
                    };

                    roofs.Add(roof);
                }
            }

            count++;
            if (count == 1000) {
                Debug.Log("Error in making roofs");
            }
        }
    }

    private static void IsRoomSameWidthOrHeightInDirection(Room r, List<Room> roomsToBeRoofed, List<Room> roomsPartOfRoof, Direction dir) {
        foreach (RoomConnection c in r.GetDoorConnections()) {
            if (c.Direction == dir || c.Direction == ReverseDirection(dir)) {
                Room adj = c.RoomStart == r ? c.RoomEnd : c.RoomStart;

                // Either the width or the height is equal to extend roof to encompass new building
                if (Equal(r.Width, adj.Width) || Equal(r.Height, adj.Height)) {
                    if (roomsToBeRoofed.Contains(adj)) {
                        roomsPartOfRoof.Add(adj);
                        roomsToBeRoofed.Remove(adj);

                        IsRoomSameWidthOrHeightInDirection(r, roomsToBeRoofed, roomsPartOfRoof, dir);
                        break;
                    }
                }
            }
        }
    }

    private static Roof GetStandardRoofForRoom(Room r) {
        Roof roof = new Roof {
            Position = new Vector3(r.X, Settings.RoofHeight * (r.Floor + 1), r.Z),
            Floor = r.Floor
        };

        if (r.Width > r.Height) {
            roof.Rotation = Rotation(Direction.N);
            roof.Scale = new Vector3(r.Height, 1, r.Width);
        } else {
            roof.Rotation = Rotation(Direction.E);
            roof.Scale = new Vector3(r.Width, 1, r.Height);
        }

        return roof;
    }

    public void GenerateAndPlaceRandomBuilding() {
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
        roofs = new List<Roof>();

        //Debug.Log("1");

        for (int floor = 0; floor < Settings.FloorNumber; floor++) {
            rooms[floor] = new List<Room>();
            roomObjects[floor] = new List<GameObject>();
            connections[floor] = new List<RoomConnection>();

            floors[floor] = Instantiate(Library.FloorHolder, Plot);
            floors[floor].name = "Floor " + floor;
            floors[floor].tag = "Floor";
        }

        //Debug.Log("2");

        Room.AddFirstRoom();

        for (int i = 1; i < Settings.RoomNumber; i++) {
            Room.AddRandomGroundRoom();
        }

        //Debug.Log("3");

        if (Settings.PathfindDoors) Room.PathFindDoorPlacements(0);

        //Debug.Log("4");

        for (int floor = 1; floor < Settings.FloorNumber; floor++) {
            Room.AddStairwellAndRoom(floor - 1);
            Room.AddRemainingRoomsOnFloor(floor);
            if (Settings.PathfindDoors) Room.PathFindDoorPlacements(floor);
        }

        //Debug.Log("5");

        // Make balcony rooms on top floor
        Room.MakeBalconyRoomsOnFloor(Settings.FloorNumber - 1, Settings.BalconyRoomNum);

        if (Settings.RoofEnabled) GenerateRoofing();
    }

    private void PlaceFloorPlan() {
        PlaceGrass();
        PlaceOutsideDoors(Settings.OutsideDoorNum);

        for (int floor = 0; floor < Settings.FloorNumber; floor++) {
            for (int room = 0; room < rooms[floor].Count; room++) {
                Room r = rooms[floor][room];
                GameObject g = roomObjects[floor][room];

                if (r.IsBalconyRoom == false) {
                    PlaceRoomFloor(r, g, floor, room);
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

        if (Settings.RoofEnabled) {
            foreach (Roof r in roofs) {
                PlaceRoof(r);
            }
        }
    }

    private void PlaceRoomFloor(Room r, GameObject g, int floor, int room) {
        GameObject floorObj = Instantiate(Library.Floor, new Vector3(r.X, Settings.RoofHeight * floor, r.Z), Quaternion.identity, g.transform);
        floorObj.transform.localScale = new Vector3(r.Width, 0.2f + (room * 0.001f), r.Height);
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

        int windowNumberX = Settings.WindowNum < r.Width  ? Settings.WindowNum : (int) r.Width  - 1;
        int windowNumberZ = Settings.WindowNum < r.Height ? Settings.WindowNum : (int) r.Height - 1;

        if (Settings.RandomiseWindows) {
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
            if (c.IsEntranceValid && c.Direction != Direction.U) {
                GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.RoomStart)];

                PlaceDoor(c.EntrancePosition, c.EntranceRotation, false, room);

            } else if (c.IsEntranceValid && c.Direction == Direction.U) {
                GameObject room = roomObjects[floor][rooms[floor].IndexOf(c.RoomStart)];

                PlaceStairs(c.EntrancePosition, c.EntranceRotation, room);
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

    private void PlaceRoof(Roof r) {
        GameObject roofObj = Instantiate(Library.Roof, r.Position + new Vector3(0, .1f, 0), r.Rotation, Plot);
        roofObj.transform.localScale = r.Scale;
        roofObj.name = "Floor " + r.Floor + " Roof";
        roofObj.tag = "Roof";
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

    private static bool Equal(float float1, float float2) {
        return System.Math.Abs(float1 - float2) < 0.01f;
    }

    private static bool GreaterNotEqual(float float1, float float2) {
        return float1 > float2 && !Equal(float1, float2);
    }

    private static bool LessNotEqual(float float1, float float2) {
        return float1 < float2 && !Equal(float1, float2);
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

    private static string NewID() {
        currentID++;
        return currentID - 1 + "";
    }

    public static void ResetID() {
        currentID = 0;
    }

    public static int GetFloorNumber() {
        return rooms.Length;
    }
}
