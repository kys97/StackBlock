using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SetVolume : MonoBehaviour
{
    public AudioMixer AudioMixer;
    public Scrollbar Scrollbar;
    
    // Start is called before the first frame update
    void Start()
    {
        Scrollbar.value = PlayerPrefs.GetFloat("Master", 0.75f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetLevel(float slidervalue)
    {
        if (slidervalue == 0) slidervalue = 0.0001f;
        AudioMixer.SetFloat("Master", Mathf.Log10(slidervalue) * 20);
        PlayerPrefs.SetFloat("Master", slidervalue);
    }

    public void MuteToggle(bool muted)
    {
        SetLevel(Scrollbar.value);
    }
}
