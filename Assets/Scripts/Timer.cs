using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    Slider timer;
    float bar_time;

    int sucess_cnt;
    public GameObject fail_pan;

    // Start is called before the first frame update
    void Start()
    {
        timer = GetComponent<Slider>();
        sucess_cnt = GameManager.Instance.comlete_num;
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.Instance.status == GameManager.Status.Puzzle && GameManager.Instance.start)
        {
            if (sucess_cnt != GameManager.Instance.comlete_num)
            {
                timer.value += 5f;
                sucess_cnt = GameManager.Instance.comlete_num;
            }
            else if (timer.value > 0.0f)
                timer.value -= Time.deltaTime;
            else
            {
                GameManager.Instance.BlockFail();
                fail_pan.SetActive(true);
            }
        }
    }
}
