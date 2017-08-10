using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class Slot : MonoBehaviour {

    [SerializeField]
    SkinnedMeshCombiner.MAIN_PARTS targetParts;

    [SerializeField]
    Text buttonText;

    [SerializeField]
    Sprite[] partsImages;


    Image Image;
    bool isRunSlot = false;
    int count = 0;

   public  UnityAction<int,int> runStopCallBack;


    void Awake()
    {
        Image = GetComponent<Image>();
        count = Random.Range(0, partsImages.Length);
    }

    public void SlotStart()
    {
        isRunSlot = !isRunSlot;
        if(isRunSlot)
        {
            StartCoroutine(RunSlot());
            buttonText.text = "STOP";

        }
        else
        {
            SoundManager.I.PlayOneShot("b_052");
            if (runStopCallBack != null) runStopCallBack((int)targetParts,count);
            buttonText.text = "START";
        }
    }


    IEnumerator RunSlot()
    {
        float t = 0;
        while(true)
        {
            t += Time.deltaTime;
            if(t>10.0f)
            {
                isRunSlot = false;
                break;
            }
            if (!isRunSlot) break;
            count++;
            if (count >= partsImages.Length)
            {
                count = 0;
            }
            Image.sprite = partsImages[count];
     
            yield return new WaitForSeconds(0.4f);
        }
    }
}
