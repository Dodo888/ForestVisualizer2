using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ForestVisualizer
{
    class Program
    {
        static void Main(string[] args)
        {
            var lines = File.ReadAllLines("test.txt");
            var map = new char[lines.Length - 3, lines[0].Length];
            for (int row = 0; row < lines.Length - 3; row++)
            {
                for (int col = 0; col < lines[row].Length; col++)
                    map[row, col] = lines[row][col];
            }
            string[] playerString = lines[lines.Length - 3].Split();
            Player player1 = new Player(1, "Bilbo", new Point(int.Parse(playerString[1]), int.Parse(playerString[0])),
                new Point(int.Parse(playerString[3]), int.Parse(playerString[2])), int.Parse(playerString[4]));
            playerString = lines[lines.Length - 2].Split();
            Player player2 = new Player(2, "Legolas", new Point(int.Parse(playerString[1]), int.Parse(playerString[0])),
                new Point(int.Parse(playerString[3]), int.Parse(playerString[2])), int.Parse(playerString[4]));
            Player[] players = new Player[2]{player1, player2};
            int fog = int.Parse(lines[lines.Length - 1]);
            string[] all = new string[] { "Aragorn", "Bilbo", "Frodo", "Gandalf", "Gollum", "Legolas", "Thranduil", "Thorin" };
            FormForestVisualizer visualizer = new FormForestVisualizer(/*map,*/ players, all, fog);
            visualizer.RunForestVisualization();
        }
    }

    class FormForestVisualizer : Form, IForestVisualizer
    {
        private bool gameStarted;
        private string winner;
        private int[,] map;
        private int fog;
        private Player[] players;
        private Dictionary<string, Image> images;
        private Dictionary<int, string> names;
        private float scaleW;
        private float scaleH;
        private StringFormat format;
        private Action<Graphics> drawingAction;

        private List<TablePosition> leaderBoard;

        private Timer timer;
        private int tickCount = 0;

        private IPEndPoint ipEndPoint;    //Конечная точка (IP и порт)
        private Socket clientSocket;
        private Stream stream;
        private Serializer serializer;

        public FormForestVisualizer(/*char[,] map,*/ Player[] players, string[] all, int fog)
        {
            //this.map = CharMapToIntMap(map);
            this.map = new int[0, 0];
            this.fog = fog;
            this.players = players;
            this.leaderBoard = new List<TablePosition>();
            foreach (var player in all)
                this.leaderBoard.Add(new TablePosition(player));
            this.names = new Dictionary<int, string>();
            this.names.Add(2, "Block");
            this.names.Add(1, "Terrain");
            this.names.Add(4, "Life");
            this.names.Add(3, "Trap");

            this.ipEndPoint = new IPEndPoint(IPAddress.Loopback, 20000);
            this.clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.serializer = new Serializer();
            this.clientSocket.Connect(ipEndPoint);
            this.stream = new NetworkStream(clientSocket, FileAccess.ReadWrite);

            Hello hello = new Hello() { IsVisualizator = true, Name = "PROEKTOR" };
            clientSocket.Send(serializer.Serialize(hello).ToArray());

            this.gameStarted = false;
            this.winner = "";

            drawingAction = DrawMap;
            DoubleBuffered = true;
            InitImages();
            InitStringFormat();
            CalculateScale();

            timer = new Timer();
            timer.Interval = 1;
            timer.Tick += TimerTick;
            timer.Start();
        }

        private int[,] CharMapToIntMap(char[,] map)
        {
            int[,] intMap = new int[map.GetLength(0),map.GetLength(1)];
            for (int i=0; i<map.GetLength(0); i++)
                for (int j = 0; j < map.GetLength(1); j++)
                {
                    if (map[i, j] == '0') intMap[i, j] = 1;
                    if (map[i, j] == '1') intMap[i, j] = 2;
                    if (map[i, j] == 'L') intMap[i, j] = 4;
                    if (map[i, j] == 'K') intMap[i, j] = 3;
                }
            return intMap;
        }

        private void InitImages()
        {
            images = Directory.EnumerateFiles("Images\\", "*png").ToDictionary(Path.GetFileNameWithoutExtension, 
                                                                               Image.FromFile);
        }

        private void InitStringFormat()
        {
            format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
        }

        private void CalculateScale()
        {
            var rowsCount = map.GetLength(1) + 1;
            var columnsCount = map.GetLength(0) + 1;
            scaleW = (float)Width / rowsCount * (float)0.85;
            scaleH = (float)Height / columnsCount * (float)0.85;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            drawingAction(e.Graphics);
        }

        protected override void OnResize(EventArgs e)
        {
            CalculateScale();
            Invalidate();
        }

        private bool Visible(Point cell)
        {
            foreach (var citizen in players)
                if (Math.Abs(citizen.CurrentPosition.X - cell.X) <= fog &&
                    Math.Abs(citizen.CurrentPosition.Y - cell.Y) <= fog)
                    return true;
            return false;
        }

        private void DrawMap(Graphics graphics)
        {
            graphics.FillRectangle(Brushes.Black, 0, 0,
                Width, Height);
            for (int x = 0; x < map.GetLength(1); x++)
                for (int y = 0; y < map.GetLength(0); y++)
                    graphics.DrawImage(Visible(new Point(y,x)) ? images[names[map[y,x]]] : images[names[map[y,x]]+"Fog"],
                        x * scaleH, y * scaleH,
                        scaleH, scaleH);
            graphics.DrawImage(Visible(players[0].Target) ? images["Finish"] : images["FinishFog"],
                players[0].Target.Y * scaleH, players[0].Target.X * scaleH,
                scaleH, scaleH);
            foreach (var citizen in players)
            {
                graphics.DrawImage(images["Citizen"+citizen.Id.ToString()],
                    citizen.CurrentPosition.Y * scaleH, citizen.CurrentPosition.X * scaleH,
                    scaleH, scaleH);
                graphics.DrawString(citizen.Nick[0].ToString(),
                    new Font("Calibri", (int)(Math.Min(scaleW, scaleH) / 2), FontStyle.Bold), Brushes.White,
                    new RectangleF(citizen.CurrentPosition.Y * scaleH, citizen.CurrentPosition.X * scaleH,
                        scaleH, scaleH), format);
            }
            var statsScale = (float)Height / 15;
            if (gameStarted)
                DrawStats(graphics, statsScale);
            else
                DrawEndGame(graphics, statsScale);
            DrawLeaderboard(graphics, statsScale);
        }

        private void DrawStats(Graphics graphics, float statsScale)
        {
            float statsWidth = Width - map.GetLength(1) * scaleH;
            graphics.DrawString("STATS:",
                new Font("Calibri", (int)(statsScale / 2), FontStyle.Bold), Brushes.White,
                new RectangleF(map.GetLength(1) * scaleH + 10, 0,
                (int)(Width - map.GetLength(1) * scaleH), statsScale), StringFormat.GenericDefault);
            string[] statsHeader = new string[] { "Name", "HP", "DTF", "PTW" };
            string[][] playersStats = new string[2][];
            foreach (var citizen in players)
            {
                playersStats[citizen.Id - 1] = new string[] {citizen.Nick, citizen.Hp.ToString(), 
                    (Math.Abs(citizen.Target.X - citizen.CurrentPosition.X) + Math.Abs(citizen.Target.Y - citizen.CurrentPosition.Y)).ToString(), 
                    "10", citizen.Id.ToString()};
            }
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 4; j++)
                {
                    RectangleF cell = new RectangleF();
                    if (j==0)
                        cell = new RectangleF(map.GetLength(1) * scaleH + j * statsWidth / 6 + 10, (i + 1) * statsScale,
                            2 * statsWidth / 6, statsScale);
                    else
                        cell = new RectangleF(map.GetLength(1) * scaleH + (j + 1) * statsWidth / 6 + 10, (i + 1) * statsScale,
                            statsWidth / 6, statsScale);
                    graphics.DrawRectangles(new Pen(Brushes.White),
                        new RectangleF[] { cell });
                    if (i == 0)
                        graphics.DrawString(statsHeader[j],
                            new Font("Calibri", (int)(statsScale / 2.5), FontStyle.Bold), Brushes.White,
                            cell, format);
                    else
                        graphics.DrawString(playersStats[i-1][j],
                            new Font("Calibri", (int)(statsScale / 2.5), FontStyle.Bold), playersStats[i - 1][4] == "1" ? Brushes.SteelBlue : Brushes.Green,
                            cell, format);
                }
        }

        private void DrawLeaderboard(Graphics graphics, float statsScale)
        {
            float statsWidth = Width - map.GetLength(1) * scaleH;
            graphics.DrawString("LEADERBOARD:",
                new Font("Calibri", (int)(statsScale / 2), FontStyle.Bold), Brushes.White,
                new RectangleF(map.GetLength(1) * scaleH + 10, 4 * statsScale,
                (int)(Width - map.GetLength(1) * scaleH), statsScale), StringFormat.GenericDefault);
            string[] boardHeader = new string[] { "Pos", "Name", "G", "W", "D", "L", "Pts" };
            for (int i = 0; i < 9; i++)
                for (int j = 0; j < 7; j++)
                {
                    RectangleF cell = new RectangleF();
                    if (j == 0)
                        cell = new RectangleF(map.GetLength(1) * scaleH + j * statsWidth / 9 + 10, (i + 5) * statsScale,
                            statsWidth / 9, statsScale);
                    if (j == 1)
                        cell = new RectangleF(map.GetLength(1) * scaleH + j * statsWidth / 9 + 10, (i + 5) * statsScale,
                            2* statsWidth / 9, statsScale);
                    if (j > 1)
                        cell = new RectangleF(map.GetLength(1) * scaleH + (j + 1) * statsWidth / 9 + 10, (i + 5) * statsScale,
                            statsWidth / 9, statsScale);

                    graphics.DrawRectangles(new Pen(Brushes.White),
                        new RectangleF[] { cell });
                    if (i == 0)
                        graphics.DrawString(boardHeader[j],
                            new Font("Calibri", (int)(statsScale / 2.5), FontStyle.Bold), Brushes.White,
                            cell, format);
                }
            var count = 1;
            foreach (var position in leaderBoard.OrderByDescending(x => (x.Points)).ToArray())
            {
                string[] pos = new string[] {count.ToString(), position.Name, (position.Wins + position.Draws + position.Loses).ToString(),
                    position.Wins.ToString(), position.Draws.ToString(), position.Loses.ToString(), position.Points.ToString()};
                for (int j = 0; j < 7; j++)
                {
                    if (j==0)
                    graphics.DrawString(pos[j],
                            new Font("Calibri", (int)(statsScale / 3), FontStyle.Bold), players.Select(x => x.Nick).Contains(position.Name) ? Brushes.Red : Brushes.White,
                            new RectangleF(map.GetLength(1) * scaleH + j * statsWidth / 9 + 10, (count + 5) * statsScale,
                            statsWidth / 9, statsScale), format);
                    if (j == 1)
                        graphics.DrawString(pos[j],
                            new Font("Calibri", (int)(statsScale / 3), FontStyle.Bold), players.Select(x => x.Nick).Contains(position.Name) ? Brushes.Red : Brushes.White,
                            new RectangleF(map.GetLength(1) * scaleH + j * statsWidth / 9 + 10, (count + 5) * statsScale,
                            2* statsWidth / 9, statsScale), format);
                    if (j>1)
                        graphics.DrawString(pos[j],
                            new Font("Calibri", (int)(statsScale / 3), FontStyle.Bold), players.Select(x => x.Nick).Contains(position.Name) ? Brushes.Red : Brushes.White,
                            new RectangleF(map.GetLength(1) * scaleH + (j + 1) * statsWidth / 9 + 10, (count + 5) * statsScale,
                            statsWidth / 9, statsScale), format);
                }
                count++;
            }
        }

        private void DrawEndGame(Graphics graphics, float statsScale)
        {
            graphics.DrawString(winner + " победил",
                new Font("Impact", (int)(statsScale / 2), FontStyle.Bold), Brushes.White,
                new RectangleF(map.GetLength(1) * scaleH, 0,
                (int)(Width - map.GetLength(1) * scaleH), statsScale), format);
        }

        private void MakeMove(Tuple<int, Point, int> move)
        {
            foreach (var player in players)
                if ((move.Item1 % 2) + 1 == player.Id)
                {
                    player.CurrentPosition = move.Item2;
                    player.Hp = move.Item3;
                }
        }

        private void ChangeCell(Tuple<Point, int> cell)
        {
            map[cell.Item1.X, cell.Item1.Y] = cell.Item2;
        }

        private void RecieveInfo()
        {
            byte[] data = new byte[1024];
            clientSocket.Receive(data);
            if (!gameStarted)
            {
                /*var s = "";
                for (int i = 0; i < data.Length; i++)
                    s += (char)data[i];*/
                WorldInfo world = serializer.Deserialize<WorldInfo>(new MemoryStream(data));
                if (world != null)
                {
                    this.players = world.Players;
                    foreach (var player in players)
                    {
                        player.CurrentPosition = player.StartPosition;
                        player.Id = (player.Id % 2) + 1;
                    }
                        this.map = world.Map;
                    gameStarted = true;
                    winner = "";
                }
                CalculateScale();
            }
            else
            {
                string loserName = "";
                LastMoveInfo move = serializer.Deserialize<LastMoveInfo>(new MemoryStream(data));
                Console.WriteLine(move.GameOver);
                foreach (var cellChange in move.ChangedCells)
                    ChangeCell(cellChange);
                foreach (var positionChange in move.PlayersChangedPosition)
                    MakeMove(positionChange);
                int points = 0;
                if (move.GameOver)
                {
                    bool hasWinner = false;
                    foreach (var player in players)
                        if (player.CurrentPosition.X == player.Target.X && player.CurrentPosition.Y == player.Target.Y)
                        {
                            winner = player.Nick;
                            hasWinner = true;
                        }
                        else
                            loserName = player.Nick;
                    if (hasWinner)
                    {
                        foreach (var position in leaderBoard)
                            if (position.Name == loserName)
                            {
                                position.Loses++;
                                points = position.Points / 3;
                                position.Points -= points;
                            }
                        foreach (var position in leaderBoard)
                            if (position.Name == winner)
                            {
                                position.Wins++;
                                position.Points += points;
                            }
                    }
                    else
                    {
                        foreach (var position in leaderBoard)
                        {
                            if (position.Name == players[0].Nick || position.Name == players[1].Nick)
                            {
                                position.Draws++;
                            }
                        }
                        winner = "Никто не";
                    }
                    gameStarted = false;
                    /*clientSocket.Close();
                    clientSocket.Connect(ipEndPoint);
                    this.stream = new NetworkStream(clientSocket, FileAccess.ReadWrite);
                    Hello hello = new Hello() { IsVisualizator = true, Name = "PROEKTOR" };
                    clientSocket.Send(serializer.Serialize(hello).ToArray());*/
                }
            }
            Answer ans = new Answer() { AnswerCode = 0 };
            clientSocket.Send(serializer.Serialize(ans).ToArray());
        }

        void TimerTick(object sender, EventArgs args)
        {
            RecieveInfo();
            Invalidate();
        }

        public void Display()
        {
            // You don't ever need to use this method
            Invalidate();
        }

        public void RunForestVisualization()
        {
            //var handle = GetConsoleWindow();
            //ShowWindow(handle, 0);

            //FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            Application.Run(this);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr HWND, int nCmdShow);
    }
}