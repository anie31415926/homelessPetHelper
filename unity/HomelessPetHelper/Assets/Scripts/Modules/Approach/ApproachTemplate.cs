using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Approach
{
    public sealed class ApproachTemplate
    {
        private readonly string[] _rows;
        public string Id { get; }
        public ApproachMode Mode { get; }
        public int Columns => _rows[0].Length;
        public int Rows => _rows.Length;
        public int CellCount => Columns * Rows;
        public int Entrance { get; }
        public int Animal { get; }
        public IReadOnlyList<int> RequiredClearCells { get; }
        public IReadOnlyList<int> BridgeCells { get; }
        public IReadOnlyList<int> Boxes { get; }
        public IReadOnlyList<ApproachDebris> Debris { get; }
        public char Terrain(int cell) => _rows[cell / Columns][cell % Columns];
        public bool Contains(int cell) => cell >= 0 && cell < CellCount;

        internal ApproachTemplate(ApproachCatalogData.Template data, int[] requiredClearCells)
        {
            Id = data.id;
            Mode = data.mode;
            _rows = (string[])data.rows.Clone();
            RequiredClearCells = Array.AsReadOnly(requiredClearCells == null ? Array.Empty<int>() :
                (int[])requiredClearCells.Clone());
            foreach (int cell in RequiredClearCells)
                if (!Contains(cell) || Terrain(cell) == '#')
                    throw new ArgumentException("Required route cell is outside usable terrain.");
            Entrance = data.entrance;
            Animal = data.animal;
            BridgeCells = Array.AsReadOnly((int[])data.bridgeCells.Clone());
            Boxes = Array.AsReadOnly((int[])data.boxes.Clone());
            var debris = new List<ApproachDebris>();
            foreach (var item in data.debris)
                debris.Add(new ApproachDebris(item.cell, item.tool, item.workSeconds));
            Debris = new ReadOnlyCollection<ApproachDebris>(debris);
        }
    }
}
