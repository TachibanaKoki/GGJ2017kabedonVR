using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager I;

    [SerializeField]
    AudioClip[] audioClip;

    //index 0 = main 1～SE
    [SerializeField]
    AudioSource[] audioSource;
	// Use this for initialization
	void Start ()
    {
        if (I == null)
        {
            I = this;
            DontDestroyOnLoad(I);
        }
        else
        {
            Destroy(gameObject);
        }
	}

    public void Stop()
    {
        audioSource[0].Stop();
    }
	
	public void PlayOneShot(string name)
    {
        for (int i = 0; i < audioClip.Length; i++)
        {
            if (audioClip[i].name ==name)
            {

                audioSource[1].PlayOneShot(audioClip[i]);
                return;
            }
        }
        Debug.Log("no match audio clips at name");
    }

    public void PlayOneShot(int index)
    {
        if (index >= audioClip.Length)
        {
            Debug.Log("no match audio clips");
            return;
        }

        audioSource[1].PlayOneShot(audioClip[index]);
    }
}
