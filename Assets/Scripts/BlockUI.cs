using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler ,IDropHandler
{
    private string key;
    GameObject move_temp;
    bool complete = false;

    // Start is called before the first frame update
    void Start()
    {
        //cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        key = name.Substring(0, name.Length - 2);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void BlokUiClick()
    {

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        //µå·¡±× ÁßÀÎ ÆÛÁñ key
        GameManager.Instance.drag_block_id = key;

        //moveImage »õ·Î »ý¼º
        GameManager.Instance.move_canvas.SetActive(true);
        GameManager.Instance.move_ui_prefab.GetComponent<Image>().sprite = GameManager.Instance.Puzzle[key].ui;
        move_temp = Instantiate<GameObject>(GameManager.Instance.move_ui_prefab, GameManager.Instance.move_canvas.transform);
        move_temp.transform.position = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        move_temp.transform.position = eventData.position;
        if (Vector2.Distance(GameManager.Instance.Puzzle[key].position, move_temp.transform.position) <= GameManager.Instance.block_distance)
        {
            //ºþÂ¦ÀÓ
            complete = true;
        }
        else
        {
            //ºþÂ¦ÀÓ ÄÚ·çÆ¾ ¸ØÃã
            complete = false;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (complete)
        {
            GameManager.Instance.Puzzle[key].block.SetActive(true);
            Destroy(move_temp.gameObject);
            move_temp = null;
            Destroy(gameObject);
        }
        else
        {
            Destroy(move_temp.gameObject);
            move_temp = null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {

    }

}
