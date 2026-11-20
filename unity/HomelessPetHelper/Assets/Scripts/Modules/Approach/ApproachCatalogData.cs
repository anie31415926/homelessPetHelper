using System;

namespace HomelessPetHelper.Modules.Approach
{
    /// <summary>Authoring input only. Catalogs validate and copy it before play.</summary>
    [Serializable]
    public sealed class ApproachCatalogData
    {
        public int schemaVersion;
        public int undoLimit;
        public double maxStepSeconds;
        public Template[] templates;

        [Serializable]
        public sealed class Template
        {
            public string id;
            public ApproachMode mode;
            public string[] rows;
            public int entrance;
            public int animal;
            public int[] bridgeCells;
            public int[] boxes;
            public Debris[] debris;
        }

        [Serializable]
        public sealed class Debris
        {
            public int cell;
            public ApproachTool tool;
            public double workSeconds;
        }
    }
}
