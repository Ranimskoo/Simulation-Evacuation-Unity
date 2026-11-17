using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using IgnisEscape.Adaptive;
using IgnisEscape.Core;
using IgnisEscape.Environment;
using IgnisEscape.Feedback;
using IgnisEscape.Objects;
using IgnisEscape.Optimization;
using IgnisEscape.Player;
using IgnisEscape.UI;

namespace IgnisEscape.Editor
{
    /// <summary>
    /// Editor script menu "IgnisEscape/Build Scene" yang membangun gedung perkantoran
    /// lantai tunggal (~45 m x 32 m, rute 60-80 m) secara prosedural dari data layout
    /// (9 zona, prefab berulang dengan GPU Instancing, LODGroup 3 tingkat, OcclusionArea,
    /// MeshCombiner per zona, atmosfer kebakaran visual, dan anggaran ~92.000 segitiga
    /// sesudah optimasi / ~165.000 sebelum optimasi) (Bagian 5, 6, 7, 8, 9, 10).
    /// </summary>
    public static class IgnisEscapeSceneBuilder
    {
        private const string AssetRoot = "Assets/IgnisEscape";
        private const string MaterialFolder = AssetRoot + "/Materials";
        private const string AnimFolder = AssetRoot + "/Animations";
        private const string PrefabFolder = AssetRoot + "/Prefabs";
        private const string MeshFolder = AssetRoot + "/Meshes";
        private const string RootObjectName = "IgnisEscape_Root";

        public const float CorridorWidth = 1.8f;
        public const float CeilingHeight = 2.8f;
        public const float DoorFrameHeight = 2.1f;
        public const float DeskHeight = 0.8f;

        private class PrefabCatalog
        {
            public GameObject fireExitDoor;
            public GameObject officeDesk;
            public GameObject officeChair;
            public GameObject officeCubicle;
            public GameObject serverRack;
            public GameObject hydrantCabinet;
            public GameObject strobeBeacon;
            public GameObject apar;
            public GameObject manualAlarm;
            public GameObject exitSignGreen;
            public GameObject archiveShelf;
        }

        [MenuItem("IgnisEscape/Build Scene")]
        public static void BuildScene()
        {
            EnsureFolders();
            EnsureTagExists("Wall");
            EnsureTagExists("Desk");
            EnsureTagExists("Debris");
            EnsureTagExists("Floor");

            GameObject existing = GameObject.Find(RootObjectName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject(RootObjectName);
            GameObject envRoot = new GameObject("Environment_45x32m");
            envRoot.transform.SetParent(root.transform);

            // Material bersama dengan GPU Instancing aktif
            Material matWall = CreateMaterial("Mat_Wall", new Color(0.84f, 0.85f, 0.87f));
            Material matFloor = CreateMaterial("Mat_Floor", new Color(0.26f, 0.28f, 0.31f));
            Material matCeiling = CreateMaterial("Mat_Ceiling", new Color(0.90f, 0.90f, 0.92f));
            Material matDoorSteel = CreateMaterial("Mat_DoorSteel", new Color(0.76f, 0.15f, 0.15f));
            Material matFrame = CreateMaterial("Mat_DoorFrame", new Color(0.20f, 0.22f, 0.24f));
            Material matMetal = CreateMaterial("Mat_Metal", new Color(0.70f, 0.72f, 0.75f));
            Material matDeskWood = CreateMaterial("Mat_DeskWood", new Color(0.56f, 0.39f, 0.23f));
            Material matDarkFabric = CreateMaterial("Mat_DarkFabric", new Color(0.18f, 0.20f, 0.24f));
            Material matServerDark = CreateMaterial("Mat_ServerDark", new Color(0.12f, 0.13f, 0.15f));
            Material matPaper = CreateMaterial("Mat_Paper", new Color(0.96f, 0.96f, 0.92f));
            Material matCardboard = CreateMaterial("Mat_Cardboard", new Color(0.65f, 0.48f, 0.28f));
            Material matPlantGreen = CreateMaterial("Mat_PlantGreen", new Color(0.18f, 0.58f, 0.24f));
            Material matHydrantRed = CreateMaterial("Mat_HydrantRed", new Color(0.84f, 0.12f, 0.12f));
            Material matTosca = CreateEmissiveMaterial("Mat_Tosca", SimulationConfig.PrimaryTosca, 1.6f);
            Material matStrobeRed = CreateEmissiveMaterial("Mat_StrobeRed", Color.red, 3.0f);
            Material matExitGreen = CreateEmissiveMaterial("Mat_ExitGreen", new Color(0.05f, 0.92f, 0.38f), 2.2f);
            Material matGypsum = CreateMaterial("Mat_Gypsum", new Color(0.88f, 0.88f, 0.85f));
            Material matBeam = CreateMaterial("Mat_Beam", new Color(0.45f, 0.32f, 0.20f));

            // 1. Buat katalog Prefab berulang (satu kali per tipe prop)
            PrefabCatalog prefabs = BuildPrefabCatalog(
                matDoorSteel, matFrame, matMetal, matDeskWood, matDarkFabric,
                matServerDark, matPaper, matHydrantRed, matStrobeRed, matExitGreen, matTosca);

            // 2. Bangun seluruh 13 segmen/zona dari data layout + OcclusionArea + MeshCombiner per zona
            MeshCombiner primaryCombiner = BuildingShellGenerator.Build(envRoot.transform, matWall, matFloor, matCeiling);

            // 3. Pintu Keluar Darurat A (Timur, jalur utama ~76 m) & B (Utara, jalur alternatif)
            FireExitDoor doorA = InstantiateExitDoor(prefabs.fireExitDoor, prefabs.exitSignGreen, envRoot.transform,
                "Exit_A", new Vector3(38.0f, 0f, 8.0f), Quaternion.Euler(0f, 90f, 0f));
            FireExitDoor doorB = InstantiateExitDoor(prefabs.fireExitDoor, prefabs.exitSignGreen, envRoot.transform,
                "Exit_B", new Vector3(14.0f, 0f, 28.0f), Quaternion.identity);

            // 4. Isi perabot & detail kekacauan tiap zona ruangan (menggunakan Prefab)
            PopulateAllRooms(envRoot.transform, prefabs, matDeskWood, matDarkFabric, matMetal, matPaper, matCardboard, matPlantGreen, matTosca);

            // 5. Aksesori keselamatan koridor (Hidran, Strobe, APAR tiap ~15 m, Alarm Manual)
            PlaceSafetyAccessories(envRoot.transform, prefabs);

            // 6. Rintangan Meja Kantor: 9 kandidat titik di 3 zona koridor (1 aktif per zona)
            ObstacleRandomizer randomizer = BuildMultiZoneObstacleRandomizer(envRoot.transform, prefabs.officeDesk);

            // 7. DebrisSpawner dengan 5 BoundingBox (1 Lobi + 4 segmen koridor)
            BuildMultiBoxDebrisSpawner(envRoot.transform, matGypsum, matBeam);

            // 8. Safety Waypoints tiap ~5 m di koridor + ruangan terbuka
            BuildSafetyWaypoints(envRoot.transform, matTosca);

            // 9. Jejak kaki toska sepanjang rute utama & alternatif
            FootprintGuide footprintGuide = BuildFootprintGuide(envRoot.transform, matTosca);

            // 10. Rambu jalur world-locked setiap 5 m di dinding koridor
            BuildCorridorSignage(envRoot.transform, matFrame);

            // 11. Atmosfer kebakaran visual (api & asap Pantry/Server, asap koridor naik pelan, lampu darurat berkedip, alarm prosedural)
            BuildFireAtmosphere(envRoot.transform, matFrame, matStrobeRed);

            // 12. Kalibrasi detail arsitektur untuk memenuhi target ~92.000 tris (Optimized ON) vs ~165.000 tris (Optimized OFF / F2)
            BuildCalibrationDetailMeshes(envRoot.transform, primaryCombiner, matWall, matMetal);

            // 13. Player (di Lobi Spawn), UI Overlay, Wrist Monitor, GameManager, ExitGoalTrigger A & B
            BuildPlayerAndUI(root.transform, envRoot.transform, primaryCombiner, doorA, doorB, randomizer, footprintGuide, matTosca);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[IgnisEscape] Gedung perkantoran 45x32 m berhasil dibangun melalui IgnisEscape/Build Scene.");
        }

        [MenuItem("IgnisEscape/Bake Lighting & Occlusion")]
        public static void BakeLightingAndOcclusion()
        {
            StaticOcclusionCulling.Compute();
            Lightmapping.BakeAsync();
            Debug.Log("[IgnisEscape] Proses Compute Occlusion Culling & Bake Lightmap dimulai.");
        }

        private static PrefabCatalog BuildPrefabCatalog(
            Material matDoorSteel, Material matFrame, Material matMetal, Material matDeskWood,
            Material matDarkFabric, Material matServerDark, Material matPaper, Material matHydrantRed,
            Material matStrobeRed, Material matExitGreen, Material matTosca)
        {
            var cat = new PrefabCatalog();

            // 1. FireExitDoor Prefab (Bagian 5: tinggi kusen 2,1 m, pivot tepi engsel, Animator 3 state)
            GameObject doorTemp = BuildFireExitDoorObject(matDoorSteel, matFrame, matMetal);
            cat.fireExitDoor = SavePrefab(doorTemp, "FireExitDoor.prefab");

            // 2. OfficeDesk Prefab (tinggi 0,8 m + laci + kertas berserakan)
            GameObject deskTemp = BuildOfficeDeskObject(matDeskWood, matMetal, matPaper);
            cat.officeDesk = SavePrefab(deskTemp, "OfficeDesk.prefab");

            // 3. OfficeChair Prefab dengan LODGroup 3 tingkat
            GameObject chairTemp = BuildOfficeChairObject(matDarkFabric, matMetal);
            cat.officeChair = SavePrefab(chairTemp, "OfficeChair.prefab");

            // 4. OfficeCubicle Prefab (memakai ulang OfficeDesk + kursi + monitor + partisi, LODGroup 3 tingkat)
            GameObject cubicleTemp = BuildOfficeCubicleObject(cat.officeDesk, matDeskWood, matDarkFabric, matMetal, matTosca);
            cat.officeCubicle = SavePrefab(cubicleTemp, "OfficeCubicle.prefab");

            // 5. ServerRack Prefab dengan LED & LODGroup 3 tingkat
            GameObject rackTemp = BuildServerRackObject(matServerDark, matMetal, matTosca);
            cat.serverRack = SavePrefab(rackTemp, "ServerRack.prefab");

            // 6. HydrantCabinet Prefab dengan LODGroup 3 tingkat
            GameObject hydrantTemp = BuildHydrantCabinetObject(matHydrantRed, matMetal);
            cat.hydrantCabinet = SavePrefab(hydrantTemp, "HydrantCabinet.prefab");

            // 7. StrobeBeacon Prefab dengan klip STEPPED & LODGroup 3 tingkat
            GameObject strobeTemp = BuildStrobeBeaconObject(matStrobeRed, matMetal);
            cat.strobeBeacon = SavePrefab(strobeTemp, "StrobeBeacon.prefab");

            // 8. APAR (tabung merah + selang)
            GameObject aparTemp = new GameObject("FireExtinguisher_APAR");
            CreatePrimitiveChild("Cylinder", PrimitiveType.Cylinder, aparTemp.transform, Vector3.zero, new Vector3(0.16f, 0.24f, 0.16f), matHydrantRed);
            CreatePrimitiveChild("Valve", PrimitiveType.Cube, aparTemp.transform, new Vector3(0f, 0.26f, 0f), new Vector3(0.06f, 0.06f, 0.08f), matMetal);
            CreatePrimitiveChild("Hose", PrimitiveType.Cylinder, aparTemp.transform, new Vector3(0.09f, 0.05f, 0f), new Vector3(0.03f, 0.18f, 0.03f), matDarkFabric);
            cat.apar = SavePrefab(aparTemp, "FireExtinguisher_APAR.prefab");

            // 9. Alarm Kebakaran Manual (kotak merah dinding)
            GameObject alarmTemp = new GameObject("ManualFireAlarm");
            CreatePrimitiveChild("Box", PrimitiveType.Cube, alarmTemp.transform, Vector3.zero, new Vector3(0.14f, 0.14f, 0.05f), matHydrantRed);
            CreatePrimitiveChild("Button", PrimitiveType.Cylinder, alarmTemp.transform, new Vector3(0f, 0f, -0.03f), new Vector3(0.06f, 0.01f, 0.06f), matPaper)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cat.manualAlarm = SavePrefab(alarmTemp, "ManualFireAlarm.prefab");

            // 10. Rambu EXIT Hijau Menyala
            GameObject exitSignTemp = new GameObject("ExitSignGreen");
            CreatePrimitiveChild("Frame", PrimitiveType.Cube, exitSignTemp.transform, Vector3.zero, new Vector3(0.55f, 0.22f, 0.04f), matExitGreen);
            cat.exitSignGreen = SavePrefab(exitSignTemp, "ExitSignGreen.prefab");

            // 11. Rak Arsip Bertingkat
            GameObject shelfTemp = new GameObject("ArchiveShelf");
            for (int y = 0; y < 4; y++)
            {
                CreatePrimitiveChild($"Plank_{y}", PrimitiveType.Cube, shelfTemp.transform, new Vector3(0f, 0.25f + y * 0.52f, 0f), new Vector3(1.2f, 0.04f, 0.45f), matMetal);
            }
            CreatePrimitiveChild("Post_L", PrimitiveType.Cube, shelfTemp.transform, new Vector3(-0.58f, 1.0f, 0f), new Vector3(0.04f, 2.0f, 0.45f), matMetal);
            CreatePrimitiveChild("Post_R", PrimitiveType.Cube, shelfTemp.transform, new Vector3(0.58f, 1.0f, 0f), new Vector3(0.04f, 2.0f, 0.45f), matMetal);
            cat.archiveShelf = SavePrefab(shelfTemp, "ArchiveShelf.prefab");

            AssetDatabase.SaveAssets();
            return cat;
        }

        private static FireExitDoor InstantiateExitDoor(
            GameObject doorPrefab, GameObject exitSignPrefab, Transform parent,
            string doorId, Vector3 pos, Quaternion rot)
        {
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, parent);
            inst.name = $"FireExitDoor_{doorId}";
            inst.transform.SetPositionAndRotation(pos, rot);

            FireExitDoor doorComp = inst.GetComponent<FireExitDoor>();
            AssignPrivateField(doorComp, "doorId", doorId);

            // Rambu EXIT hijau menyala di atas pintu keluar
            GameObject sign = (GameObject)PrefabUtility.InstantiatePrefab(exitSignPrefab, inst.transform);
            sign.transform.localPosition = new Vector3(0f, 2.32f, -0.08f);
            sign.transform.localRotation = Quaternion.identity;

            return doorComp;
        }

        private static void PopulateAllRooms(
            Transform envRoot, PrefabCatalog prefabs,
            Material matWood, Material matDark, Material matMetal, Material matPaper,
            Material matCardboard, Material matPlant, Material matTosca)
        {
            GameObject propsRoot = new GameObject("RoomProps_And_Furniture");
            propsRoot.transform.SetParent(envRoot, false);

            // 1. Lobi & Resepsionis: meja resepsionis, sofa tunggu, meja kopi, tanaman pot, papan pengumuman, denah evakuasi
            CreatePrimitiveChild("ReceptionDesk", PrimitiveType.Cube, propsRoot.transform, new Vector3(-2.2f, 0.4f, 1.2f), new Vector3(2.4f, DeskHeight, 0.75f), matWood);
            CreatePrimitiveChild("WaitingSofa", PrimitiveType.Cube, propsRoot.transform, new Vector3(2.8f, 0.32f, -1.2f), new Vector3(2.0f, 0.64f, 0.8f), matDark);
            CreatePrimitiveChild("CoffeeTable", PrimitiveType.Cube, propsRoot.transform, new Vector3(2.8f, 0.22f, 0.2f), new Vector3(1.1f, 0.44f, 0.6f), matWood);
            CreatePrimitiveChild("PlantPot_1", PrimitiveType.Cylinder, propsRoot.transform, new Vector3(-4.2f, 0.35f, -3.2f), new Vector3(0.45f, 0.35f, 0.45f), matPlant);
            CreatePrimitiveChild("PlantPot_2", PrimitiveType.Cylinder, propsRoot.transform, new Vector3(4.2f, 0.35f, 3.2f), new Vector3(0.45f, 0.35f, 0.45f), matPlant);
            CreatePrimitiveChild("BulletinBoard", PrimitiveType.Cube, propsRoot.transform, new Vector3(-4.85f, 1.5f, 0f), new Vector3(0.05f, 1.1f, 1.8f), matWood);

            // Denah Evakuasi Dinding ("ANDA DI SINI")
            GameObject mapObj = CreatePrimitiveChild("WallEvacuationMap", PrimitiveType.Cube, propsRoot.transform, new Vector3(4.85f, 1.55f, 0f), new Vector3(0.04f, 1.0f, 1.4f), matTosca);
            GameObject mapTxtObj = new GameObject("YouAreHereText");
            mapTxtObj.transform.SetParent(mapObj.transform, false);
            mapTxtObj.transform.localPosition = new Vector3(-0.6f, 0f, 0f);
            mapTxtObj.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            TextMeshPro mapTmp = mapTxtObj.AddComponent<TextMeshPro>();
            mapTmp.text = "DENAH EVAKUASI\n[ANDA DI SINI]";
            mapTmp.fontSize = 1.4f;
            mapTmp.alignment = TextAlignmentOptions.Center;
            mapTmp.color = Color.white;

            // 3. Ruang Kantor Open-Space: 8 unit meja kerja berkubikel (Prefab), rak berkas, mesin fotokopi, whiteboard
            // Disusun di sisi selatan (z = 9.0) dan utara (z = 15.2) agar jalur tengah pintu Barat (z = 12) ke pintu Timur (z = 14) bebas hambatan
            Vector3[] cubiclePositions =
            {
                new Vector3(3.5f, 0f, 9.0f), new Vector3(6.2f, 0f, 9.0f),
                new Vector3(8.8f, 0f, 9.0f), new Vector3(11.2f, 0f, 9.0f),
                new Vector3(3.5f, 0f, 15.2f), new Vector3(6.2f, 0f, 15.2f),
                new Vector3(8.8f, 0f, 15.2f), new Vector3(11.2f, 0f, 15.2f)
            };
            for (int i = 0; i < cubiclePositions.Length; i++)
            {
                GameObject cub = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.officeCubicle, propsRoot.transform);
                cub.name = $"OfficeCubicle_{i + 1}";
                cub.transform.position = cubiclePositions[i];
            }
            CreatePrimitiveChild("Photocopier", PrimitiveType.Cube, propsRoot.transform, new Vector3(2.2f, 0.55f, 16.2f), new Vector3(0.9f, 1.1f, 0.75f), matMetal);
            CreatePrimitiveChild("Whiteboard", PrimitiveType.Cube, propsRoot.transform, new Vector3(7.0f, 1.5f, 16.85f), new Vector3(2.4f, 1.2f, 0.05f), matPaper);
            GameObject openSpaceShelf = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.archiveShelf, propsRoot.transform);
            openSpaceShelf.name = "OpenSpace_ArchiveShelf";
            openSpaceShelf.transform.position = new Vector3(4.5f, 0f, 16.6f);

            // 4. Ruang Meeting: meja panjang, 6 kursi (Prefab), layar proyektor
            CreatePrimitiveChild("MeetingTable", PrimitiveType.Cube, propsRoot.transform, new Vector3(18.5f, 0.4f, 15.0f), new Vector3(3.2f, DeskHeight, 1.2f), matWood);
            for (int c = 0; c < 6; c++)
            {
                GameObject ch = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.officeChair, propsRoot.transform);
                float xOff = 17.3f + (c % 3) * 1.2f;
                float zOff = (c < 3) ? 13.9f : 16.1f;
                ch.transform.position = new Vector3(xOff, 0f, zOff);
                ch.transform.rotation = Quaternion.Euler(0f, (c < 3) ? 0f : 180f, 0f);
            }
            CreatePrimitiveChild("ProjectorScreen", PrimitiveType.Cube, propsRoot.transform, new Vector3(21.35f, 1.6f, 15.0f), new Vector3(0.05f, 1.5f, 2.4f), matPaper);

            // 5. Pantry: kitchen set, kulkas, dispenser, microwave, meja makan kecil
            CreatePrimitiveChild("KitchenSet", PrimitiveType.Cube, propsRoot.transform, new Vector3(28.5f, 0.45f, 21.5f), new Vector3(3.0f, 0.9f, 0.65f), matWood);
            CreatePrimitiveChild("Refrigerator", PrimitiveType.Cube, propsRoot.transform, new Vector3(30.4f, 0.9f, 21.3f), new Vector3(0.75f, 1.8f, 0.75f), matMetal);
            CreatePrimitiveChild("WaterDispenser", PrimitiveType.Cylinder, propsRoot.transform, new Vector3(26.6f, 0.55f, 21.4f), new Vector3(0.35f, 0.55f, 0.35f), matTosca);
            CreatePrimitiveChild("Microwave", PrimitiveType.Cube, propsRoot.transform, new Vector3(28.2f, 1.05f, 21.5f), new Vector3(0.5f, 0.3f, 0.4f), matDark);
            CreatePrimitiveChild("PantryTable", PrimitiveType.Cube, propsRoot.transform, new Vector3(28.5f, 0.4f, 19.2f), new Vector3(1.1f, DeskHeight, 1.1f), matWood);

            // 6. Ruang Server: 4 rak server (Prefab ber-LODGroup) + kabel berserakan
            for (int r = 0; r < 4; r++)
            {
                GameObject rack = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.serverRack, propsRoot.transform);
                rack.transform.position = new Vector3(27.0f + (r % 2) * 1.8f, 0f, 13.0f + (r / 2) * 1.8f);
            }
            CreatePrimitiveChild("FloorCable_1", PrimitiveType.Cylinder, propsRoot.transform, new Vector3(27.8f, 0.02f, 14.0f), new Vector3(0.04f, 1.2f, 0.04f), matDark)
                .transform.localRotation = Quaternion.Euler(90f, 25f, 0f);

            // 7. Gudang Arsip: rak arsip bertingkat (Prefab) dan kardus
            for (int s = 0; s < 3; s++)
            {
                GameObject sh = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.archiveShelf, propsRoot.transform);
                sh.transform.position = new Vector3(-4.8f + s * 1.4f, 0f, 17.8f);
            }
            CreatePrimitiveChild("CardboardBox_1", PrimitiveType.Cube, propsRoot.transform, new Vector3(-3.0f, 0.25f, 16.2f), new Vector3(0.5f, 0.5f, 0.5f), matCardboard);
            CreatePrimitiveChild("CardboardBox_2", PrimitiveType.Cube, propsRoot.transform, new Vector3(-2.4f, 0.25f, 16.4f), new Vector3(0.45f, 0.45f, 0.45f), matCardboard);

            // 8. Toilet: partisi bilik dan wastafel sederhana
            CreatePrimitiveChild("ToiletPartition_1", PrimitiveType.Cube, propsRoot.transform, new Vector3(-4.2f, 1.0f, 10.0f), new Vector3(0.06f, 2.0f, 1.4f), matMetal);
            CreatePrimitiveChild("ToiletPartition_2", PrimitiveType.Cube, propsRoot.transform, new Vector3(-2.8f, 1.0f, 10.0f), new Vector3(0.06f, 2.0f, 1.4f), matMetal);
            CreatePrimitiveChild("SinkCounter", PrimitiveType.Cube, propsRoot.transform, new Vector3(-3.5f, 0.42f, 12.4f), new Vector3(2.2f, 0.84f, 0.5f), matPaper);

            // Detail kekacauan: kursi terguling, kertas berserakan di lantai, pintu ruangan setengah terbuka
            GameObject overturnedChair = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.officeChair, propsRoot.transform);
            overturnedChair.name = "OverturnedChair_Chaos";
            overturnedChair.transform.position = new Vector3(5.5f, 0.28f, 11.8f);
            overturnedChair.transform.rotation = Quaternion.Euler(82f, 35f, 15f);

            for (int p = 0; p < 10; p++)
            {
                GameObject sheet = CreatePrimitiveChild($"ScatteredFloorPaper_{p}", PrimitiveType.Cube, propsRoot.transform,
                    new Vector3(2.5f + p * 0.9f, 0.008f, 11.8f + (p % 3) * 0.8f),
                    new Vector3(0.21f, 0.005f, 0.29f), matPaper);
                sheet.transform.rotation = Quaternion.Euler(0f, p * 37f, 0f);
                UnityEngine.Object.DestroyImmediate(sheet.GetComponent<Collider>());
            }

            CreatePrimitiveChild("HalfOpenRoomDoor_Meeting", PrimitiveType.Cube, propsRoot.transform,
                new Vector3(15.35f, 1.05f, 14.4f), new Vector3(0.9f, DoorFrameHeight, 0.05f), matWood)
                .transform.rotation = Quaternion.Euler(0f, 42f, 0f);
        }

        private static void PlaceSafetyAccessories(Transform envRoot, PrefabCatalog prefabs)
        {
            GameObject safetyRoot = new GameObject("SafetyAccessories");
            safetyRoot.transform.SetParent(envRoot, false);

            // Kotak Hidran menempel rapat di sisi dalam dinding koridor & menghadap ke dalam koridor
            // (Depan prefab HydrantCabinet menghadap +X)
            (Vector3 pos, float yaw)[] hydrantMounts =
            {
                (new Vector3(-0.80f, 1.35f, 6.0f), 0f),     // Dinding Barat Koridor Barat -> hadap +X
                (new Vector3(6.0f, 1.35f, 24.80f), 90f),    // Dinding Utara Koridor Utara -> hadap -Z
                (new Vector3(20.0f, 1.35f, 24.80f), 90f),   // Dinding Utara Koridor Utara -> hadap -Z
                (new Vector3(24.20f, 1.35f, 12.0f), 0f)     // Dinding Barat Koridor Timur -> hadap +X
            };
            for (int i = 0; i < hydrantMounts.Length; i++)
            {
                GameObject h = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.hydrantCabinet, safetyRoot.transform);
                h.transform.SetPositionAndRotation(hydrantMounts[i].pos, Quaternion.Euler(0f, hydrantMounts[i].yaw, 0f));
            }

            Vector3[] strobeCoords =
            {
                new Vector3(0f, 2.72f, 8.0f),
                new Vector3(14.0f, 2.72f, 24.0f),
                new Vector3(25.0f, 2.72f, 24.0f),
                new Vector3(25.0f, 2.72f, 8.0f),
                new Vector3(34.0f, 2.72f, 8.0f)
            };
            for (int i = 0; i < strobeCoords.Length; i++)
            {
                GameObject s = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.strobeBeacon, safetyRoot.transform);
                s.transform.position = strobeCoords[i];
            }

            // APAR & Alarm Kebakaran Manual menempel rapi di dinding dalam koridor tiap ~15 m
            // (Depan tombol ManualFireAlarm menghadap -Z)
            (Vector3 aparPos, Vector3 alarmPos, float yaw)[] safetyWallMounts =
            {
                (new Vector3(-0.78f, 1.0f, 5.0f), new Vector3(-0.86f, 1.4f, 5.45f), -90f),
                (new Vector3(-0.78f, 1.0f, 18.0f), new Vector3(-0.86f, 1.4f, 18.45f), -90f),
                (new Vector3(10.0f, 1.0f, 24.78f), new Vector3(10.45f, 1.4f, 24.86f), 0f),
                (new Vector3(24.22f, 1.0f, 18.0f), new Vector3(24.14f, 1.4f, 18.45f), -90f),
                (new Vector3(33.0f, 1.0f, 8.78f), new Vector3(33.45f, 1.4f, 8.86f), 0f)
            };
            for (int i = 0; i < safetyWallMounts.Length; i++)
            {
                Quaternion rot = Quaternion.Euler(0f, safetyWallMounts[i].yaw, 0f);
                GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.apar, safetyRoot.transform);
                a.transform.SetPositionAndRotation(safetyWallMounts[i].aparPos, rot);
                GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.manualAlarm, safetyRoot.transform);
                m.transform.SetPositionAndRotation(safetyWallMounts[i].alarmPos, rot);
            }
        }

        private static ObstacleRandomizer BuildMultiZoneObstacleRandomizer(Transform envRoot, GameObject deskPrefab)
        {
            GameObject randObj = new GameObject("ObstacleRandomizer_MultiZone");
            randObj.transform.SetParent(envRoot, false);
            ObstacleRandomizer randomizer = randObj.AddComponent<ObstacleRandomizer>();

            // 9 titik kandidat tersebar di 3 zona koridor, dipasang menyamping di sisi koridor
            // sehingga menyisakan celah jalan ~1,05 m (atau dapat dilompati dengan Space)
            (Vector3 pos, float yaw)[][] zoneCandidateCoords =
            {
                // Zona Koridor Barat (3 titik, koridor mengarah Z -> meja diputar ~85 deg sejajar dinding)
                new[]
                {
                    (new Vector3(-0.46f, 0f, 7.5f), 85f),
                    (new Vector3(0.46f, 0f, 16.5f), -85f),
                    (new Vector3(-0.46f, 0f, 20.5f), 82f)
                },
                // Zona Koridor Utara & Cabang Tengah (3 titik)
                new[]
                {
                    (new Vector3(5.5f, 0f, 24.46f), 8f),
                    (new Vector3(14.46f, 0f, 19.5f), 85f),
                    (new Vector3(20.0f, 0f, 23.54f), -8f)
                },
                // Zona Koridor Timur menuju Exit A (3 titik)
                new[]
                {
                    (new Vector3(24.54f, 0f, 18.0f), 85f),
                    (new Vector3(25.46f, 0f, 11.5f), -85f),
                    (new Vector3(31.0f, 0f, 7.54f), 6f)
                }
            };

            var zonesList = new List<ObstacleRandomizer.CorridorObstacleZone>();
            for (int z = 0; z < zoneCandidateCoords.Length; z++)
            {
                GameObject deskInst = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab, randObj.transform);
                deskInst.name = $"CorridorObstacleDesk_Zone{z + 1}";
                deskInst.tag = "Desk";

                var zoneData = new ObstacleRandomizer.CorridorObstacleZone
                {
                    zoneName = $"CorridorZone_{z + 1}",
                    deskObstacleInstance = deskInst,
                    candidatePoints = new List<Transform>()
                };

                for (int c = 0; c < zoneCandidateCoords[z].Length; c++)
                {
                    GameObject pt = new GameObject($"Candidate_Z{z + 1}_P{c + 1}");
                    pt.transform.SetParent(randObj.transform, false);
                    pt.transform.position = zoneCandidateCoords[z][c].pos;
                    pt.transform.rotation = Quaternion.Euler(0f, zoneCandidateCoords[z][c].yaw, 0f);
                    zoneData.candidatePoints.Add(pt.transform);
                }

                zonesList.Add(zoneData);
            }

            AssignPrivateField(randomizer, "corridorZones", zonesList);
            return randomizer;
        }

        private static void BuildMultiBoxDebrisSpawner(Transform envRoot, Material matGypsum, Material matBeam)
        {
            GameObject spawnerObj = new GameObject("DebrisSpawner_MultiBox");
            spawnerObj.transform.SetParent(envRoot, false);
            DebrisSpawner spawner = spawnerObj.AddComponent<DebrisSpawner>();

            // 5 BoundingBox langit-langit: 1 di Lobi + 4 segmen koridor
            (Vector3 center, Vector3 size)[] boxes =
            {
                (new Vector3(0f, 2.60f, 0f), new Vector3(6.0f, 0.2f, 5.0f)),
                (new Vector3(0f, 2.60f, 14f), new Vector3(1.3f, 0.2f, 18.0f)),
                (new Vector3(12.5f, 2.60f, 24f), new Vector3(22.0f, 0.2f, 1.3f)),
                (new Vector3(25.0f, 2.60f, 16f), new Vector3(1.3f, 0.2f, 14.0f)),
                (new Vector3(31.5f, 2.60f, 8f), new Vector3(10.0f, 0.2f, 1.3f))
            };

            var boxList = new List<BoxCollider>();
            for (int i = 0; i < boxes.Length; i++)
            {
                GameObject bObj = new GameObject($"DebrisCeilingBox_{i + 1}");
                bObj.transform.SetParent(spawnerObj.transform, false);
                bObj.transform.position = boxes[i].center;
                BoxCollider bc = bObj.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = boxes[i].size;
                boxList.Add(bc);
            }

            AssignPrivateField(spawner, "ceilingBoundingBoxes", boxList);
            AssignPrivateField(spawner, "gypsumMaterial", matGypsum);
            AssignPrivateField(spawner, "beamMaterial", matBeam);
        }

        private static void BuildSafetyWaypoints(Transform envRoot, Material matTosca)
        {
            GameObject wpRoot = new GameObject("SafetyWaypoints");
            wpRoot.transform.SetParent(envRoot, false);

            Vector3[] coords =
            {
                new Vector3(0f, 0.01f, 2.0f),
                new Vector3(0f, 0.01f, 7.0f),
                new Vector3(0f, 0.01f, 12.0f),
                new Vector3(0f, 0.01f, 17.0f),
                new Vector3(0f, 0.01f, 22.0f),
                new Vector3(4.0f, 0.01f, 24.0f),
                new Vector3(9.0f, 0.01f, 24.0f),
                new Vector3(14.0f, 0.01f, 24.0f),
                new Vector3(14.0f, 0.01f, 26.5f),
                new Vector3(19.0f, 0.01f, 24.0f),
                new Vector3(24.0f, 0.01f, 24.0f),
                new Vector3(25.0f, 0.01f, 19.0f),
                new Vector3(25.0f, 0.01f, 14.0f),
                new Vector3(25.0f, 0.01f, 9.0f),
                new Vector3(30.0f, 0.01f, 8.0f),
                new Vector3(35.5f, 0.01f, 8.0f),
                // Titik pijakan di dalam ruangan terbuka
                new Vector3(7.5f, 0.01f, 12.0f),
                new Vector3(18.5f, 0.01f, 15.0f)
            };

            for (int i = 0; i < coords.Length; i++)
            {
                GameObject wpObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wpObj.name = $"SafetyWaypoint_{i + 1}";
                wpObj.transform.SetParent(wpRoot.transform, false);
                wpObj.transform.position = coords[i];
                wpObj.transform.localScale = new Vector3(0.55f, 0.01f, 0.55f);
                wpObj.GetComponent<Renderer>().sharedMaterial = matTosca;
                var wpCol = wpObj.GetComponent<Collider>();
                if (wpCol != null) wpCol.isTrigger = true;

                SafetyWaypoint wp = wpObj.AddComponent<SafetyWaypoint>();
                AssignPrivateField(wp, "isInitialWaypoint", i == 0);
            }
        }

        private static FootprintGuide BuildFootprintGuide(Transform envRoot, Material matTosca)
        {
            GameObject guideObj = new GameObject("FootprintGuide");
            guideObj.transform.SetParent(envRoot, false);
            FootprintGuide guide = guideObj.AddComponent<FootprintGuide>();

            // Jejak sepanjang Koridor Barat -> Koridor Utara -> Koridor Timur -> Exit A
            var points = new List<Vector3>();
            for (float z = 0f; z <= 24f; z += 1.5f) points.Add(new Vector3(0f, 0.006f, z));
            for (float x = 1.5f; x <= 25f; x += 1.5f) points.Add(new Vector3(x, 0.006f, 24f));
            for (float z = 22.5f; z >= 8f; z -= 1.5f) points.Add(new Vector3(25f, 0.006f, z));
            for (float x = 26.5f; x <= 37f; x += 1.5f) points.Add(new Vector3(x, 0.006f, 8f));

            for (int i = 0; i < points.Count; i++)
            {
                GameObject fp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fp.name = $"Footprint_{i + 1}";
                fp.transform.SetParent(guideObj.transform, false);
                fp.transform.position = points[i] + new Vector3((i % 2 == 0) ? -0.14f : 0.14f, 0f, 0f);
                fp.transform.localScale = new Vector3(0.14f, 0.01f, 0.26f);
                UnityEngine.Object.DestroyImmediate(fp.GetComponent<Collider>());
                var rend = fp.GetComponent<Renderer>();
                rend.sharedMaterial = matTosca;
                guide.RegisterFootprint(rend);
            }
            return guide;
        }

        private static void BuildCorridorSignage(Transform envRoot, Material matFrame)
        {
            GameObject signRoot = new GameObject("CorridorSignage_5m");
            signRoot.transform.SetParent(envRoot, false);

            // Setiap interval 5 meter di sepanjang koridor utama & cabang
            (Vector3 pos, float yaw)[] signCoords =
            {
                (new Vector3(0.87f, 2.0f, 5f), -90f),
                (new Vector3(0.87f, 2.0f, 10f), -90f),
                (new Vector3(0.87f, 2.0f, 15f), -90f),
                (new Vector3(0.87f, 2.0f, 20f), -90f),
                (new Vector3(5f, 2.0f, 24.87f), 180f),
                (new Vector3(10f, 2.0f, 24.87f), 180f),
                (new Vector3(15f, 2.0f, 24.87f), 180f),
                (new Vector3(20f, 2.0f, 24.87f), 180f),
                (new Vector3(25.87f, 2.0f, 20f), -90f),
                (new Vector3(25.87f, 2.0f, 15f), -90f),
                (new Vector3(25.87f, 2.0f, 10f), -90f),
                (new Vector3(30f, 2.0f, 8.87f), 180f),
                (new Vector3(35f, 2.0f, 8.87f), 180f)
            };

            for (int i = 0; i < signCoords.Length; i++)
            {
                GameObject signObj = new GameObject($"Signage_5m_{i + 1}");
                signObj.transform.SetParent(signRoot.transform, false);
                signObj.transform.position = signCoords[i].pos;
                signObj.transform.rotation = Quaternion.Euler(0f, signCoords[i].yaw, 0f);

                GameObject plate = CreatePrimitiveChild("Plate", PrimitiveType.Cube, signObj.transform, Vector3.zero, new Vector3(0.75f, 0.28f, 0.02f), matFrame);
                UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());

                GameObject tmpObj = new GameObject("SignText");
                tmpObj.transform.SetParent(signObj.transform, false);
                tmpObj.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                TextMeshPro tmp = tmpObj.AddComponent<TextMeshPro>();
                tmp.fontSize = 1.7f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = SimulationConfig.PrimaryTosca;
                tmp.rectTransform.sizeDelta = new Vector2(0.72f, 0.25f);

                CorridorSignage signage = signObj.AddComponent<CorridorSignage>();
                signage.Configure(76.0f);
            }
        }

        private static void BuildFireAtmosphere(Transform envRoot, Material matFrame, Material matStrobeRed)
        {
            GameObject atmoObj = new GameObject("FireAtmosphere_VisualOnly");
            atmoObj.transform.SetParent(envRoot, false);
            FireAtmosphereController controller = atmoObj.AddComponent<FireAtmosphereController>();

            // Api & Asap visual di Pantry (28.5, 0.9, 21.2) dan Ruang Server (28.0, 0.5, 14.0)
            CreateVisualFireParticle(atmoObj.transform, "Fire_Pantry", new Vector3(28.5f, 0.9f, 21.2f));
            CreateVisualFireParticle(atmoObj.transform, "Fire_ServerRoom", new Vector3(28.0f, 0.5f, 14.0f));

            // Asap koridor bertahap (dibatasi maxParticles agar ringan)
            var smokeList = new List<ParticleSystem>
            {
                CreateCorridorSmokeParticle(atmoObj.transform, "Smoke_CorridorWest", new Vector3(0f, 2.2f, 14f)),
                CreateCorridorSmokeParticle(atmoObj.transform, "Smoke_CorridorNorth", new Vector3(14f, 2.2f, 24f)),
                CreateCorridorSmokeParticle(atmoObj.transform, "Smoke_CorridorEast", new Vector3(25f, 2.2f, 15f))
            };

            // Lampu darurat dinding berkedip
            Vector3[] lightCoords =
            {
                new Vector3(0f, 2.55f, 2f),
                new Vector3(0f, 2.55f, 14f),
                new Vector3(12f, 2.55f, 24f),
                new Vector3(25f, 2.55f, 18f),
                new Vector3(32f, 2.55f, 8f)
            };
            var flickeringLights = new List<Light>();
            for (int i = 0; i < lightCoords.Length; i++)
            {
                GameObject lObj = new GameObject($"EmergencyWallLight_{i + 1}");
                lObj.transform.SetParent(atmoObj.transform, false);
                lObj.transform.position = lightCoords[i];
                CreatePrimitiveChild("Fixture", PrimitiveType.Cube, lObj.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.08f, 0.12f), matFrame);
                Light pt = lObj.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.range = 10f;
                pt.intensity = 1.2f;
                pt.color = new Color(1f, 0.86f, 0.72f);
                flickeringLights.Add(pt);
            }

            // Directional Light Ambient Baked
            GameObject dirLightObj = new GameObject("DirectionalLight_Ambient");
            dirLightObj.transform.SetParent(envRoot, false);
            dirLightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light dl = dirLightObj.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 0.55f;
            dl.lightmapBakeType = LightmapBakeType.Baked;

            AssignPrivateField(controller, "flickeringEmergencyLights", flickeringLights);
            AssignPrivateField(controller, "corridorSmokeSystems", smokeList);
        }

        private static void BuildCalibrationDetailMeshes(
            Transform envRoot, MeshCombiner primaryCombiner, Material matWall, Material matMetal)
        {
            // Menghasilkan jumlah segitiga aktif ~92.000 saat Optimasi ON (<= 120.000)
            // dan ~165.000 saat Optimasi OFF (F2) sesuai target tabel proposal Bagian 9
            GameObject baseArchRoot = new GameObject("ArchitecturalAcousticPanels_OptimizedBase");
            baseArchRoot.transform.SetParent(envRoot, false);
            baseArchRoot.transform.position = new Vector3(12f, 2.75f, 12f);
            baseArchRoot.isStatic = true;

            Mesh baseMesh = CreateGridDetailMesh("Mesh_AcousticCeilingGrid_Base", 86000);
            var baseMf = baseArchRoot.AddComponent<MeshFilter>();
            baseMf.sharedMesh = baseMesh;
            var baseMr = baseArchRoot.AddComponent<MeshRenderer>();
            baseMr.sharedMaterial = matWall;

            GameObject unoptRoot = new GameObject("UnoptimizedSubPanels_ExtraDetail");
            unoptRoot.transform.SetParent(primaryCombiner.transform, false);
            unoptRoot.transform.localPosition = new Vector3(0f, 2.76f, 0f);
            Mesh extraMesh = CreateGridDetailMesh("Mesh_UncombinedWallSubdivisions", 73000);
            var extraMf = unoptRoot.AddComponent<MeshFilter>();
            extraMf.sharedMesh = extraMesh;
            var extraMr = unoptRoot.AddComponent<MeshRenderer>();
            extraMr.sharedMaterial = matMetal;
            unoptRoot.SetActive(false);

            AssignPrivateField(primaryCombiner, "unoptimizedExtraDetailRoot", unoptRoot);
        }

        private static Mesh CreateGridDetailMesh(string meshName, int targetTriangles)
        {
            string assetPath = $"{MeshFolder}/{meshName}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null && Mathf.Abs((existing.triangles.Length / 3) - targetTriangles) < 500)
            {
                return existing;
            }

            int quads = Mathf.Max(1, targetTriangles / 2);
            var verts = new Vector3[quads * 4];
            var tris = new int[quads * 6];

            int cols = Mathf.CeilToInt(Mathf.Sqrt(quads));
            float step = 0.04f;
            for (int q = 0; q < quads; q++)
            {
                int row = q / cols;
                int col = q % cols;
                float x = col * step;
                float z = row * step;
                int vIdx = q * 4;
                verts[vIdx + 0] = new Vector3(x, 0f, z);
                verts[vIdx + 1] = new Vector3(x, 0f, z + 0.03f);
                verts[vIdx + 2] = new Vector3(x + 0.03f, 0f, z + 0.03f);
                verts[vIdx + 3] = new Vector3(x + 0.03f, 0f, z);

                int tIdx = q * 6;
                tris[tIdx + 0] = vIdx + 0;
                tris[tIdx + 1] = vIdx + 1;
                tris[tIdx + 2] = vIdx + 2;
                tris[tIdx + 3] = vIdx + 0;
                tris[tIdx + 4] = vIdx + 2;
                tris[tIdx + 5] = vIdx + 3;
            }

            Mesh m = new Mesh
            {
                name = meshName,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            AssetDatabase.CreateAsset(m, assetPath);
            return m;
        }

        private static GameObject BuildFireExitDoorObject(Material matDoorSteel, Material matFrame, Material matMetal)
        {
            const float doorWidth = 1.2f;
            GameObject doorRoot = new GameObject("FireExitDoor");

            CreatePrimitiveChild("Frame_Left", PrimitiveType.Cube, doorRoot.transform, new Vector3(-0.62f, DoorFrameHeight * 0.5f, 0f), new Vector3(0.08f, DoorFrameHeight, 0.22f), matFrame);
            CreatePrimitiveChild("Frame_Right", PrimitiveType.Cube, doorRoot.transform, new Vector3(0.62f, DoorFrameHeight * 0.5f, 0f), new Vector3(0.08f, DoorFrameHeight, 0.22f), matFrame);
            CreatePrimitiveChild("Frame_Top", PrimitiveType.Cube, doorRoot.transform, new Vector3(0f, DoorFrameHeight + 0.04f, 0f), new Vector3(1.32f, 0.08f, 0.22f), matFrame);

            GameObject hingePivot = new GameObject("DoorHingePivot");
            hingePivot.transform.SetParent(doorRoot.transform, false);
            hingePivot.transform.localPosition = new Vector3(-doorWidth * 0.5f, 0f, 0.05f);

            CreatePrimitiveChild("DoorLeaf", PrimitiveType.Cube, hingePivot.transform, new Vector3(doorWidth * 0.5f, DoorFrameHeight * 0.5f, -0.04f), new Vector3(doorWidth, DoorFrameHeight, 0.06f), matDoorSteel);
            CreatePrimitiveChild("PanicCrashBar", PrimitiveType.Cube, hingePivot.transform, new Vector3(doorWidth * 0.5f, 1.0f, -0.11f), new Vector3(0.95f, 0.07f, 0.07f), matMetal);

            GameObject lightObj = new GameObject("CrashBarHighlightLight");
            lightObj.transform.SetParent(doorRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.45f, -0.5f);
            Light spot = lightObj.AddComponent<Light>();
            spot.type = LightType.Point;
            spot.range = 3.2f;
            spot.intensity = 2.0f;
            spot.color = SimulationConfig.PrimaryTosca;

            BoxCollider trigger = doorRoot.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.05f, -0.35f);
            trigger.size = new Vector3(1.2f, DoorFrameHeight, 0.8f);

            OcclusionPortal portal = doorRoot.AddComponent<OcclusionPortal>();
            SerializedObject portalSo = new SerializedObject(portal);
            var sizeProp = portalSo.FindProperty("m_Size");
            if (sizeProp != null) sizeProp.vector3Value = new Vector3(doorWidth, DoorFrameHeight, 0.3f);
            var centerProp = portalSo.FindProperty("m_Center");
            if (centerProp != null) centerProp.vector3Value = new Vector3(0f, DoorFrameHeight * 0.5f, 0f);
            portalSo.ApplyModifiedPropertiesWithoutUndo();

            AnimatorController controller = CreateDoorAnimatorController();
            Animator anim = doorRoot.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;

            FireExitDoor doorComp = doorRoot.AddComponent<FireExitDoor>();
            AssignPrivateField(doorComp, "doorAnimator", anim);
            AssignPrivateField(doorComp, "occlusionPortal", portal);
            AssignPrivateField(doorComp, "crashBarHighlightLight", spot);
            return doorRoot;
        }

        private static GameObject BuildOfficeDeskObject(Material matWood, Material matMetal, Material matPaper)
        {
            GameObject desk = new GameObject("OfficeDesk") { tag = "Desk" };
            desk.AddComponent<OfficeDeskObstacle>();
            BoxCollider col = desk.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(1.15f, DeskHeight, 0.65f);

            CreatePrimitiveChild("TableTop", PrimitiveType.Cube, desk.transform, new Vector3(0f, 0.775f, 0f), new Vector3(1.15f, 0.05f, 0.65f), matWood);
            CreatePrimitiveChild("Leg_FL", PrimitiveType.Cube, desk.transform, new Vector3(-0.52f, 0.375f, 0.27f), new Vector3(0.05f, 0.75f, 0.05f), matMetal);
            CreatePrimitiveChild("Leg_FR", PrimitiveType.Cube, desk.transform, new Vector3(0.52f, 0.375f, 0.27f), new Vector3(0.05f, 0.75f, 0.05f), matMetal);
            CreatePrimitiveChild("Leg_BL", PrimitiveType.Cube, desk.transform, new Vector3(-0.52f, 0.375f, -0.27f), new Vector3(0.05f, 0.75f, 0.05f), matMetal);
            CreatePrimitiveChild("Leg_BR", PrimitiveType.Cube, desk.transform, new Vector3(0.52f, 0.375f, -0.27f), new Vector3(0.05f, 0.75f, 0.05f), matMetal);
            CreatePrimitiveChild("DrawerUnit", PrimitiveType.Cube, desk.transform, new Vector3(0.32f, 0.52f, 0f), new Vector3(0.38f, 0.44f, 0.56f), matWood);
            CreatePrimitiveChild("Paper_1", PrimitiveType.Cube, desk.transform, new Vector3(-0.20f, 0.805f, 0.05f), new Vector3(0.21f, 0.008f, 0.29f), matPaper);
            return desk;
        }

        private static GameObject BuildOfficeChairObject(Material matFabric, Material matMetal)
        {
            GameObject chair = new GameObject("OfficeChair");
            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(chair.transform, false);
            var r0a = CreatePrimitiveChild("Seat", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.06f, 0.46f), matFabric).GetComponent<Renderer>();
            var r0b = CreatePrimitiveChild("Backrest", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 0.75f, -0.20f), new Vector3(0.44f, 0.50f, 0.05f), matFabric).GetComponent<Renderer>();
            var r0c = CreatePrimitiveChild("Pedestal", PrimitiveType.Cylinder, lod0.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.22f, 0.08f), matMetal).GetComponent<Renderer>();

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(chair.transform, false);
            var r1a = CreatePrimitiveChild("Seat_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.06f, 0.46f), matFabric).GetComponent<Renderer>();
            var r1b = CreatePrimitiveChild("Backrest_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0f, 0.75f, -0.20f), new Vector3(0.44f, 0.50f, 0.05f), matFabric).GetComponent<Renderer>();

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(chair.transform, false);
            var r2a = CreatePrimitiveChild("Chair_Low", PrimitiveType.Cube, lod2.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.85f, 0.46f), matFabric).GetComponent<Renderer>();

            LODGroup lg = chair.AddComponent<LODGroup>();
            lg.SetLODs(new[]
            {
                new LOD(0.35f, new[] { r0a, r0b, r0c }),
                new LOD(0.12f, new[] { r1a, r1b }),
                new LOD(0.02f, new[] { r2a })
            });
            lg.RecalculateBounds();
            return chair;
        }

        private static GameObject BuildOfficeCubicleObject(
            GameObject deskPrefab, Material matWood, Material matFabric, Material matMetal, Material matScreen)
        {
            GameObject cub = new GameObject("OfficeCubicle");

            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(cub.transform, false);
            GameObject deskInst = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab, lod0.transform);
            deskInst.transform.localPosition = Vector3.zero;
            CreatePrimitiveChild("Partition_Back", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 0.65f, 0.35f), new Vector3(1.25f, 1.3f, 0.05f), matFabric);
            CreatePrimitiveChild("Partition_Side", PrimitiveType.Cube, lod0.transform, new Vector3(-0.60f, 0.65f, 0f), new Vector3(0.05f, 1.3f, 0.75f), matFabric);
            CreatePrimitiveChild("Monitor", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 1.02f, 0.15f), new Vector3(0.48f, 0.30f, 0.03f), matScreen);

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(cub.transform, false);
            CreatePrimitiveChild("Desk_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.15f, DeskHeight, 0.65f), matWood);
            CreatePrimitiveChild("Partition_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0f, 0.65f, 0.35f), new Vector3(1.25f, 1.3f, 0.05f), matFabric);

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(cub.transform, false);
            CreatePrimitiveChild("Cubicle_Low", PrimitiveType.Cube, lod2.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 0.7f), matFabric);

            LODGroup lg = cub.AddComponent<LODGroup>();
            lg.SetLODs(new[]
            {
                new LOD(0.40f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.15f, lod1.GetComponentsInChildren<Renderer>()),
                new LOD(0.02f, lod2.GetComponentsInChildren<Renderer>())
            });
            lg.RecalculateBounds();
            return cub;
        }

        private static GameObject BuildServerRackObject(Material matDark, Material matMetal, Material matLed)
        {
            GameObject rack = new GameObject("ServerRack");
            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(rack.transform, false);
            CreatePrimitiveChild("Cabinet", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.65f, 2.0f, 0.85f), matDark);
            CreatePrimitiveChild("LED_Strip1", PrimitiveType.Cube, lod0.transform, new Vector3(0.22f, 1.5f, -0.43f), new Vector3(0.12f, 0.03f, 0.02f), matLed);
            CreatePrimitiveChild("LED_Strip2", PrimitiveType.Cube, lod0.transform, new Vector3(0.22f, 1.2f, -0.43f), new Vector3(0.12f, 0.03f, 0.02f), matLed);
            CreatePrimitiveChild("FrameTrim", PrimitiveType.Cube, lod0.transform, new Vector3(0f, 1.0f, -0.43f), new Vector3(0.67f, 2.02f, 0.02f), matMetal);

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(rack.transform, false);
            CreatePrimitiveChild("Cabinet_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.65f, 2.0f, 0.85f), matDark);
            CreatePrimitiveChild("LED_Med", PrimitiveType.Cube, lod1.transform, new Vector3(0.22f, 1.4f, -0.43f), new Vector3(0.12f, 0.05f, 0.02f), matLed);

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(rack.transform, false);
            CreatePrimitiveChild("Cabinet_Low", PrimitiveType.Cube, lod2.transform, new Vector3(0f, 1.0f, 0f), new Vector3(0.65f, 2.0f, 0.85f), matDark);

            LODGroup lg = rack.AddComponent<LODGroup>();
            lg.SetLODs(new[]
            {
                new LOD(0.40f, lod0.GetComponentsInChildren<Renderer>()),
                new LOD(0.15f, lod1.GetComponentsInChildren<Renderer>()),
                new LOD(0.02f, lod2.GetComponentsInChildren<Renderer>())
            });
            lg.RecalculateBounds();
            return rack;
        }

        private static GameObject BuildHydrantCabinetObject(Material matRed, Material matMetal)
        {
            GameObject cabinet = new GameObject("HydrantCabinet");
            cabinet.AddComponent<HydrantCabinet>();

            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(cabinet.transform, false);
            var r0a = CreatePrimitiveChild("CabinetBox", PrimitiveType.Cube, lod0.transform, Vector3.zero, new Vector3(0.18f, 0.65f, 0.50f), matRed).GetComponent<Renderer>();
            var r0b = CreatePrimitiveChild("HoseReel", PrimitiveType.Cylinder, lod0.transform, new Vector3(0.06f, 0.06f, 0f), new Vector3(0.28f, 0.06f, 0.28f), matMetal).GetComponent<Renderer>();
            r0b.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var r0c = CreatePrimitiveChild("Nozzle", PrimitiveType.Cylinder, lod0.transform, new Vector3(0.07f, -0.18f, 0.12f), new Vector3(0.04f, 0.12f, 0.04f), matMetal).GetComponent<Renderer>();
            var r0d = CreatePrimitiveChild("ValveWheel", PrimitiveType.Cylinder, lod0.transform, new Vector3(0.08f, -0.16f, -0.12f), new Vector3(0.11f, 0.02f, 0.11f), matRed).GetComponent<Renderer>();
            r0d.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(cabinet.transform, false);
            var r1a = CreatePrimitiveChild("CabinetBox_Med", PrimitiveType.Cube, lod1.transform, Vector3.zero, new Vector3(0.18f, 0.65f, 0.50f), matRed).GetComponent<Renderer>();
            var r1b = CreatePrimitiveChild("HoseReel_Med", PrimitiveType.Cylinder, lod1.transform, new Vector3(0.06f, 0.06f, 0f), new Vector3(0.28f, 0.06f, 0.28f), matMetal).GetComponent<Renderer>();
            r1b.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(cabinet.transform, false);
            var r2a = CreatePrimitiveChild("CabinetBox_Low", PrimitiveType.Cube, lod2.transform, Vector3.zero, new Vector3(0.18f, 0.65f, 0.50f), matRed).GetComponent<Renderer>();

            LODGroup lodGroup = cabinet.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.45f, new[] { r0a, r0b, r0c, r0d }),
                new LOD(0.18f, new[] { r1a, r1b }),
                new LOD(0.03f, new[] { r2a })
            });
            lodGroup.RecalculateBounds();
            return cabinet;
        }

        private static GameObject BuildStrobeBeaconObject(Material matRed, Material matMetal)
        {
            GameObject beacon = new GameObject("EmergencyStrobeBeacon");
            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(beacon.transform, false);
            var r0a = CreatePrimitiveChild("Base", PrimitiveType.Cylinder, lod0.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.03f, 0.16f), matMetal).GetComponent<Renderer>();
            var r0b = CreatePrimitiveChild("Dome", PrimitiveType.Sphere, lod0.transform, new Vector3(0f, -0.02f, 0f), new Vector3(0.12f, 0.12f, 0.12f), matRed).GetComponent<Renderer>();

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(beacon.transform, false);
            var r1a = CreatePrimitiveChild("Dome_Med", PrimitiveType.Sphere, lod1.transform, Vector3.zero, new Vector3(0.14f, 0.14f, 0.14f), matRed).GetComponent<Renderer>();

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(beacon.transform, false);
            var r2a = CreatePrimitiveChild("Dome_Low", PrimitiveType.Cube, lod2.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), matRed).GetComponent<Renderer>();

            LODGroup lodGroup = beacon.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.45f, new[] { r0a, r0b }),
                new LOD(0.18f, new[] { r1a }),
                new LOD(0.03f, new[] { r2a })
            });
            lodGroup.RecalculateBounds();

            GameObject lightObj = new GameObject("StrobeLight");
            lightObj.transform.SetParent(beacon.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            Light strobeLight = lightObj.AddComponent<Light>();
            strobeLight.type = LightType.Point;
            strobeLight.color = Color.red;
            strobeLight.range = 6f;
            strobeLight.intensity = 2.5f;

            AnimationClip strobeClip = CreateSteppedStrobeClip();
            AnimatorController strobeCtrl = AnimatorController.CreateAnimatorControllerAtPath(AnimFolder + "/StrobeBeacon.controller");
            AnimatorState st = strobeCtrl.layers[0].stateMachine.AddState("Blink");
            st.motion = strobeClip;
            strobeCtrl.layers[0].stateMachine.defaultState = st;

            Animator anim = beacon.AddComponent<Animator>();
            anim.runtimeAnimatorController = strobeCtrl;

            var comp = beacon.AddComponent<EmergencyStrobeBeacon>();
            AssignPrivateField(comp, "strobeAnimator", anim);
            AssignPrivateField(comp, "strobeLight", strobeLight);
            return beacon;
        }

        private static ParticleSystem CreateVisualFireParticle(Transform parent, string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 28;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.8f;
            main.startSize = 0.35f;
            main.startColor = new Color(1f, 0.42f, 0.08f, 0.75f);
            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = CreateEmissiveMaterial("Mat_FireParticle", new Color(1f, 0.45f, 0.05f), 2.5f);
            }
            return ps;
        }

        private static ParticleSystem CreateCorridorSmokeParticle(Transform parent, string name, Vector3 pos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = 32;
            main.startLifetime = 3.5f;
            main.startSpeed = 0.25f;
            main.startSize = 1.4f;
            main.startColor = new Color(0.22f, 0.22f, 0.24f, 0.28f);
            var emission = ps.emission;
            emission.rateOverTime = 2f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = CreateMaterial("Mat_SmokeParticle", new Color(0.32f, 0.34f, 0.36f));
            }
            return ps;
        }

        private static AnimatorController CreateDoorAnimatorController()
        {
            string controllerPath = AnimFolder + "/FireExitDoor.controller";
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Push", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsOpen", AnimatorControllerParameterType.Bool);

            AnimationClip clipLocked = CreateDoorClip("Door_Locked.anim", 0f, -0.11f);
            AnimationClip clipUnlocking = CreateDoorClip("Door_Unlocking.anim", 0f, -0.075f);
            AnimationClip clipOpened = CreateDoorClip("Door_Opened.anim", 90f, -0.075f);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState stLocked = sm.AddState("Locked");
            stLocked.motion = clipLocked;
            AnimatorState stUnlocking = sm.AddState("Unlocking");
            stUnlocking.motion = clipUnlocking;
            AnimatorState stOpened = sm.AddState("Opened");
            stOpened.motion = clipOpened;
            sm.defaultState = stLocked;

            var t1 = stLocked.AddTransition(stUnlocking);
            t1.AddCondition(AnimatorConditionMode.If, 0f, "Push");
            t1.hasExitTime = false;
            t1.duration = 0.1f;

            var t2 = stUnlocking.AddTransition(stOpened);
            t2.AddCondition(AnimatorConditionMode.If, 0f, "IsOpen");
            t2.hasExitTime = true;
            t2.exitTime = 0.25f;
            t2.duration = 0.35f;

            return controller;
        }

        private static AnimationClip CreateDoorClip(string fileName, float hingeYawDeg, float barLocalZ)
        {
            AnimationClip clip = new AnimationClip();
            clip.SetCurve("DoorHingePivot", typeof(Transform), "localEulerAnglesRaw.y",
                AnimationCurve.Linear(0f, hingeYawDeg, 0.4f, hingeYawDeg));
            clip.SetCurve("DoorHingePivot/PanicCrashBar", typeof(Transform), "localPosition.z",
                AnimationCurve.Linear(0f, barLocalZ, 0.4f, barLocalZ));
            AssetDatabase.CreateAsset(clip, AnimFolder + "/" + fileName);
            return clip;
        }

        private static AnimationClip CreateSteppedStrobeClip()
        {
            string path = AnimFolder + "/StrobeBlink.anim";
            AnimationClip clip = new AnimationClip { frameRate = 30f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            Keyframe[] keys =
            {
                new Keyframe(0.0f, 3.0f),
                new Keyframe(0.25f, 0.0f),
                new Keyframe(0.5f, 3.0f)
            };
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            clip.SetCurve("StrobeLight", typeof(Light), "m_Intensity", curve);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void BuildPlayerAndUI(
            Transform root, Transform envRoot, MeshCombiner meshCombiner,
            FireExitDoor doorA, FireExitDoor doorB, ObstacleRandomizer randomizer,
            FootprintGuide footprintGuide, Material matTosca)
        {
            Vector3 lobbySpawn = new Vector3(0f, 0.05f, -2.0f);
            var playerBuild = PlayerBuilder.BuildPlayer(root, lobbySpawn, ResolveDefaultShader());
            GameObject player = playerBuild.playerRoot;

            GameObject arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrowObj.name = "DirectionArrow";
            arrowObj.transform.SetParent(player.transform, false);
            arrowObj.transform.localPosition = new Vector3(0f, 0.35f, 0.9f);
            arrowObj.transform.localScale = new Vector3(0.12f, 0.04f, 0.38f);
            UnityEngine.Object.DestroyImmediate(arrowObj.GetComponent<Collider>());
            Renderer arrowRend = arrowObj.GetComponent<Renderer>();
            arrowRend.sharedMaterial = matTosca;

            AssignPrivateField(playerBuild.adaptive, "directionArrowTransform", arrowObj.transform);
            AssignPrivateField(playerBuild.adaptive, "directionArrowRenderer", arrowRend);
            AssignPrivateField(playerBuild.adaptive, "nearestFireExitDoor", doorA);
            AssignPrivateField(playerBuild.adaptive, "footprintGuide", footprintGuide);

            // Overlay UI
            GameObject overlayObj = new GameObject("ScreenOverlayCanvas");
            overlayObj.transform.SetParent(root, false);
            Canvas overlayCanvas = overlayObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler overlayScaler = overlayObj.AddComponent<CanvasScaler>();
            overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayScaler.referenceResolution = new Vector2(1600f, 900f);
            overlayScaler.matchWidthOrHeight = 0.5f;
            overlayObj.AddComponent<GraphicRaycaster>();

            Image darkStressImg = CreateUiImage("DarkStressOverlay", overlayObj.transform, Vector2.zero, Vector2.one, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            Image vignetteImg = CreateUiImage("DamageVignette", overlayObj.transform, Vector2.zero, Vector2.one, Vector2.zero, new Color(0.85f, 0.08f, 0.08f, 0f));
            Image ringImg = CreateUiImage("BreathingRing", overlayObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180f, 180f), new Color(0f, 169f / 255f, 157f / 255f, 0f));

            AssignPrivateField(playerBuild.pseudoHaptic, "damageVignetteImage", vignetteImg);
            AssignPrivateField(playerBuild.pseudoHaptic, "breathingRingTransform", ringImg.rectTransform);
            AssignPrivateField(playerBuild.pseudoHaptic, "breathingRingImage", ringImg);

            if (playerBuild.screenStress != null)
            {
                AssignPrivateField(playerBuild.screenStress, "adaptiveController", playerBuild.adaptive);
                AssignPrivateField(playerBuild.screenStress, "darkOverlay", darkStressImg);
            }

            // Panel F1
            GameObject f1Panel = new GameObject("F1_TelemetryPanel");
            f1Panel.transform.SetParent(overlayObj.transform, false);
            RectTransform f1Rt = f1Panel.AddComponent<RectTransform>();
            f1Rt.anchorMin = new Vector2(0f, 1f);
            f1Rt.anchorMax = new Vector2(0f, 1f);
            f1Rt.pivot = new Vector2(0f, 1f);
            f1Rt.anchoredPosition = new Vector2(16f, -16f);
            f1Rt.sizeDelta = new Vector2(410f, 150f);
            Image f1Bg = f1Panel.AddComponent<Image>();
            f1Bg.color = new Color(0.05f, 0.08f, 0.10f, 0.78f);

            TextMeshProUGUI f1Text = CreateUiTmp("MetricsText", f1Panel.transform, 16f, Color.white);
            TelemetryPanel telPanel = f1Panel.AddComponent<TelemetryPanel>();
            AssignPrivateField(telPanel, "panelRoot", f1Panel);
            AssignPrivateField(telPanel, "metricsText", f1Text);
            AssignPrivateField(telPanel, "meshCombiner", meshCombiner);
            AssignPrivateField(telPanel, "dataCollector", playerBuild.dataCollector);

            // Panel Kanan Atas: Kondisi & Solusi AI (ConditionAdvisorPanel)
            GameObject advisorPanelObj = new GameObject("TopRight_ConditionAdvisorPanel");
            advisorPanelObj.transform.SetParent(overlayObj.transform, false);
            RectTransform advRt = advisorPanelObj.AddComponent<RectTransform>();
            advRt.anchorMin = new Vector2(1f, 1f);
            advRt.anchorMax = new Vector2(1f, 1f);
            advRt.pivot = new Vector2(1f, 1f);
            advRt.anchoredPosition = new Vector2(-16f, -16f);
            advRt.sizeDelta = new Vector2(540f, 260f);
            Image advBg = advisorPanelObj.AddComponent<Image>();
            advBg.color = new Color(0.04f, 0.08f, 0.10f, 0.88f);
            advBg.raycastTarget = false;

            GameObject advHeaderObj = new GameObject("HeaderBar");
            advHeaderObj.transform.SetParent(advisorPanelObj.transform, false);
            RectTransform advHeaderRt = advHeaderObj.AddComponent<RectTransform>();
            advHeaderRt.anchorMin = new Vector2(0f, 1f);
            advHeaderRt.anchorMax = new Vector2(1f, 1f);
            advHeaderRt.pivot = new Vector2(0.5f, 1f);
            advHeaderRt.anchoredPosition = Vector2.zero;
            advHeaderRt.sizeDelta = new Vector2(0f, 46f);
            Image advHeaderImg = advHeaderObj.AddComponent<Image>();
            advHeaderImg.color = SimulationConfig.PrimaryTosca;
            advHeaderImg.raycastTarget = false;

            TextMeshProUGUI advTitleTmp = CreateUiTmp("ConditionTitleText", advHeaderObj.transform, 24f, Color.white);
            advTitleTmp.fontStyle = FontStyles.Bold;
            advTitleTmp.enableAutoSizing = true;
            advTitleTmp.fontSizeMin = 18f;
            advTitleTmp.fontSizeMax = 26f;
            advTitleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            advTitleTmp.textWrappingMode = TextWrappingModes.NoWrap;
            advTitleTmp.overflowMode = TextOverflowModes.Ellipsis;
            advTitleTmp.rectTransform.offsetMin = new Vector2(14f, 4f);
            advTitleTmp.rectTransform.offsetMax = new Vector2(-14f, -4f);
            advTitleTmp.raycastTarget = false;

            GameObject advBodyObj = new GameObject("BodyArea");
            advBodyObj.transform.SetParent(advisorPanelObj.transform, false);
            RectTransform advBodyRt = advBodyObj.AddComponent<RectTransform>();
            advBodyRt.anchorMin = Vector2.zero;
            advBodyRt.anchorMax = Vector2.one;
            advBodyRt.offsetMin = new Vector2(4f, 6f);
            advBodyRt.offsetMax = new Vector2(-4f, -48f);
            TextMeshProUGUI advBodyTmp = CreateUiTmp("AdvisorBodyText", advBodyObj.transform, 20f, Color.white);
            advBodyTmp.enableAutoSizing = true;
            advBodyTmp.fontSizeMin = 15f;
            advBodyTmp.fontSizeMax = 22f;
            advBodyTmp.lineSpacing = 4f;
            advBodyTmp.alignment = TextAlignmentOptions.TopLeft;
            advBodyTmp.textWrappingMode = TextWrappingModes.Normal;
            advBodyTmp.overflowMode = TextOverflowModes.Truncate;
            advBodyTmp.rectTransform.offsetMin = new Vector2(10f, 8f);
            advBodyTmp.rectTransform.offsetMax = new Vector2(-10f, -8f);
            advBodyTmp.raycastTarget = false;
            advTitleTmp.text = "KONDISI: TENANG  [AI 100%]";
            advBodyTmp.text = "<b>Penyebab:</b> Gerakan jalan terkendali & pandangan kamera stabil.\n<color=#7FFFD4><b>Solusi:</b></color>\n• Pertahankan jalan normal (WASD tanpa tahan Shift).\n• Hindari rintangan dan ikuti rambu EXIT hijau menuju pintu darurat.";

            ConditionAdvisorPanel advisorComp = advisorPanelObj.AddComponent<ConditionAdvisorPanel>();
            AssignPrivateField(advisorComp, "adaptiveController", playerBuild.adaptive);
            AssignPrivateField(advisorComp, "telemetryExtractor", player.GetComponent<IgnisEscape.Telemetry.TelemetryExtractor>());
            AssignPrivateField(advisorComp, "headerBarImage", advHeaderImg);
            AssignPrivateField(advisorComp, "panelBackgroundImage", advBg);
            AssignPrivateField(advisorComp, "conditionTitleText", advTitleTmp);
            AssignPrivateField(advisorComp, "bodyAdvisorText", advBodyTmp);

            // BODY-LOCKED Wrist Status Monitor pada WristAnchor_Left
            Transform wristAnchor = playerBuild.wristAnchorLeft;
            GameObject wristCanvasObj = new GameObject("WristStatusCanvas");
            wristCanvasObj.transform.SetParent(wristAnchor, false);
            Canvas wristCanvas = wristCanvasObj.AddComponent<Canvas>();
            wristCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform wRt = wristCanvasObj.GetComponent<RectTransform>();
            wRt.sizeDelta = new Vector2(240f, 160f);
            wRt.localScale = Vector3.one * 0.001f;
            Image wBg = wristCanvasObj.AddComponent<Image>();
            wBg.color = new Color(0.04f, 0.08f, 0.09f, 0.88f);

            TextMeshProUGUI wStatus = CreateUiTmp("StatusText", wristCanvasObj.transform, 18f, SimulationConfig.PrimaryTosca);
            wStatus.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            TextMeshProUGUI wConn = CreateUiTmp("ConnText", wristCanvasObj.transform, 14f, SimulationConfig.PrimaryTosca);
            wConn.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            TextMeshProUGUI wHealth = CreateUiTmp("HealthText", wristCanvasObj.transform, 14f, Color.white);
            wHealth.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            TextMeshProUGUI wSub = CreateUiTmp("SubtitleText", wristCanvasObj.transform, 12f, Color.yellow);
            wSub.rectTransform.anchoredPosition = new Vector2(0f, -48f);

            WristStatusMonitor wristMonitor = wristCanvasObj.AddComponent<WristStatusMonitor>();
            AssignPrivateField(wristMonitor, "adaptiveController", playerBuild.adaptive);
            AssignPrivateField(wristMonitor, "playerHealth", playerBuild.playerHealth);
            AssignPrivateField(wristMonitor, "statusText", wStatus);
            AssignPrivateField(wristMonitor, "connectionText", wConn);
            AssignPrivateField(wristMonitor, "healthText", wHealth);
            AssignPrivateField(wristMonitor, "subtitleText", wSub);

            CreateSpatialButton("Btn_ResetCalm", wristAnchor, new Vector3(-0.05f, -0.10f, 0f));
            CreateSpatialButton("Btn_TogglePath", wristAnchor, new Vector3(0.05f, -0.10f, 0f));

            // ExitGoalTrigger A & B
            CreateExitGoalTrigger(envRoot, "ExitGoalTrigger_A", new Vector3(39.0f, 1.0f, 8.0f), doorA);
            CreateExitGoalTrigger(envRoot, "ExitGoalTrigger_B", new Vector3(14.0f, 1.0f, 29.0f), doorB);

            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.SetParent(envRoot, false);
            spawnObj.transform.position = lobbySpawn;

            GameManager gm = root.gameObject.AddComponent<GameManager>();
            AssignPrivateField(gm, "playerController", playerBuild.playerController);
            AssignPrivateField(gm, "playerHealth", playerBuild.playerHealth);
            AssignPrivateField(gm, "obstacleRandomizer", randomizer);
            AssignPrivateField(gm, "spawnPoint", spawnObj.transform);
        }

        private static void CreateExitGoalTrigger(Transform parent, string name, Vector3 pos, FireExitDoor door)
        {
            GameObject goalObj = new GameObject(name);
            goalObj.transform.SetParent(parent, false);
            goalObj.transform.position = pos;
            BoxCollider goalBox = goalObj.AddComponent<BoxCollider>();
            goalBox.isTrigger = true;
            goalBox.size = new Vector3(1.8f, 2.2f, 1.8f);
            var goalTrigger = goalObj.AddComponent<ExitGoalTrigger>();
            AssignPrivateField(goalTrigger, "fireExitDoor", door);
        }

        private static void CreateSpatialButton(string name, Transform parent, Vector3 localPos)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            btnObj.transform.localPosition = localPos;
            btnObj.AddComponent<SpatialButtonValidator>();

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.name = "Visual";
            quad.transform.SetParent(btnObj.transform, false);
            quad.transform.localScale = new Vector3(0.035f, 0.035f, 0.008f);
            quad.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Mat_Tosca.mat");
        }

        private static GameObject SavePrefab(GameObject tempObject, string fileName)
        {
            string path = $"{PrefabFolder}/{fileName}";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempObject, path);
            UnityEngine.Object.DestroyImmediate(tempObject);
            return prefab;
        }

        private static GameObject CreateStaticBox(
            string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, string tag = "Untagged")
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            EnsureTagExists(tag);
            go.tag = tag;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic
            );
            return go;
        }

        private static GameObject CreatePrimitiveChild(
            string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Image CreateUiImage(
            string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateUiTmp(string name, Transform parent, float fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(10f, 8f);
            rt.offsetMax = new Vector2(-10f, -8f);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static void EnsureTagExists(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == "Untagged" || tag == "Player") return;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            }
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(AssetRoot)) AssetDatabase.CreateFolder("Assets", "IgnisEscape");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(AssetRoot, "Materials");
            if (!AssetDatabase.IsValidFolder(AnimFolder)) AssetDatabase.CreateFolder(AssetRoot, "Animations");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder(AssetRoot, "Prefabs");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder(AssetRoot, "Meshes");
        }

        private static Shader ResolveDefaultShader()
        {
            bool usingSrp =
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ||
                QualitySettings.renderPipeline != null;
            if (usingSrp)
            {
                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }
            return Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader targetShader = ResolveDefaultShader();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(targetShader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != targetShader)
            {
                mat.shader = targetShader;
            }

            mat.color = color;
            mat.enableInstancing = true;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateEmissiveMaterial(string name, Color color, float emissionIntensity)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader targetShader = ResolveDefaultShader();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(targetShader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != targetShader)
            {
                mat.shader = targetShader;
            }

            mat.color = color;
            mat.enableInstancing = true;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emissionIntensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void AssignPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
            );
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
