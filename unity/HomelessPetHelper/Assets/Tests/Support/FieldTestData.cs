using System;
using System.Linq;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using UnityEngine;

namespace HomelessPetHelper.Tests.Support
{
    public static class FieldTestData
    {
        // Preserve the known solvable routes while moving every index to a different rectangular grid.
        public static OutingFieldLayout.Data Resize(string json, int columns, int rows)
        {
            var data = JsonUtility.FromJson<OutingFieldLayout.Data>(json);
            int oldColumns = data.approach.templates[0].rows[0].Length;
            int Map(int cell) => cell / oldColumns * columns + cell % oldColumns;
            foreach (var template in data.approach.templates)
            {
                template.rows = template.rows.Select(row => row.PadRight(columns, '#'))
                    .Concat(Enumerable.Repeat(new string('#', columns), rows - template.rows.Length)).ToArray();
                template.entrance = Map(template.entrance); template.animal = Map(template.animal);
                template.boxes = template.boxes.Select(Map).ToArray();
                template.bridgeCells = template.bridgeCells.Select(Map).ToArray();
                foreach (var debris in template.debris) debris.cell = Map(debris.cell);
            }
            foreach (var route in data.routes)
            { route.cells = route.cells.Select(Map).ToArray(); route.portalCell = Map(route.portalCell); }
            return data;
        }
    }
}
