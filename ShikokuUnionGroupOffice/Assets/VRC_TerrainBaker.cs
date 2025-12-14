#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class VRC_TerrainBaker_V2 : EditorWindow
{
    public Terrain sourceTerrain;
    public Material templateMaterial; // "Custom/VRC_TerrainWave_8Layer" を入れたマテリアル
    public VRC_WaveController udonControllerPrefab; // 前回のUdonスクリプトがついたPrefab

    public int resolution = 150;
    public float size = 200f;
    public Vector3 centerOffset;

    [MenuItem("Tools/VRC Terrain Baker V2 (8 Layers)")]
    public static void ShowWindow()
    {
        GetWindow<VRC_TerrainBaker_V2>("VRC Baker V2");
    }

    void OnGUI()
    {
        GUILayout.Label("8-Layer Terrain to VRChat Proxy", EditorStyles.boldLabel);

        sourceTerrain = (Terrain)EditorGUILayout.ObjectField("Source Terrain", sourceTerrain, typeof(Terrain), true);
        templateMaterial = (Material)EditorGUILayout.ObjectField("Template Material", templateMaterial, typeof(Material), false);
        udonControllerPrefab = (VRC_WaveController)EditorGUILayout.ObjectField("Udon Prefab", udonControllerPrefab, typeof(VRC_WaveController), false);

        GUILayout.Space(10);
        resolution = EditorGUILayout.IntSlider("Mesh Resolution", resolution, 32, 250);
        size = EditorGUILayout.FloatField("Size", size);
        centerOffset = EditorGUILayout.Vector3Field("Center Offset", centerOffset);

        if (GUILayout.Button("Bake & Setup Scene"))
        {
            Bake();
        }
    }

    void Bake()
    {
        if (!sourceTerrain || !templateMaterial)
        {
            Debug.LogError("Terrain or Material missing!");
            return;
        }

        // 1. ルート生成
        GameObject root = new GameObject("VRC_WaveSystem_8Layer");
        Vector3 tPos = sourceTerrain.transform.position;
        Vector3 worldCenter = tPos + centerOffset;

        // 2. メッシュ生成
        Mesh mesh = GenerateMesh(worldCenter);
        string meshPath = "Assets/VRC_WaveMesh_8L.asset";
        AssetDatabase.CreateAsset(mesh, meshPath);

        // 3. マテリアル設定 (8レイヤー対応)
        Material mat = new Material(templateMaterial);
        SetupMaterial(mat, sourceTerrain.terrainData);
        string matPath = "Assets/VRC_WaveMat_8L.mat";
        AssetDatabase.CreateAsset(mat, matPath);

        // 4. メッシュオブジェクト
        GameObject meshObj = new GameObject("ProxyMesh");
        meshObj.transform.parent = root.transform;
        meshObj.transform.position = new Vector3(worldCenter.x, 0, worldCenter.z);

        var mf = meshObj.AddComponent<MeshFilter>();
        var mr = meshObj.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;

        // 5. Udon & 木
        if (udonControllerPrefab)
        {
            // Prefabとしてインスタンス化（Udonの参照を壊さないため）
            GameObject udonObj = (GameObject)PrefabUtility.InstantiatePrefab(udonControllerPrefab.gameObject, root.transform);
            udonObj.name = "WaveController";
            var controller = udonObj.GetComponent<VRC_WaveController>();

            controller.targetMaterial = mat;

            // 木の配置
            GameObject treesRoot = new GameObject("Trees");
            treesRoot.transform.parent = root.transform;
            SetupVegetation(controller, treesRoot, worldCenter);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Bake Complete!");
    }

    Mesh GenerateMesh(Vector3 center)
    {
        Mesh mesh = new Mesh();
        int res = resolution;
        int vCount = (res + 1) * (res + 1);
        Vector3[] verts = new Vector3[vCount];
        Vector2[] uvs = new Vector2[vCount];
        int[] tris = new int[res * res * 6];

        float step = size / res;
        float offset = size * -0.5f;
        TerrainData td = sourceTerrain.terrainData;
        Vector3 tPos = sourceTerrain.transform.position;

        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                int i = z * (res + 1) + x;
                float px = offset + x * step;
                float pz = offset + z * step;
                Vector3 wPos = center + new Vector3(px, 0, pz);

                float h = sourceTerrain.SampleHeight(wPos);
                verts[i] = new Vector3(px, h, pz);

                // SplatMap UV (Global 0-1)
                float u = (wPos.x - tPos.x) / td.size.x;
                float v = (wPos.z - tPos.z) / td.size.z;
                uvs[i] = new Vector2(u, v);
            }
        }

        int t = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int row = res + 1;
                int i = z * row + x;
                tris[t++] = i; tris[t++] = i + row; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + row; tris[t++] = i + row + 1;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void SetupMaterial(Material mat, TerrainData td)
    {
        // Control Maps
        if (td.alphamapTextureCount > 0) mat.SetTexture("_Control", td.alphamapTextures[0]);
        if (td.alphamapTextureCount > 1) mat.SetTexture("_Control2", td.alphamapTextures[1]);

        TerrainLayer[] layers = td.terrainLayers;
        for (int i = 0; i < layers.Length && i < 8; i++)
        {
            mat.SetTexture("_Splat" + i, layers[i].diffuseTexture);
            if (layers[i].normalMapTexture) mat.SetTexture("_Normal" + i, layers[i].normalMapTexture);

            // Tile Size (Shaderでは worldPos / Scale で計算しているので、TileSizeそのままを入れる)
            // ※XとYが違う場合は近似
            mat.SetFloat("_Scale" + i, layers[i].tileSize.x);
        }
    }

    void SetupVegetation(VRC_WaveController controller, GameObject treesRoot, Vector3 center)
    {
        TerrainData td = sourceTerrain.terrainData;
        Vector3 tPos = sourceTerrain.transform.position;
        float rangeSq = (size * 0.5f) * (size * 0.5f);

        System.Collections.Generic.List<Transform> treeTransforms = new System.Collections.Generic.List<Transform>();

        foreach (var tree in td.treeInstances)
        {
            Vector3 wPos = Vector3.Scale(tree.position, td.size) + tPos;
            Vector3 distVec = new Vector3(wPos.x, 0, wPos.z) - new Vector3(center.x, 0, center.z);

            if (distVec.sqrMagnitude < rangeSq)
            {
                GameObject prefab = td.treePrototypes[tree.prototypeIndex].prefab;
                if (!prefab) continue;

                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, treesRoot.transform);
                obj.transform.position = wPos;
                obj.transform.rotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
                obj.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);

                treeTransforms.Add(obj.transform);
            }
        }

        // Udonに配列をセット
        controller.trees = treeTransforms.ToArray();

        // 変更を保存 (Prefabインスタンスのオーバーライドとして適用)
        EditorUtility.SetDirty(controller);
    }
}
#endif