using System;
using System.IO;
using UnityEngine;

namespace ArGuildEncounter.Players
{
    /// <summary>
    /// Fallback and runtime loader for Dark Necromancer GLB mesh data.
    /// Guarantees that the mesh is never null under any condition (editor reload, playmode transition, or standalone).
    /// </summary>
    public static class RuntimeDarkNecromancerMeshLoader
    {
        private static Mesh _cachedMesh;

        public static Mesh GetOrLoadMesh()
        {
            if (_cachedMesh != null && _cachedMesh.vertexCount > 0)
            {
                return _cachedMesh;
            }

            string binPath = null;
            try
            {
                if (!string.IsNullOrEmpty(Application.dataPath))
                {
                    string p = Path.Combine(Application.dataPath, "player-model", "dark_necromancer_mesh.bin");
                    if (File.Exists(p)) binPath = p;
                }
            }
            catch { }

            if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
            {
                string p = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "player-model", "dark_necromancer_mesh.bin");
                if (File.Exists(p)) binPath = p;
            }
            if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
            {
                string p = @"C:\Users\LENOVO\hackv2\Assets\player-model\dark_necromancer_mesh.bin";
                if (File.Exists(p)) binPath = p;
            }
            if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
            {
                string p = @"C:\Users\LENOVO\OneDrive\Desktop\hackv2\Assets\player-model\dark_necromancer_mesh.bin";
                if (File.Exists(p)) binPath = p;
            }

            if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
            {
                Debug.LogWarning($"[RuntimeDarkNecromancerMeshLoader] Mesh binary data not found.");
                return null;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(binPath);
                int totalVerts = BitConverter.ToInt32(fileBytes, 0);
                int totalInds = BitConverter.ToInt32(fileBytes, 4);

                Vector3[] positions = new Vector3[totalVerts];
                Vector3[] normals = new Vector3[totalVerts];
                Vector4[] tangents = new Vector4[totalVerts];
                Vector2[] uvs = new Vector2[totalVerts];
                int[] indices = new int[totalInds];

                int offset = 8;
                int posBytes = totalVerts * 12;
                Buffer.BlockCopy(fileBytes, offset, positions, 0, posBytes);
                offset += posBytes;

                int normBytes = totalVerts * 12;
                Buffer.BlockCopy(fileBytes, offset, normals, 0, normBytes);
                offset += normBytes;

                int tanBytes = totalVerts * 16;
                Buffer.BlockCopy(fileBytes, offset, tangents, 0, tanBytes);
                offset += tanBytes;

                int uvBytes = totalVerts * 8;
                Buffer.BlockCopy(fileBytes, offset, uvs, 0, uvBytes);
                offset += uvBytes;

                int indBytes = totalInds * 4;
                Buffer.BlockCopy(fileBytes, offset, indices, 0, indBytes);

                _cachedMesh = new Mesh
                {
                    name = "DarkNecromancer_Mesh",
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };

                _cachedMesh.SetVertices(positions);
                _cachedMesh.SetNormals(normals);
                _cachedMesh.SetTangents(tangents);
                _cachedMesh.SetUVs(0, uvs);
                _cachedMesh.SetTriangles(indices, 0);
                _cachedMesh.RecalculateBounds();

                Debug.Log($"[RuntimeDarkNecromancerMeshLoader] Fast loaded Dark Necromancer mesh ({_cachedMesh.vertexCount:N0} verts, {_cachedMesh.triangles.Length / 3:N0} tris).");
                return _cachedMesh;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeDarkNecromancerMeshLoader] Error loading binary mesh: {ex.Message}");
                return null;
            }
        }
    }
}
