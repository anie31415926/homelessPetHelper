using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Approach
{
    public sealed class ApproachCatalog
    {
        // Matches the first static board prefab. Larger boards require an explicit UI revision.
        public const int BoardColumns = 7;
        public const int BoardRows = 8;
        private readonly ApproachTemplate[] _templates = new ApproachTemplate[2];
        public int UndoLimit { get; }
        public double MaxStepSeconds { get; }

        public ApproachCatalog(ApproachCatalogData data)
        {
            if (data == null || data.schemaVersion != 1 || data.templates == null ||
                data.templates.Length != _templates.Length || data.undoLimit <= 0 || data.undoLimit > 100 ||
                !FinitePositive(data.maxStepSeconds) || data.maxStepSeconds > 1)
                throw new ArgumentException("Invalid approach catalog.", nameof(data));
            UndoLimit = data.undoLimit;
            MaxStepSeconds = data.maxStepSeconds;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var template in data.templates)
            {
                Validate(template);
                int mode = (int)template.mode;
                if (!ids.Add(template.id) || _templates[mode] != null)
                    throw new ArgumentException("Duplicate approach template or mode.");
                _templates[mode] = new ApproachTemplate(template);
            }
        }

        public ApproachTemplate Get(ApproachMode mode)
        {
            if (!Enum.IsDefined(typeof(ApproachMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            return _templates[(int)mode];
        }

        public ApproachSession Create(Guid outingId, int seed)
        {
            if (seed < 0) throw new ArgumentOutOfRangeException(nameof(seed));
            return new ApproachSession(outingId, seed, _templates[new Random(seed).Next(_templates.Length)], this);
        }

        public static bool FinitePositive(double value) => value > 0 && !double.IsInfinity(value);

        private static void Validate(ApproachCatalogData.Template template)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.id) ||
                !Enum.IsDefined(typeof(ApproachMode), template.mode) || template.rows == null ||
                template.rows.Length != BoardRows || template.boxes == null ||
                template.bridgeCells == null || template.debris == null)
                throw new ArgumentException("Invalid approach template.");
            foreach (string row in template.rows)
            {
                if (row == null || row.Length != BoardColumns) throw new ArgumentException("Invalid board size.");
                foreach (char tile in row)
                    if (tile != '#' && tile != '.' && tile != '~') throw new ArgumentException("Invalid terrain.");
            }
            bool Ground(int cell) => cell >= 0 && cell < BoardColumns * BoardRows &&
                template.rows[cell / BoardColumns][cell % BoardColumns] == '.';
            if (!Ground(template.entrance) || !Ground(template.animal) || template.entrance == template.animal)
                throw new ArgumentException("Invalid approach endpoints.");
            var occupied = new HashSet<int>();
            foreach (int cell in template.boxes)
                if (!Ground(cell) || cell == template.entrance || cell == template.animal || !occupied.Add(cell))
                    throw new ArgumentException("Invalid crate.");
            foreach (var item in template.debris)
                if (item == null || !Ground(item.cell) || item.cell == template.entrance ||
                    !occupied.Add(item.cell) || item.tool < ApproachTool.Litter || item.tool > ApproachTool.Mud ||
                    !FinitePositive(item.workSeconds) || item.workSeconds > 60)
                    throw new ArgumentException("Invalid debris.");
            var bridges = new HashSet<int>();
            foreach (int cell in template.bridgeCells)
                if (cell < 0 || cell >= BoardColumns * BoardRows || !bridges.Add(cell) ||
                    template.rows[cell / BoardColumns][cell % BoardColumns] != '~')
                    throw new ArgumentException("Invalid bridge site.");
            if (template.mode == ApproachMode.Puzzle ?
                template.debris.Length != 0 || template.boxes.Length == 0 || bridges.Count == 0 :
                template.boxes.Length != 0 || bridges.Count != 0 || template.debris.Length == 0)
                throw new ArgumentException("Content does not match approach mode.");
        }
    }
}
