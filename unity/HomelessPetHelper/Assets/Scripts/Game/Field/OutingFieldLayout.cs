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
            public bool stagedRoutes, illustratedInterior, introduceWithCleanup;
            public PropVisual[] propVisuals;
            public int battleGoalCell;
            public GridData deployment;
            public int defenseGridSize, initialDefenseCell;
            public string initialTower, initialTowerEncounter;
            public float initialTowerRange;
            public int revisitMinutes;
            public int[] refillCells;
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
            public bool optional;
        }

        [Serializable]
        public sealed class PropVisual { public int cell; public string sprite; }

        private readonly Data _data;
        private readonly Dictionary<int, string> _props = new Dictionary<int, string>();
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
        public bool AllowsOptionalCleanup => _data.stagedRoutes;
        public bool IntroduceWithCleanup => _data.introduceWithCleanup;
        public bool IllustratedInterior => _data.illustratedInterior;
        public string PropAt(int cell, string fallback) => _props.TryGetValue(cell, out string key) ? key : fallback;
        public int RevisitMinutes => _data.revisitMinutes;
        public bool CanRefill(int cell) => _data.refillCells != null && Array.IndexOf(_data.refillCells, cell) >= 0;

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
                _data.routes == null || (!_data.stagedRoutes && _data.routes.Length != 3) ||
                (_data.stagedRoutes && (_data.routes.Length == 0 || _data.routes.Length > 3)))
                throw new ArgumentException("Invalid outing field frame.");
            if (_data.routes.Any(route => route == null || route.cells == null ||
                (!_data.stagedRoutes && route.optional)) || _data.routes.All(route => route.optional))
                throw new ArgumentException("Missing field route cells.");
            Opening = new ApproachCatalog(_data.approach,
                _data.routes.Where(route => !route.optional).SelectMany(route => route.cells).Distinct().ToArray());
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
            var cleanup = Opening.Get(ApproachMode.Cleanup);
            foreach (var prop in _data.propVisuals ?? Array.Empty<PropVisual>())
                if (prop == null || string.IsNullOrWhiteSpace(prop.sprite) ||
                    !cleanup.Debris.Any(debris => debris.Cell == prop.cell) || !_props.TryAdd(prop.cell, prop.sprite))
                    throw new ArgumentException("Invalid field prop visual.");
            if (_data.revisitMinutes < 0 || _data.revisitMinutes > 0 &&
                (_data.refillCells == null || _data.refillCells.Length == 0 ||
                 _data.refillCells.Distinct().Count() != _data.refillCells.Length ||
                 _data.refillCells.Any(cell => cell == cleanup.Animal || cleanup.RequiredClearCells.Contains(cell) ||
                    cleanup.PassageDestination(cell) >= 0 || !cleanup.Debris.Any(debris => debris.Cell == cell))))
                throw new ArgumentException("Invalid field revisit debris.");
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
            if (route.cells[0] != reference.Entrance || route.cells[route.cells.Length - 1] !=
                (_data.stagedRoutes ? _data.battleGoalCell : reference.Animal))
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

        private TextAsset _lastBattleSource;
        private SiegeCatalog _lastBattle;
        private bool[] _lastFloor;

        private bool FloorMatches(ApproachSession opening)
        {
            if (opening == null || _lastFloor == null) return opening == null && _lastFloor == null;
            for (int cell = 0; cell < _lastFloor.Length; cell++)
                if (_lastFloor[cell] != opening.IsPassable(cell)) return false;
            return true;
        }

        public SiegeCatalog CreateBattleCatalog(TextAsset source, ApproachSession opening = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_data.stagedRoutes)
            {
                if (opening != null) ValidateBattleClear(opening);
                if (_lastBattleSource == source && _lastBattle != null && FloorMatches(opening)) return _lastBattle;
            }
            // Never mutate generated content or its shared instances.
            var data = JsonUtility.FromJson<SiegeCatalogData>(source.text);
            data.map.width = Width;
            data.map.height = Height;
            if (_data.stagedRoutes)
            {
                data.map.lanes = _data.routes.Select(route => data.map.lanes.Single(lane => lane.id == route.laneId))
                    .ToArray();
                data.map.pads = data.map.pads.Where(pad => data.map.lanes.Any(lane => lane.id == pad.laneId))
                    .ToArray();
                if (_data.defenseGridSize > 0) ConfigureBattleFloor(data, opening);
            }
            if (data.map.lanes.Length != LaneCount) throw new ArgumentException("Field lane count mismatch.");
            for (int i = 0; i < LaneCount; i++)
                if (data.map.lanes[i].id != _data.routes[i].laneId)
                    throw new ArgumentException("Field lane order mismatch.");
            foreach (var lane in data.map.lanes)
            {
                var route = _data.routes.SingleOrDefault(item => item.laneId == lane.id);
                if (route == null) throw new ArgumentException("Field lane ID mismatch.");
                lane.nameKey = route.nameKey;
                if (_data.stagedRoutes) lane.open = !route.optional ||
                    (opening == null || IsClear(route, opening));
                lane.points = route.cells.Select(cell =>
                {
                    var point = CellPoint(cell);
                    return new SiegeCatalogData.PointDto { x = point.x, y = point.y };
                }).ToArray();
            }
            Vector2 animal = CellPoint(_data.stagedRoutes ? _data.battleGoalCell :
                Opening.Get(ApproachMode.Cleanup).Animal);
            foreach (var encounter in data.encounters)
            {
                encounter.holdOriginX = encounter.carryOriginX = animal.x;
                encounter.holdOriginY = encounter.carryOriginY = animal.y;
            }
            var catalog = new SiegeCatalog(data);
            if (_data.stagedRoutes)
            {
                _lastBattle = catalog;
                _lastBattleSource = source;
                _lastFloor = opening == null ? null : Enumerable.Range(0, CellCount).Select(opening.IsPassable).ToArray();
            }
            return catalog;
        }

        private void ConfigureBattleFloor(SiegeCatalogData data, ApproachSession opening)
        {
            var deployment = _data.deployment;
            data.map.customDeployment = true;
            data.map.deploymentMin = new SiegeCatalogData.PointDto { x = deployment.x, y = deployment.y };
            data.map.deploymentMax = new SiegeCatalogData.PointDto
                { x = deployment.x + deployment.width, y = deployment.y + deployment.height };
            int size = _data.defenseGridSize;
            data.rules.defenseGridSize = size;
            var cells = new List<int> { _data.initialDefenseCell };
            var template = Opening.Get(ApproachMode.Cleanup);
            var grid = Grid;
            for (int index = 0; index < size * size; index++)
            {
                if (index == _data.initialDefenseCell) continue;
                float x = (index % size + .5f) * Width / size;
                float y = (index / size + .5f) * Height / size;
                if (y < CellPoint(_data.battleGoalCell).y - 1.5f * grid.height / Rows ||
                    !grid.Contains(new Vector2(x, y))) continue;
                int cell = (int)((y - grid.y) / grid.height * Rows) * Columns +
                    (int)((x - grid.x) / grid.width * Columns);
                if (cell != _data.battleGoalCell && template.Terrain(cell) == '.' &&
                    (opening == null || opening.IsPassable(cell))) cells.Add(index);
            }
            data.map.defenseCells = cells.ToArray();
            data.map.initialDefenseCell = _data.initialDefenseCell;
            foreach (var pad in data.map.pads) pad.initial = false;
            data.map.pads.First(pad => pad.laneId == _data.routes[0].laneId).initial = true;
            var tower = data.towerTypes.Single(item => item.id == _data.initialTower);
            tower.range = _data.initialTowerRange;
            var encounter = data.encounters.Single(item => item.id == _data.initialTowerEncounter);
            encounter.initialTowerIds = new[] { tower.id };
        }

        public void ValidateBattleClear(ApproachSession session)
        {
            if (session == null || session.IsClosed || !session.IsComplete ||
                !ReferenceEquals(session.Template, Opening.Get(session.Template.Mode)))
                throw new InvalidOperationException("Opening is not the active completed field template.");
            foreach (var route in _data.routes)
                if (!route.optional && !IsClear(route, session))
                    throw new InvalidOperationException("Completed opening still blocks a required battle route.");
        }

        private static bool IsClear(Route route, ApproachSession session) =>
            route.cells.All(session.IsPassable);
    }
}
