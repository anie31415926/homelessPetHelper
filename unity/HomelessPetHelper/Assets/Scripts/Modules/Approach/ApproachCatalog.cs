using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Approach
{
    public sealed class ApproachCatalog
    {
        // Resource safety bound, not a level size. Dimensions come from each template's rows.
        public const int MaxCells = 1024;
        private readonly ApproachTemplate[] _templates = new ApproachTemplate[2];
        public int UndoLimit { get; }
        public double MaxStepSeconds { get; }

        public ApproachCatalog(ApproachCatalogData data, int[] requiredClearCells = null)
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
                _templates[mode] = new ApproachTemplate(template, requiredClearCells);
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
                template.rows.Length == 0 || template.rows[0] == null || template.rows[0].Length == 0 ||
                (long)template.rows.Length * template.rows[0].Length > MaxCells || template.boxes == null ||
                template.bridgeCells == null || template.debris == null)
                throw new ArgumentException("Invalid approach template.");
            int columns = template.rows[0].Length, rows = template.rows.Length;
            foreach (string row in template.rows)
            {
                if (row == null || row.Length != columns) throw new ArgumentException("Invalid board size.");
                foreach (char tile in row)
                    if (tile != '#' && tile != '.' && tile != '~') throw new ArgumentException("Invalid terrain.");
            }
            bool Ground(int cell) => cell >= 0 && cell < columns * rows &&
                template.rows[cell / columns][cell % columns] == '.';
            if (!Ground(template.entrance) || !Ground(template.animal) || template.entrance == template.animal)
                throw new ArgumentException("Invalid approach endpoints.");
            if (template.passage != null && template.passage.Length != 0 &&
                (template.passage.Length != 2 || !Ground(template.passage[0]) ||
                 !Ground(template.passage[1]) || template.passage[0] == template.passage[1]))
                throw new ArgumentException("Invalid passage endpoints.");
            if (template.concealAnimal && template.mode == ApproachMode.Cleanup &&
                !Array.Exists(template.debris, item => item != null && item.cell == template.animal &&
                    item.tool != ApproachTool.Mud))
                throw new ArgumentException("Concealed animal must have a removable cover.");
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
                if (cell < 0 || cell >= columns * rows || !bridges.Add(cell) ||
                    template.rows[cell / columns][cell % columns] != '~')
                    throw new ArgumentException("Invalid bridge site.");
            if (template.mode == ApproachMode.Puzzle ?
                template.debris.Length != 0 || template.boxes.Length == 0 :
                template.boxes.Length != 0 || bridges.Count != 0 || template.debris.Length == 0)
                throw new ArgumentException("Content does not match approach mode.");
        }
    }
}
