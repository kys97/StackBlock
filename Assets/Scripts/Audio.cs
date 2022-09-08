using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Audio : MonoBehaviour
{
    GameObject BGM;
    GameObject MainCamera;
    GameObject Canvas;
    AudioSource audioSource;
    private GameObject[] Musics;

    void Awake()
    {
        Musics = GameObject.FindGameObjectsWithTag("Music");
        MainCamera = GameObject.Find("Main Camera");

        if (Musics.Length >= 2)
        {
             Destroy(this.gameObject);
        }
        
        DontDestroyOnLoad(transform.gameObject);
        DontDestroyOnLoad(MainCamera);
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayMusic()
    {
        if (audioSource.isPlaying) return;
        audioSource.Play();
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }
    
}
