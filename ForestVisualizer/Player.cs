using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForestVisualizer
{
    public class Player
    {
        public Player(int id, string name, Point startPos, Point target, int hp)
        {
            Id = id;
            Nick = name;
            StartPosition = startPos;
            Target = target;
            Hp = hp;
            CurrentPosition = startPos;
        }

        public int Id;
        public string Nick;
        public int Hp;
        public Point StartPosition;
        public Point Target;
        public Point CurrentPosition;
    }

    public class TablePosition
    {
        public TablePosition(string name)
        {
            Name = name;
            Wins = 0;
            Draws = 0;
            Loses = 0;
            Points = 0;
        }

        public string Name;
        public int Wins;
        public int Draws;
        public int Loses;
        public int Points;
    }
}
