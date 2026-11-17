using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IgnisEscape.Optimization;

namespace IgnisEscape.Editor
{
    /// <summary>
    /// DATA LAYOUT denah gedung (Bagian 6): daftar zona (persegi panjang) + daftar bukaan pintu.
    /// Ubah denah cukup di sini tanpa menyentuh kode generator.
    /// Koridor ditulis selebar 2,0 m (as dinding ke as dinding) sehingga lebar BERSIH = 1,8 m
    /// setelah dinding 0,2 m (0,1 m menjorok ke tiap sisi).
    /// </summary>
    public static class BuildingLayoutData
    {
        public const float CellSize = 0.1f;
        public const float WallThickness = 0.2f;
        public const float CeilingHeight = 2.8f;     // Tinggi plafon 2,8 m
        public const float DoorwayHeight = 2.1f;     // Tinggi kusen 2,1 m
        public const float CorridorClearWidth = 1.8f; // Lebar koridor bersih 1,8 m

        public enum OpeningKind { Doorway, ExitDoor }

        [Serializable]
        public struct Zone
        {
            public string id;
            public string group; // Zona dengan grup sama tidak diberi dinding pemisah (mis. semua koridor)
            public float minX, minZ, maxX, maxZ;
            public bool isCorridor;

            public Vector3 Center => new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            public Vector3 Size => new Vector3(maxX - minX, CeilingHeight, maxZ - minZ);
        }

        [Serializable]
        public struct Opening
        {
            public string id;
            public bool lineIsX; // true: dinding pada garis x = line (memanjang sumbu Z)
            public float line;
            public float min;
            public float max;
            public OpeningKind kind;
        }

        // Batas grid (harus lebih luas dari semua zona) -> gedung ~44 m x 32 m
        public static readonly Vector2 FootprintMin = new Vector2(-7f, -5f);
        public static readonly Vector2 FootprintMax = new Vector2(40f, 30f);

        public static readonly Zone[] Zones =
        {
            Z("Zone1_Lobby", "Lobby", -5f, -4f, 5f, 4f),                         // 1. Lobi 10 x 8 m (spawn)
            Z("Zone2A_CorridorWest", "Corridor", -1f, 4f, 1f, 25f, true),        // 2. Koridor L - lengan barat
            Z("Zone2B_CorridorNorth", "Corridor", -1f, 23f, 26f, 25f, true),     // 2. Koridor L - lengan utara
            Z("Zone2C_CorridorEast", "Corridor", 24f, 7f, 26f, 25f, true),       // Koridor timur
            Z("Zone2D_CorridorExitA", "Corridor", 24f, 7f, 38f, 9f, true),       // Menuju Exit A
            Z("Zone2E_CorridorBranchB", "Corridor", 13f, 12f, 15f, 28f, true),   // Cabang tengah -> Exit B
            Z("Zone2F_CorridorWestBranch", "Corridor", -6f, 13f, -1f, 15f, true),// Cabang barat (buntu)
            Z("Zone3_OpenSpace", "OpenSpace", 1f, 7f, 13f, 17f),                 // 3. Open-space 12 x 10 m
            Z("Zone4_MeetingRoom", "Meeting", 15f, 12.5f, 21f, 17.5f),           // 4. Meeting 6 x 5 m
            Z("Zone5_Pantry", "Pantry", 26f, 18f, 31f, 22f),                     // 5. Pantry 5 x 4 m
            Z("Zone6_ServerRoom", "Server", 26f, 12f, 30f, 16f),                 // 6. Server 4 x 4 m
            Z("Zone7_ArchiveStorage", "Archive", -6f, 15f, -1f, 19f),            // 7. Gudang arsip (buntu)
            Z("Zone8_Restroom", "Restroom", -6f, 9f, -1f, 13f)                   // 8. Toilet (buntu)
        };

        public static readonly Opening[] Openings =
        {
            OZ("Lobby_To_CorridorWest", 4f, -1f, 1f, OpeningKind.Doorway),
            OX("OpenSpace_West", 1f, 11.2f, 12.8f, OpeningKind.Doorway),
            OX("OpenSpace_East", 13f, 13.2f, 14.8f, OpeningKind.Doorway),
            OX("Meeting_Door", 15f, 14.2f, 15.8f, OpeningKind.Doorway),
            OX("Pantry_Door", 26f, 19.2f, 20.8f, OpeningKind.Doorway),
            OX("Server_Door", 26f, 13.2f, 14.8f, OpeningKind.Doorway),
            OZ("Archive_Door", 15f, -4.2f, -2.6f, OpeningKind.Doorway),
            OZ("Restroom_Door", 13f, -4.2f, -2.6f, OpeningKind.Doorway),
            OX("ExitA", 38f, 7.2f, 8.8f, OpeningKind.ExitDoor),   // 9. Pintu Darurat A (timur)
            OZ("ExitB", 28f, 13.2f, 14.8f, OpeningKind.ExitDoor)  // 9. Pintu Darurat B (utara)
        };

        private static Zone Z(string id, string group, float minX, float minZ, float maxX, float maxZ, bool corridor = false)
        {
            return new Zone { id = id, group = group, minX = minX, minZ = minZ, maxX = maxX, maxZ = maxZ, isCorridor = corridor };
        }

        private static Opening OX(string id, float x, float zMin, float zMax, OpeningKind kind)
        {
            return new Opening { id = id, lineIsX = true, line = x, min = zMin, max = zMax, kind = kind };
        }

        private static Opening OZ(string id, float z, float xMin, float xMax, OpeningKind kind)
        {
            return new Opening { id = id, lineIsX = false, line = z, min = xMin, max = xMax, kind = kind };
        }
    }

    /// <summary>
    /// Generator cangkang gedung berbasis grid 0,1 m (Bagian 6 & 9):
    /// - Setiap batas antara dua grup zona berbeda (atau zona vs ruang kosong) OTOMATIS diberi dinding,
    ///   sehingga tidak ada dinding yang bolong.
    /// - Bukaan pintu hanya menyisakan lintel di atas 2,1 m.
    /// - Satu pelat lantai dan satu pelat plafon menutup seluruh gedung (tidak ada lantai bolong).
    /// - Dinding dikelompokkan per zona dengan MeshCombiner; tiap zona diberi OcclusionArea.
    /// </summary>
    public static class BuildingShellGenerator
    {
        public static MeshCombiner Build(Transform envRoot, Material matWall, Material matFloor, Material matCeiling)
        {
            float cell = BuildingLayoutData.CellSize;
            Vector2 o = BuildingLayoutData.FootprintMin;
            Vector2 max = BuildingLayoutData.FootprintMax;
            int nx = Mathf.RoundToInt((max.x - o.x) / cell);
            int nz = Mathf.RoundToInt((max.y - o.y) / cell);

            var zones = BuildingLayoutData.Zones;
            int[,] group = new int[nx, nz];
            int[,] owner = new int[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    owner[i, j] = -1;

            var groupIds = new Dictionary<string, int>();
            for (int k = 0; k < zones.Length; k++)
            {
                var z = zones[k];
                if (!groupIds.TryGetValue(z.group, out int gid))
                {
                    gid = groupIds.Count + 1;
                    groupIds[z.group] = gid;
                }

                int i0 = Mathf.RoundToInt((z.minX - o.x) / cell);
                int i1 = Mathf.RoundToInt((z.maxX - o.x) / cell);
                int j0 = Mathf.RoundToInt((z.minZ - o.y) / cell);
                int j1 = Mathf.RoundToInt((z.maxZ - o.y) / cell);
                for (int i = i0; i < i1; i++)
                {
                    for (int j = j0; j < j1; j++)
                    {
                        if (group[i, j] != 0) continue;
                        group[i, j] = gid;
                        owner[i, j] = k;
                    }
                }
            }

            // Pelat lantai & plafon tunggal (menutup seluruh gedung + area di balik pintu darurat)
            GameObject shell = new GameObject("BuildingShell_Grid");
            shell.transform.SetParent(envRoot, false);
            float w = max.x - o.x;
            float d = max.y - o.y;
            float h = BuildingLayoutData.CeilingHeight;
            CreateStaticBox("Building_FloorSlab", shell.transform,
                new Vector3(o.x + w * 0.5f, -0.05f, o.y + d * 0.5f), new Vector3(w, 0.1f, d), matFloor, "Floor");
            CreateStaticBox("Building_CeilingSlab", shell.transform,
                new Vector3(o.x + w * 0.5f, h + 0.05f, o.y + d * 0.5f), new Vector3(w, 0.1f, d), matCeiling, "Untagged");

            // Objek zona + OcclusionArea + grup dinding ber-MeshCombiner
            var wallGroups = new Transform[zones.Length];
            MeshCombiner first = null;
            for (int k = 0; k < zones.Length; k++)
            {
                var z = zones[k];
                GameObject zoneObj = new GameObject(z.id);
                zoneObj.transform.SetParent(envRoot, false);
                zoneObj.transform.position = z.Center;

                OcclusionArea occ = zoneObj.AddComponent<OcclusionArea>();
                occ.center = new Vector3(0f, h * 0.5f, 0f);
                occ.size = z.Size;

                GameObject wallGroup = new GameObject($"{z.id}_Walls");
                wallGroup.transform.SetParent(zoneObj.transform, false);
                MeshCombiner combiner = wallGroup.AddComponent<MeshCombiner>();
                if (first == null) first = combiner;
                wallGroups[k] = wallGroup.transform;
            }

            int wallCount = 0;

            // Dinding pada garis x = konstan (memanjang sumbu Z)
            for (int i = 1; i < nx; i++)
            {
                float lineX = o.x + i * cell;
                int runType = 0, runStart = 0, runOwner = -1;
                for (int j = 0; j <= nz; j++)
                {
                    int type = 0, own = -1;
                    if (j < nz)
                    {
                        int a = group[i - 1, j];
                        int b = group[i, j];
                        if (a != b)
                        {
                            float along = o.y + (j + 0.5f) * cell;
                            type = IsOpening(true, lineX, along) ? 2 : 1;
                            own = owner[i, j] >= 0 ? owner[i, j] : owner[i - 1, j];
                        }
                    }
                    if (type != runType)
                    {
                        if (runType != 0)
                        {
                            EmitRun(true, lineX, o.y + runStart * cell, o.y + j * cell, runType, wallGroups[runOwner], matWall, wallCount++);
                        }
                        runType = type;
                        runStart = j;
                        runOwner = own;
                    }
                }
            }

            // Dinding pada garis z = konstan (memanjang sumbu X)
            for (int j = 1; j < nz; j++)
            {
                float lineZ = o.y + j * cell;
                int runType = 0, runStart = 0, runOwner = -1;
                for (int i = 0; i <= nx; i++)
                {
                    int type = 0, own = -1;
                    if (i < nx)
                    {
                        int a = group[i, j - 1];
                        int b = group[i, j];
                        if (a != b)
                        {
                            float along = o.x + (i + 0.5f) * cell;
                            type = IsOpening(false, lineZ, along) ? 2 : 1;
                            own = owner[i, j] >= 0 ? owner[i, j] : owner[i, j - 1];
                        }
                    }
                    if (type != runType)
                    {
                        if (runType != 0)
                        {
                            EmitRun(false, lineZ, o.x + runStart * cell, o.x + i * cell, runType, wallGroups[runOwner], matWall, wallCount++);
                        }
                        runType = type;
                        runStart = i;
                        runOwner = own;
                    }
                }
            }

            Debug.Log($"[IgnisEscape] BuildingShellGenerator: {zones.Length} zona, {wallCount} segmen dinding/lintel tanpa celah.");
            return first;
        }

        private static bool IsOpening(bool lineIsX, float line, float along)
        {
            var openings = BuildingLayoutData.Openings;
            for (int i = 0; i < openings.Length; i++)
            {
                var op = openings[i];
                if (op.lineIsX != lineIsX) continue;
                if (Mathf.Abs(op.line - line) > 0.01f) continue;
                if (along > op.min && along < op.max) return true;
            }
            return false;
        }

        private static void EmitRun(bool lineIsX, float line, float s0, float s1, int type, Transform parent, Material mat, int index)
        {
            float t = BuildingLayoutData.WallThickness;
            float ceiling = BuildingLayoutData.CeilingHeight;
            float door = BuildingLayoutData.DoorwayHeight;

            float len = s1 - s0;
            float mid = (s0 + s1) * 0.5f;
            float height = type == 1 ? ceiling : (ceiling - door);
            float yCenter = type == 1 ? ceiling * 0.5f : door + height * 0.5f;

            // Diperpanjang setengah tebal dinding di tiap ujung agar sudut pertemuan tertutup rapat
            Vector3 pos = lineIsX ? new Vector3(line, yCenter, mid) : new Vector3(mid, yCenter, line);
            Vector3 size = lineIsX ? new Vector3(t, height, len + t) : new Vector3(len + t, height, t);
            string name = (type == 1 ? "Wall_" : "Lintel_") + index;
            CreateStaticBox(name, parent, pos, size, mat, "Wall");
        }

        private static GameObject CreateStaticBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, string tag)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.tag = tag;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}

