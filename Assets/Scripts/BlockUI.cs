using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler ,IDropHandler
{
    [SerializeField]private int ui_id;
    GameObject move_temp;
    private Vector2 pos;

    // Start is called before the first frame update
    void Start()
    {
        //cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void BlokUiClick()
    {

    }

    public void SetUiId(int i) { ui_id = i; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        GameManager.Instance.drag_block_id = ui_id;

        GameManager.Instance.move_canvas.SetActive(true);
        GameManager.Instance.move_ui_prefab.GetComponent<Image>().sprite = GameManager.Instance.block_parts_image[ui_id - 1];
        move_temp = Instantiate<GameObject>(GameManager.Instance.move_ui_prefab, GameManager.Instance.move_canvas.transform);
        move_temp.transform.position = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        move_temp.transform.position = eventData.position;
        if (Vector2.Distance(GameManager.Instance.parts_pos[ui_id - 1], move_temp.transform.position) <= GameManager.Instance.block_distance)
        {

        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(GameManager.Instance.parts_pos[ui_id - 1], move_temp.transform.position) <= GameManager.Instance.block_distance)
        {
            GameManager.Instance.block_parts[ui_id - 1].transform.Find("object").gameObject.SetActive(true);
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
