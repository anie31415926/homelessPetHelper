using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Siege;
using UnityEngine;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>One authored coordinate frame. Rule catalogs still own all game validation.</summary>
    public sealed class OutingFieldLayout
    {
        [Serializable]
        public sealed class Data
        {
            public int schemaVersion;
            public string pointId, layoutId;
            public bool introductory;
            public float width, height, animalWidth, catWidth;
            public GridData grid;
            public ApproachCatalogData approach;
            public Route[] routes;
        }

        [Serializable]
        public sealed class GridData
        {
            public float x, y, width, height;
        }

        [Serializable]
        public sealed class Route
        {
            public string laneId, nameKey;
            public int portalCell;
            public int[] cells;
        }

        private readonly Data _data;
        public ApproachCatalog Opening { get; }
        public string PointId => _data.pointId;
        public bool Introductory => _data.introductory;
        public string LayoutId { get; }
        public string ContentVersion { get; }
        public int Columns => Opening.Get(ApproachMode.Cleanup).Columns;
        public int Rows => Opening.Get(ApproachMode.Cleanup).Rows;
        public int CellCount => Columns * Rows;
        public float Width => _data.width;
        public float Height => _data.height;
        public float AnimalWidth => _data.animalWidth;
        public float AnimalWidthFor(bool dog) => dog ? _data.animalWidth : _data.catWidth;
        public Rect Grid => new Rect(_data.grid.x, _data.grid.y, _data.grid.width, _data.grid.height);
        public int LaneCount => _data.routes.Length;

        public OutingFieldLayout(TextAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _data = JsonUtility.FromJson<Data>(source.text);
            ContentVersion = Progress.FieldProgressContent.Hash(source.text);
            if (_data == null || _data.schemaVersion != 1 || string.IsNullOrWhiteSpace(_data.pointId) ||
                !Positive(_data.width) || !Positive(_data.height) || !Positive(_data.animalWidth) ||
                !Positive(_data.catWidth) || _data.catWidth > _data.animalWidth ||
                _data.animalWidth >= _data.width / 3 || _data.grid == null || !Positive(_data.grid.width) ||
                    !Positive(_data.grid.height) ||
                !Finite(_data.grid.x) || !Finite(_data.grid.y) || _data.grid.x < 0 || _data.grid.y < 0 ||
                    Grid.xMax > Width || Grid.yMax > Height ||
                _data.routes == null || _data.routes.Length != 3)
                throw new ArgumentException("Invalid outing field frame.");
            if (_data.routes.Any(route => route == null || route.cells == null))
                throw new ArgumentException("Missing field route cells.");
            Opening = new ApproachCatalog(_data.approach,
                _data.routes.SelectMany(route => route.cells).Distinct().ToArray());
            LayoutId = string.IsNullOrEmpty(_data.layoutId) ? PointId : _data.layoutId;
            if (string.IsNullOrWhiteSpace(LayoutId) || LayoutId != LayoutId.Trim() ||
                PointId != PointId.Trim()) throw new ArgumentException("Invalid field identity.");
            var puzzle = Opening.Get(ApproachMode.Puzzle);
            if (puzzle.Columns != Columns || puzzle.Rows != Rows)
                throw new ArgumentException("Opening variants must share field dimensions.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var portals = new HashSet<int>();
            foreach (var route in _data.routes)
            {
                if (route == null || string.IsNullOrWhiteSpace(route.laneId) ||
                    string.IsNullOrWhiteSpace(route.nameKey) ||
                    !ids.Add(route.laneId) ||
                    route.cells == null || route.cells.Length < 2 || !route.cells.Contains(route.portalCell) ||
                    !portals.Add(route.portalCell))
                    throw new ArgumentException("Invalid field route.");
                ValidateRoute(route);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => value > 0 && Finite(value);

        public Vector2 CellPoint(int cell)
        {
            if (cell < 0 || cell >= CellCount)
                throw new ArgumentOutOfRangeException(nameof(cell));
            return new Vector2(Grid.x + (cell % Columns + .5f) * Grid.width / Columns,
                Grid.y + (cell / Columns + .5f) * Grid.height / Rows);
        }

        public Vector2 Portal(int lane) => CellPoint(_data.routes[lane].portalCell);

        private void ValidateRoute(Route route)
        {
            var reference = Opening.Get(ApproachMode.Cleanup);
            if (route.cells[0] != reference.Entrance || route.cells[route.cells.Length - 1] != reference.Animal)
                throw new ArgumentException("Field routes must share the opening endpoints.");
            var visited = new HashSet<int>();
            var puzzle = Opening.Get(ApproachMode.Puzzle);
            for (int i = 0; i < route.cells.Length; i++)
            {
                int cell = route.cells[i];
                if (!visited.Add(cell) || !reference.Contains(cell) || reference.Terrain(cell) != '.')
                    throw new ArgumentException("Field route crosses a wall.");
                if (puzzle.Terrain(cell) == '#' ||
                    puzzle.Terrain(cell) == '~' && !puzzle.BridgeCells.Contains(cell))
                    throw new ArgumentException("Battle route cannot be opened in the puzzle variant.");
                if (i == 0) continue;
                int prior = route.cells[i - 1];
                int dx = Math.Abs(prior % reference.Columns - cell % reference.Columns);
                int dy = Math.Abs(prior / reference.Columns - cell / reference.Columns);
                if (dx + dy != 1) throw new ArgumentException("Field route must follow adjacent clear cells.");
            }
            if (puzzle.Entrance != reference.Entrance || puzzle.Animal != reference.Animal)
                throw new ArgumentException("Opening variants move the field endpoints.");
        }

        public SiegeCatalog CreateBattleCatalog(TextAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            // Derive a fresh catalog; never mutate generated content or its shared instances.
            var data = JsonUtility.FromJson<SiegeCatalogData>(source.text);
            data.map.width = Width;
            data.map.height = Height;
            if (data.map.lanes.Length != LaneCount) throw new ArgumentException("Field lane count mismatch.");
            for (int i = 0; i < LaneCount; i++)
                if (data.map.lanes[i].id != _data.routes[i].laneId)
                    throw new ArgumentException("Field lane order mismatch.");
            foreach (var lane in data.map.lanes)
            {
                var route = _data.routes.SingleOrDefault(item => item.laneId == lane.id);
                if (route == null) throw new ArgumentException("Field lane ID mismatch.");
                lane.nameKey = route.nameKey;
                lane.points = route.cells.Select(cell =>
                {
                    var point = CellPoint(cell);
                    return new SiegeCatalogData.PointDto { x = point.x, y = point.y };
                }).ToArray();
            }
            Vector2 animal = CellPoint(Opening.Get(ApproachMode.Cleanup).Animal);
            foreach (var encounter in data.encounters)
            {
                encounter.holdOriginX = encounter.carryOriginX = animal.x;
                encounter.holdOriginY = encounter.carryOriginY = animal.y;
            }
            return new SiegeCatalog(data);
        }

        public void ValidateBattleClear(ApproachSession session)
        {
            if (session == null || session.IsClosed || !session.IsComplete ||
                !ReferenceEquals(session.Template, Opening.Get(session.Template.Mode)))
                throw new InvalidOperationException("Opening is not the active completed field template.");
            foreach (var route in _data.routes)
                foreach (int cell in route.cells)
                {
                    int debris = session.DebrisAt(cell);
                    bool blocked = session.Template.Terrain(cell) == '#' || session.HasBox(cell) ||
                        (session.Template.Terrain(cell) == '~' && session.BridgeCell != cell) ||
                        (debris >= 0 && session.Template.Debris[debris].Tool != ApproachTool.Mud);
                    if (blocked) throw new InvalidOperationException("Completed opening still blocks a battle route.");
                }
        }
    }
}
