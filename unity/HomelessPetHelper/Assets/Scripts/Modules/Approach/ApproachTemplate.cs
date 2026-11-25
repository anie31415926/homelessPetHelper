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
        public bool ConcealAnimal { get; }
        private readonly int _passageStart, _passageEnd;
        public int PassageDestination(int cell) => cell < 0 ? -1 : cell == _passageStart ? _passageEnd :
            cell == _passageEnd ? _passageStart : -1;
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
            ConcealAnimal = data.concealAnimal;
            _passageStart = data.passage?.Length == 2 ? data.passage[0] : -1;
            _passageEnd = data.passage?.Length == 2 ? data.passage[1] : -1;
            BridgeCells = Array.AsReadOnly((int[])data.bridgeCells.Clone());
            Boxes = Array.AsReadOnly((int[])data.boxes.Clone());
            var debris = new List<ApproachDebris>();
            foreach (var item in data.debris)
                debris.Add(new ApproachDebris(item.cell, item.tool, item.workSeconds));
            Debris = new ReadOnlyCollection<ApproachDebris>(debris);
        }
    }
}
