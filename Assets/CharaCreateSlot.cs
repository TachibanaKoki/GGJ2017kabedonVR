using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharaCreateSlot : MonoBehaviour {
    public Text text;
    [SerializeField]
    string sceneName = "main";
    [SerializeField]
    Slot[] slots = new Slot[(int)SkinnedMeshCombiner.MAIN_PARTS.MAX];

    int[] currentIndex = new int[(int)SkinnedMeshCombiner.MAIN_PARTS.MAX];

    bool isSceneLoad = false;
    // Use this for initialization
    void Start ()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].runStopCallBack += OnSlotStop;
            slots[i].SlotStart();
            currentIndex[i] = -1;
        }

    }

    void OnSlotStop(int targetParts, int index)
    {
        currentIndex[targetParts] = index;
        Destroy(slots[targetParts]);
    }

    // Update is called once per frame
    void Update ()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (currentIndex[i] == -1) return;
        }
        if (isSceneLoad) return;
        isSceneLoad = true;
        text.text = "キャラクターが決定しました";
        PlayerPrefs.SetInt("SelectHead",currentIndex[0]);
        PlayerPrefs.SetInt("SelectBody",currentIndex[1]);
        PlayerPrefs.SetInt("SelectLeg",currentIndex[2]);
        PlayerPrefs.SetInt("SelectARM",currentIndex[3]);

        Camera.main.GetComponent<SceenFade>().LoadSceenWithFade(sceneName);
    }
}
