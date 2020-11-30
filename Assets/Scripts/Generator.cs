using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using System.Diagnostics;

public class Generator : MonoBehaviour {
    private const float WALL_HEIGHT = 1.4f;
    private const float ROOF_HEIGHT = 3.0f;

    public PrefabLibrary Library;

    public Transform Master;
    public Text Map;
    public Text Stats;
    public CanvasGroup UI;

    public int PlotHeight = 50, PlotWidth = 50;
    public int MaxRoomSize = 15;
    public int RoomNumber = 4;
    public int FloorNumber = 2;

    public bool SquareRooms = false;
    public bool RoofEnabled = false;

    private char[,,] floorPlan;
    private static List<Room>[] rooms;

    private bool showingMap = false;

    enum Direction { LEFT, RIGHT, UP, DOWN }

    private class Room {
        public struct RoomConnection {
            public Direction direction;
            public Room adjacentRoom;
            public int doorPosition;
            public bool hasDoor;
        }

        public List<RoomConnection> Connections = new List<RoomConnection>();

        public int x, y, width, height, floor;
        public int left, right, top, bottom;
        private char[,,] floorPlan;

        public static bool AddRoom(int x, int y, int width, int height, int floor, ref char[,,] floorPlan, Direction dest) {
            var room = new Room {
                x = x,
                y = y,
                width = width,
                height = height,
                floor = floor,
                floorPlan = floorPlan,

                left = x - (width / 2),
                right = x + (width / 2),
                top = y - (height / 2),
                bottom = y + (height / 2)
            };

            if (rooms[floor].Count == 0 || floor > 0 || room.IsRoomValid(dest) == true) {
                room.GenerateFloor();
                room.GenerateWalls();
                room.GenerateConnections();

                rooms[floor].Add(room);

                return true;
            }
            
            return false;
        }

        public bool IsRoomValid(Direction dir) {
            if (left - 1 < 0) {
                return false;
            } else if (right + 1 >= floorPlan.GetLength(1)) {
                return false;
            } else if (top - 1 < 0) {
                return false;
            } else if (bottom + 1 >= floorPlan.GetLength(0)) {
                return false;
            } else if (floorPlan[y, x, floor] == 'F') {
                return false;
            }

            switch (dir) {
                case Direction.LEFT:
                    if (CheckWalls(dir, rooms[floor][rooms[floor].Count - 1].left, right) != true) return false;
                    else break;
                case Direction.RIGHT:
                    if (CheckWalls(dir, rooms[floor][rooms[floor].Count - 1].right, left) != true) return false;
                    else break;
                case Direction.UP:
                    if (CheckWalls(dir, rooms[floor][rooms[floor].Count - 1].top, bottom) != true) return false;
                    else break;
                case Direction.DOWN:
                    if (CheckWalls(dir, rooms[floor][rooms[floor].Count - 1].bottom, top) != true) return false;
                    else break;
            }

            for (int i = left; i < right; i++) {
                if (floorPlan[top, i, floor] == 'F' || floorPlan[bottom, i, floor] == 'F') {
                    return false;
                }
            }

            for (int i = top; i < bottom; i++) {
                if (floorPlan[i, left, floor] == 'F' || floorPlan[i, right, floor] == 'F') {
                    return false;
                }
            }

            return true;
        }

        private bool CheckWalls(Direction dir, int lastRoomWall, int curRoomWall) {
            
            // Checking whether last added room is directly adjacent to this one
            if (lastRoomWall != curRoomWall) return false;

            // Checking whether any of the adjacent rooms are in the way of this new one
            foreach (RoomConnection c in rooms[floor][rooms[floor].Count - 1].Connections) {
                if (c.direction == dir) return false;
            }

            return true;
        }

        private void GenerateWalls() {

            for (int i = left; i < right; i++) {
                ref char cell = ref floorPlan[top, i, floor];
                if (cell != 'F' && cell != 'o') cell = '—';

                cell = ref floorPlan[bottom, i, floor];
                if (cell != 'F' && cell != 'o') cell = '—';
            }

            for (int i = top; i < bottom; i++) {
                ref char cell = ref floorPlan[i, left, floor];
                if (cell != 'F' && cell != 'o') cell = '|';

                cell = ref floorPlan[i, right, floor];
                if (cell != 'F' && cell != 'o') cell = '|';
            }

            floorPlan[top, left, floor] = 'o';
            floorPlan[top, right, floor] = 'o';
            floorPlan[bottom, left, floor] = 'o';
            floorPlan[bottom, right, floor] = 'o';
        }

        private void GenerateFloor() {
            for (int i = top + 1; i < bottom; i++) {
                for (int j = left + 1; j < right; j++) {
                    floorPlan[i, j, floor] = 'F';
                }
            }
        }

        public void AddConnection(Room r, Direction d, int doorPosition) {
            var connection = new RoomConnection {
                direction = d,
                adjacentRoom = r,
                doorPosition = doorPosition
            };
            Connections.Add(connection);
        }

        private void GenerateConnections() {
            foreach (Room r in rooms[floor]) {
                // Check that the walls are lined up and that the rooms are adjacent to one another
                if (r.bottom == top && r.right > left && right > r.left) {
                    int doorPosition = GenerateDoorPosition(left > r.left ? left : r.left, right < r.right ? right : r.right);
                    AddConnection(r, Direction.UP, doorPosition);
                    r.AddConnection(this, Direction.DOWN, doorPosition);
                } else if (r.top == bottom && r.right > left && right > r.left) {
                    int doorPosition = GenerateDoorPosition(left > r.left ? left : r.left, right < r.right ? right : r.right);
                    AddConnection(r, Direction.DOWN, doorPosition);
                    r.AddConnection(this, Direction.UP, doorPosition);
                } else if (r.right == left && r.top < bottom && top < r.bottom) {
                    int doorPosition = GenerateDoorPosition(top > r.top ? top : r.top, bottom < r.bottom ? bottom : r.bottom);
                    AddConnection(r, Direction.LEFT, doorPosition);
                    r.AddConnection(this, Direction.RIGHT, doorPosition);
                } else if (r.left == right && r.top < bottom && top < r.bottom) {
                    int doorPosition = GenerateDoorPosition(top > r.top ? top : r.top, bottom < r.bottom ? bottom : r.bottom);
                    AddConnection(r, Direction.RIGHT, doorPosition);
                    r.AddConnection(this, Direction.LEFT, doorPosition);
                }
            }
        }

        private int GenerateDoorPosition(int room1Side, int room2Side) {
            return RandomNumber(room1Side + 1, room2Side - 1);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        GenerateAndPlaceRandomBuilding();
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("space")) {
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";

            PrintFloorPlan();
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
            var stopwatch = new Stopwatch();

            stopwatch.Start();
            for (int i = 0; i < 30; i++) GenerateAndPlaceRandomBuilding();
            stopwatch.Stop();

            Stats.text = "Time: " + stopwatch.ElapsedMilliseconds + "ms";

            Map.text = "";
        }
    }

    private void GenerateAndPlaceRandomBuilding() {
        DeleteChildren();
        GenerateFloorPlan();
        PlaceFloorPlan();
    }

    private void DeleteChildren() {
        foreach (Transform child in Master) {
            Destroy(child.gameObject);
        }
    }

    private void GenerateFloorPlan() {
        floorPlan = new char[PlotHeight, PlotWidth, FloorNumber];

        GenerateGrass();
        SetWidthAndHeight(out int roomWidth, out int roomHeight);

        int y = PlotHeight / 2;
        int x = PlotWidth / 2;

        rooms = new List<Room>[FloorNumber];
        rooms[0] = new List<Room>();

        Room.AddRoom(x, y, roomWidth, roomHeight, 0, ref floorPlan, Direction.DOWN);

        int oldX, oldY;
        Direction oldDest = Direction.DOWN;

        for (int i = 0, timeout = 0; i < RoomNumber; i++, timeout++) {
            oldX = x;
            oldY = y;

            Direction dest = (Direction) RandomNumber(0, 3);

            if (dest == oldDest) {
                if (dest == Direction.LEFT)  dest++;
                if (dest == Direction.RIGHT) dest++;
                if (dest == Direction.UP)    dest--;
                if (dest == Direction.DOWN)  dest--;
            }

            MoveXY(roomWidth, roomHeight, ref y, ref x, dest);
            SetWidthAndHeight(out roomWidth, out roomHeight);
            MoveXY(roomWidth, roomHeight, ref y, ref x, dest);

            // If room failed to complete it's check, rollback x and y and decrement the room number
            if (Room.AddRoom(x, y, roomWidth, roomHeight, 0, ref floorPlan, dest) != true) {
                if (timeout != 10) {
                    i--;
                    x = oldX;
                    y = oldY;
                } else {
                    UnityEngine.Debug.Log("Timed out when generating room");
                    timeout = 0;
                    x = oldX;
                    y = oldY;
                }
            } else {
                oldDest = dest;
            }
        }

        for (int i = 1; i < FloorNumber; i++) {
            rooms[i] = new List<Room>();
            foreach (Room r in rooms[i - 1]) {
                // VERY WROOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOONG (the direction thing)
                Room.AddRoom(r.x, r.y, r.width, r.height, i, ref floorPlan, Direction.UP);
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

    private void SetWidthAndHeight(out int roomWidth, out int roomHeight) {
        roomWidth = RandomNumber(4, MaxRoomSize);
        roomHeight = SquareRooms ? roomWidth : RandomNumber(4, MaxRoomSize);
        if (roomWidth % 2 != 0) roomWidth++;
        if (roomHeight % 2 != 0) roomHeight++;
    }

    private static void MoveXY(int roomWidth, int roomHeight, ref int y, ref int x, Direction dest) {
        switch (dest) {
            case Direction.LEFT:
                x -= roomWidth / 2;
                break;
            case Direction.RIGHT:
                x += roomWidth / 2;
                break;
            case Direction.UP:
                y -= roomHeight / 2;
                break;
            case Direction.DOWN:
                y += roomHeight / 2;
                break;
        }
    }

    private void GenerateGrass() {
        for (int i = 0; i < PlotHeight; i++) {
            for (int j = 0; j < PlotWidth; j++) {
                for (int k = 0; k < FloorNumber; k++) {
                    floorPlan[i, j, k] = 'G';
                }
            }
        }
    }

    private void PrintFloorPlan() {
        string plan = "";
        for (int i = 0; i < PlotHeight; i++) {
            for (int j = 0; j < PlotWidth; j++) {
                plan += floorPlan[i, j, 0] + " ";
            }
            plan += "\n";
        }

        Map.text = plan;
    }

    private void PlaceGrass() {
        GameObject grass = Instantiate(Library.Grass, new Vector3(PlotHeight / 2, -.1f, PlotWidth / 2), Quaternion.identity, Master);
        grass.transform.localScale = new Vector3(PlotHeight, 0.2f, PlotWidth);
    }

    private void PlaceCorners() {
        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                PlaceCorner(new Vector3(r.top,    WALL_HEIGHT + (ROOF_HEIGHT * i), r.left));
                PlaceCorner(new Vector3(r.top,    WALL_HEIGHT + (ROOF_HEIGHT * i), r.right));
                PlaceCorner(new Vector3(r.bottom, WALL_HEIGHT + (ROOF_HEIGHT * i), r.left));
                PlaceCorner(new Vector3(r.bottom, WALL_HEIGHT + (ROOF_HEIGHT * i), r.right));
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

                GameObject floor = Instantiate(Library.Floor, new Vector3(r.y, ROOF_HEIGHT * i, r.x), Quaternion.identity, Master);
                floor.transform.localScale = new Vector3(r.height, 0.2f + (j * 0.001f), r.width);
            }
        }
    }

    private void PlaceWalls() {
        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                GameObject wall;

                wall = Instantiate(Library.Wall, new Vector3(r.top, WALL_HEIGHT + (ROOF_HEIGHT * i), (r.left + r.right) / 2), Quaternion.Euler(0, 0, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.width);

                wall = Instantiate(Library.Wall, new Vector3((r.top + r.bottom) / 2, WALL_HEIGHT + (ROOF_HEIGHT * i), r.right), Quaternion.Euler(0, 90, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.height);

                wall = Instantiate(Library.Wall, new Vector3(r.bottom, WALL_HEIGHT + (ROOF_HEIGHT * i), (r.left + r.right) / 2), Quaternion.Euler(0, 180, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.width);

                wall = Instantiate(Library.Wall, new Vector3((r.top + r.bottom) / 2, WALL_HEIGHT + (ROOF_HEIGHT * i), r.left), Quaternion.Euler(0, 270, 0), Master);
                wall.transform.localScale = new Vector3(0.2f, 3f, r.height);
            }
        }
    }

    private void PlaceDoors() {
        Room start = rooms[0][0];

        if (floorPlan[start.top - 1, start.left + (start.width / 2), 0] == 'G') {
            PlaceDoor(new Vector3(start.top, WALL_HEIGHT, start.left + (start.width / 2)), Quaternion.Euler(0, 0, 0), true);
        } else if (floorPlan[start.bottom + 1, start.left + (start.width / 2), 0] == 'G') {
            PlaceDoor(new Vector3(start.bottom, WALL_HEIGHT, start.left + (start.width / 2)), Quaternion.Euler(0, 180, 0), true);
        } else if (floorPlan[start.top + (start.height / 2), start.left - 1, 0] == 'G') {
            PlaceDoor(new Vector3(start.top + (start.height / 2), WALL_HEIGHT, start.left), Quaternion.Euler(0, 270, 0), true);
        } else if (floorPlan[start.top + (start.height / 2), start.right + 1, 0] == 'G') {
            PlaceDoor(new Vector3(start.top + (start.height / 2), WALL_HEIGHT, start.right), Quaternion.Euler(0, 90, 0), true);
        }

        for (int i = 0; i < FloorNumber; i++) {
            foreach (Room r in rooms[i]) {
                foreach (Room.RoomConnection connection in r.Connections) {

                    // Checking that this connection has not already been doored
                    if (rooms[i].IndexOf(connection.adjacentRoom) > rooms[i].IndexOf(r)) {
                        switch (connection.direction) {
                            case Direction.LEFT:
                                PlaceDoor(new Vector3(connection.doorPosition, WALL_HEIGHT + (ROOF_HEIGHT * i), r.left), Quaternion.Euler(0, 270, 0), false);
                                break;
                            case Direction.RIGHT:
                                PlaceDoor(new Vector3(connection.doorPosition, WALL_HEIGHT + (ROOF_HEIGHT * i), r.right), Quaternion.Euler(0, 90, 0), false);
                                break;
                            case Direction.UP:
                                PlaceDoor(new Vector3(r.top, WALL_HEIGHT + (ROOF_HEIGHT * i), connection.doorPosition), Quaternion.Euler(0, 0, 0), false);
                                break;
                            case Direction.DOWN:
                                PlaceDoor(new Vector3(r.bottom, WALL_HEIGHT + (ROOF_HEIGHT * i), connection.doorPosition), Quaternion.Euler(0, 180, 0), false);
                                break;
                        }
                    }
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
                
                bool left = true, right = true, top = true, bottom = true;

                foreach (Room.RoomConnection c in r.Connections) {
                         if (c.direction == Direction.LEFT)  left   = false;
                    else if (c.direction == Direction.RIGHT) right  = false;
                    else if (c.direction == Direction.UP)    top    = false;
                    else if (c.direction == Direction.DOWN)  bottom = false;
                }

                if (left) PlaceWindow(new Vector3(r.top + RandomNumber(1, r.height - 1), WALL_HEIGHT + (ROOF_HEIGHT * i), r.left), Quaternion.Euler(0, 270, 0));
                if (right) PlaceWindow(new Vector3(r.top + RandomNumber(1, r.height - 1), WALL_HEIGHT + (ROOF_HEIGHT * i), r.right), Quaternion.Euler(0, 90, 0));
                if (top) PlaceWindow(new Vector3(r.top, WALL_HEIGHT + (ROOF_HEIGHT * i), r.left + RandomNumber(1, r.width - 1)), Quaternion.Euler(0, 0, 0));
                if (bottom) PlaceWindow(new Vector3(r.bottom, WALL_HEIGHT + (ROOF_HEIGHT * i), r.left + RandomNumber(1, r.width - 1)), Quaternion.Euler(0, 180, 0));
            }
        }
    }

    private void PlaceWindow(Vector3 position, Quaternion rotation) {
        Instantiate(Library.Window, position, rotation, Master);
    }

    private void PlaceRoofing() {
        foreach (Room r in rooms[rooms.Length - 1]) {
            GameObject roof = Instantiate(Library.Floor, new Vector3(r.y, ROOF_HEIGHT, r.x), Quaternion.identity, Master);
            roof.transform.localScale = new Vector3(r.height, 0.2f, r.width);
        }
    }

    private static bool RandomBool() {
        if (RandomNumber(1, 2) == 1) {
            return true;
        } else {
            return false;
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
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static float RandomNumber(float min, float max) {
        return UnityEngine.Random.Range(min, max);
    }

}
