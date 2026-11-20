using System;

namespace HomelessPetHelper.Editor.PetMotion
{
    [Serializable]
    public sealed class PetMotionBakeData
    {
        public int schema;
        public int sampleRate;
        public Rig[] rigs;
        public Rig[] sources;
        public Take[] takes;

        [Serializable]
        public sealed class Rig
        {
            public string id, texture;
            public float[] positions, uv, cropUV, weights;
            public int[] triangles, indices;
            public Bone[] bones;
        }

        [Serializable]
        public sealed class Bone
        {
            public string id;
            public int parent;
            public float[] position;
        }

        [Serializable]
        public sealed class Take
        {
            public string id;
            public float duration;
            public Curve[] curves;
        }

        [Serializable]
        public sealed class Curve
        {
            public string path, property;
            public bool step;
            public float[] times, values;
        }
    }
}
