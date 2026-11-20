using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace HomelessPetHelper.Editor.Atlas
{
    public static class AtlasMeshAudit
    {
        public static string Hash(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var vertices = mesh.vertices;
                writer.Write(vertices.Length * 3);
                foreach (var value in vertices)
                {
                    WriteFloat(writer, value.x); WriteFloat(writer, value.y); WriteFloat(writer, value.z);
                }
                WriteUV(writer, mesh.uv);
                WriteUV(writer, mesh.uv2);
                int[] indices = mesh.triangles;
                writer.Write(indices.Length);
                foreach (int value in indices) writer.Write(value);
                writer.Flush();
                return HashBytes(stream.ToArray());
            }
        }

        private static void WriteUV(BinaryWriter writer, Vector2[] values)
        {
            writer.Write(values.Length * 2);
            foreach (var value in values) { WriteFloat(writer, value.x); WriteFloat(writer, value.y); }
        }

        private static void WriteFloat(BinaryWriter writer, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("Non-finite atlas mesh data.");
            writer.Write(value == 0 ? 0f : value);
        }

        internal static string HashBytes(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
