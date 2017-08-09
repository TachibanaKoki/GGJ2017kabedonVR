using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlotManager : MonoBehaviour
{
    [SerializeField]
    AvatarTest avatarTest;

    [SerializeField]
    Slot[] slots = new Slot[(int)SkinnedMeshCombiner.MAIN_PARTS.MAX];

    int[] currentIndex = new int[(int)SkinnedMeshCombiner.MAIN_PARTS.MAX];

    void Start ()
    {
		for(int i = 0;i< slots.Length;i++)
        {
            slots[i].runStopCallBack += OnSlotStop;
        }
	}

    void OnSlotStop(int targetParts,int index)
    {
        currentIndex[targetParts] = index;
        switch ((SkinnedMeshCombiner.MAIN_PARTS)targetParts)
        {
            case SkinnedMeshCombiner.MAIN_PARTS.HEAD:
                avatarTest.SelectHeadFile(index);
                break;
            case SkinnedMeshCombiner.MAIN_PARTS.ARM:
                avatarTest.SelectARMFile(index);
                break;
            case SkinnedMeshCombiner.MAIN_PARTS.BODY:
                avatarTest.SelectBodyFile(index);
                break;
            case SkinnedMeshCombiner.MAIN_PARTS.LEG:
                avatarTest.SelectLegFile(index);
                break;
        }
        ChangeAvator();
    }

    void ChangeAvator()
    {

        avatarTest.AvatarChange();
    }

	void Update ()
    {
		
	}
}
