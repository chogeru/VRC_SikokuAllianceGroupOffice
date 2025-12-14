using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VRC_WaveController : UdonSharpBehaviour
{
    [Header("Target Material")]
    public Material targetMaterial;

    [Header("Wave Settings (Must match Shader)")]
    public float masterAmp = 1.0f;
    public float waveSpeed = 1.0f;
    public Vector2 waveDir = new Vector2(1, 0.6f);
    public float waveFreq = 0.1f;

    [Header("Vegetation")]
    public Transform[] trees; // エディタ拡張で自動登録される
    [HideInInspector] public Vector3[] basePositions; // 初期位置

    [Header("Optimization")]
    public float cullDistance = 50f; // プレイヤーに近い木だけ動かす

    private float time;
    private VRCPlayerApi localPlayer;
    private int treeCount;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
        if (targetMaterial != null)
        {
            targetMaterial.SetFloat("_WaveAmp", masterAmp);
            targetMaterial.SetFloat("_WaveSpeed", waveSpeed);
            targetMaterial.SetFloat("_WaveDirX", waveDir.x);
            targetMaterial.SetFloat("_WaveDirZ", waveDir.y);
            targetMaterial.SetFloat("_WaveFrequency", waveFreq);
        }

        // 初期位置のキャッシュ
        if (trees != null)
        {
            treeCount = trees.Length;
            basePositions = new Vector3[treeCount];
            for (int i = 0; i < treeCount; i++)
            {
                if (trees[i] != null) basePositions[i] = trees[i].position;
            }
        }
    }

    void Update()
    {
        time += Time.deltaTime;

        // 1. シェーダーに時間を送る (地面を動かす)
        if (targetMaterial != null)
        {
            targetMaterial.SetFloat("_WaveTime", time);
        }

        // 2. 植物を動かす (CPU計算)
        UpdateVegetation();
    }

    void UpdateVegetation()
    {
        if (trees == null || treeCount == 0) return;

        Vector3 playerPos = Vector3.zero;
        if (localPlayer != null) playerPos = localPlayer.GetPosition();

        float t = time * waveSpeed;
        float dirX = waveDir.x;
        float dirZ = waveDir.y;
        float freq = waveFreq;
        float amp = masterAmp;
        float cullSq = cullDistance * cullDistance;

        for (int i = 0; i < treeCount; i++)
        {
            if (trees[i] == null) continue;

            Vector3 basePos = basePositions[i];

            // 距離カリング (遠くの木は計算しない)
            if (Vector3.SqrMagnitude(basePos - playerPos) > cullSq) continue;

            // ★Shaderと同じ計算式 (ここがズレないポイント)
            float dotVal = basePos.x * dirX + basePos.z * dirZ;
            float h = Mathf.Sin(dotVal * freq + t);
            h += Mathf.Sin(basePos.x * 0.3f + t * 1.3f) * 0.5f;
            h *= amp;

            // 位置適用
            trees[i].position = new Vector3(basePos.x, basePos.y + h, basePos.z);

            // 簡易的な揺れ (回転)
            float sway = h * 5.0f; // 傾き係数
            trees[i].rotation = Quaternion.Euler(sway, 0, sway) * Quaternion.identity; // 元の回転を保持したい場合は要調整
        }
    }
}