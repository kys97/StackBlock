using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class Sound : MonoBehaviour
{
    public AudioMixer Mixer;
    public Sprite mute;
    public Sprite sound;
    public Button mute_btn;
    public Slider vol_slide;
    [SerializeField]private float volume;
    [SerializeField] private float max_volume = 0;
    [SerializeField] private float min_volume = -80;

    private static Sound _instance;
    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else if (_instance != this)
            Destroy(gameObject);
        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        Mixer.SetFloat("BGM", (vol_slide.value  - 100) * (float)(2 /5.0));
        volume = (vol_slide.value - 100) * (float)(2 / 5.0);
    }

    public void AudioControl()
    {
        if(vol_slide.value == 0)
        {
            mute_btn.image.sprite = mute;
            AudioMute();
        }
        else
        {
            volume = (vol_slide.value - 100) * (float)(2 / 5.0);
            Mixer.SetFloat("BGM", volume);
        }
    }

    public void AudioMute()
    {
        volume = -80;
        Mixer.SetFloat("BGM", -80);
        vol_slide.value = 0;
    }
}
