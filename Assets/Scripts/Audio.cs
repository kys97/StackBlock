using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Audio : MonoBehaviour
{
    GameObject BGM;
    GameObject MainCamera;
    AudioSource audioSource;

    void Awake()
    {
        BGM = GameObject.Find("BGM");
        MainCamera = GameObject.Find("Main Camera");
        
        audioSource = BGM.GetComponent<AudioSource>();
        
        if (audioSource.isPlaying) return;
        
        DontDestroyOnLoad(BGM);
        DontDestroyOnLoad(MainCamera);

    }
}
