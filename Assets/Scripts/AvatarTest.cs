using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class AvatarTest : MonoBehaviour {
	
    private const int partsNum = (int)SkinnedMeshCombiner.MAIN_PARTS.MAX;

    [SerializeField]
    GameObject rootParent;


    [SerializeField]
    PlayableDirector director;

    [SerializeField] private string rootBoneFileName = null;
    [SerializeField] private string[] headFileNames = null;
    [SerializeField] private string[] bodyFileNames = null;
    [SerializeField] private string[] legFileNames = null;
    [SerializeField] private string[] armFileNames = null;

    public float loadTime = 0;

	private bool isLoading = false;

	private int charaCount = 0;

	private GameObject loadedPrefab = null;

    private string[] selectedFileNames = new string[partsNum];
    private ResourceRequest[] resourceReqs = new ResourceRequest[partsNum];

    private GameObject player = null;

    private Animator animator;

    // =====================================================
    // UI Interface
    // =====================================================
    public void SelectHeadFile(int index)
    {
		int idx = (int)SkinnedMeshCombiner.MAIN_PARTS.HEAD;
		if (headFileNames.Length >= index)
        {
			selectedFileNames[idx] = headFileNames[index];
			Debug.Log("SelectHeadFile " + selectedFileNames[idx]);
        }
    }

    public void SelectBodyFile(int index) {
		int idx = (int)SkinnedMeshCombiner.MAIN_PARTS.BODY;
		if (bodyFileNames.Length >= index) {
			selectedFileNames[idx] = bodyFileNames[index];
			Debug.Log("SelectBodyFile " + selectedFileNames[idx]);
		}
    }

    public void SelectLegFile(int index) {
		int idx = (int)SkinnedMeshCombiner.MAIN_PARTS.LEG;
		if (legFileNames.Length >= index) {
			selectedFileNames[idx] = legFileNames[index];
			Debug.Log("SelectLegFile " + selectedFileNames[idx]);
		}
    }

    public void SelectARMFile(int index)
    {
        int idx = (int)SkinnedMeshCombiner.MAIN_PARTS.ARM;
        if (armFileNames.Length >= index)
        {
            selectedFileNames[idx] = armFileNames[index];
            Debug.Log("SelectLegFile " + selectedFileNames[idx]);
        }
    }

    public void AvatarChange() {
        StartCoroutine(LoadAvatar());
    }

    // =======================================================

    void Awake () {
		Application.targetFrameRate = 60; // ターゲットフレームレートを60に設定
	}

	void Start () {
        animator = GetComponent<Animator>();
		Caching.ClearCache();
		Resources.UnloadUnusedAssets ();
        StartCoroutine (InitAvatar());
    }

	IEnumerator InitAvatar() {
        SelectHeadFile(PlayerPrefs.GetInt("SelectHead"));
        SelectBodyFile(PlayerPrefs.GetInt("SelectBody"));
        SelectLegFile(PlayerPrefs.GetInt("SelectLeg"));
        SelectARMFile(PlayerPrefs.GetInt("SelectARM"));
        yield return StartCoroutine(LoadAvatar());
    }

    IEnumerator LoadAvatar()
    {
        if (isLoading)
        {
            Debug.Log("Now Loading!");
            yield break;
        }

        // ルートボーン用のファイルを読み込む
		Debug.Log ("rootBoneFileName " + rootBoneFileName);
		ResourceRequest bornReq = Resources.LoadAsync<GameObject>(rootBoneFileName);

        // 各パーツのファイルを読み込む
        for ( int i = 0; i < partsNum; i++) {
			resourceReqs[i] = Resources.LoadAsync<Object>(selectedFileNames[i]);
        }

		// ロード待ち
        while (true) {
            bool isLoadEnd = true;
            for ( int i = 0; i < partsNum; i++)
            {
				if (!resourceReqs[i].isDone) isLoadEnd = false;
            }

            if (isLoadEnd)
            {
                break;
            }
            yield return null;
        }

		while (!bornReq.isDone)
        {
            yield return null;
        }

        // Resourcesから必要なファイルを読み込み終わったら、空のGameObjectを生成
        GameObject root = new GameObject();
        root.transform.position = Vector3.zero;
        root.transform.localScale = Vector3.one;
		root.name = "Ikemen";

        // 生成した空のGameObjectにSkinnedMeshCombinerを追加する（以下、Root)
        SkinnedMeshCombiner smc = root.AddComponent<SkinnedMeshCombiner>();
        smc.gameObject.AddComponent<Animator>();
        if ( bornReq.asset == null)
        {
            Debug.LogError("born asset is null");
        }
        // ルートボーン用のファイルをInstantiateする
        GameObject rootBone = (GameObject)Instantiate(bornReq.asset as GameObject);
        if (rootBone != null)
        {
            rootBone.transform.parent = root.transform;
            rootBone.transform.localPosition = Vector3.zero;
            rootBone.transform.localScale = Vector3.one;
            rootBone.transform.localRotation = Quaternion.identity;
            smc.rootBoneObject = rootBone.transform;
        } else
        {
            Debug.LogError("Root Bone Instantiate Error!");
            yield break;
        }

        // Rootの下に読み込んだファイル一式をInstanTiateする
        for ( int i = 0; i < partsNum; i++)
        {
            GameObject obj = (GameObject)Instantiate(resourceReqs[i].asset);
            if (obj != null)
            {
                Debug.Log("[" + i + "] " + obj.name);
                obj.transform.parent = root.transform;
                obj.transform.localPosition= Vector3.zero;
                obj.transform.localScale = Vector3.one;
                obj.transform.localRotation = Quaternion.identity;
                // 生成したモデルをRootのSkinnedMeshCombinerの各種プロパティに設定する
                smc.equipPartsObjectList[i] = obj;
            }
        }

        // レッツ・コンバイン
        smc.Combine();
        var binding = director.playableAsset.outputs.First(c => c.streamName == "Animation Track1");
        director.SetGenericBinding(binding.sourceObject, smc.anim);
        var bind = director.playableAsset.outputs.First(c => c.streamName == "Animation Track2");
        director.SetGenericBinding(bind.sourceObject, smc.GetComponent<Animator>());
        // AvatarTest.playerにRootを割り当てる（古いRootは削除する）
        if (player != null)
        {
            GameObject.DestroyImmediate(player);
            player = null;
        }
        root.transform.parent = rootParent.transform;

        player = root;
    }

}
