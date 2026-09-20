using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class Sound : MonoBehaviour
{
    [SerializeField] private AudioMixer Mixer;
    [SerializeField] private Sprite mute;
    [SerializeField] private Sprite sound;
    [SerializeField] private Button mute_btn;
    [SerializeField] private Slider vol_slide;
    [SerializeField]private float volume;

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
